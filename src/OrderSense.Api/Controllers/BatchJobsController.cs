using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Predictions;
using System.Text.Json; 
using Hangfire;
namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/batch-jobs")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
public class BatchJobsController(AppDbContext db, IBackgroundJobClient jobs, TimeProvider time) : ControllerBase
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    [HttpPost(Name = "CreateBatchJob")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<BatchJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "The file is missing or empty.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > MaxFileBytes)
        {
            ModelState.AddModelError("file", "The file is larger than 5 MB.");
            return ValidationProblem(ModelState);
        }
        BatchCsvParseResult parsed;
        await using (var stream = file.OpenReadStream())
        {
            parsed = BatchCsv.Parse(stream);
        }
        foreach (var error in parsed.FileErrors)
        {
            ModelState.AddModelError("file", error);
        }
        var badRows = parsed.Rows.Where(r => r.Error is not null).ToList();
        foreach (var row in badRows.Take(50))
        {
            ModelState.AddModelError($"row {row.RowNo}", row.Error!);
        }
        if (badRows.Count > 50)
        {
            ModelState.AddModelError("file", $"...and {badRows.Count - 50} more rows have errors.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        var job = new BatchJob
        {
            UserId = CurrentUserId(),
            Status = BatchJobStatus.Queued,
            FileName = Path.GetFileName(file.FileName) is { Length: > 0 } name ? name[..Math.Min(name.Length, 255)] : "upload.csv",
            TotalRows = parsed.Rows.Count,
            CreatedAt = time.GetUtcNow(),
            Results = parsed.Rows.Select(r => new BatchResult
            {
                RowNo = r.RowNo,
                Input = JsonSerializer.Serialize(r.Features, BatchJobProcessor.Json),
            }).ToList(),
        };
        db.BatchJobs.Add(job);
        await db.SaveChangesAsync(ct);
        jobs.Enqueue<BatchJobProcessor>(p => p.RunAsync(job.Id, CancellationToken.None));
        var dto = BatchJobMapping.ToDto(job);
        return AcceptedAtRoute("GetBatchJob", new { id = job.Id }, dto);
    }

    [HttpGet(Name = "GetBatchJobs")]
    [ProducesResponseType<IReadOnlyList<BatchJobDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<BatchJobDto>> GetAll(CancellationToken ct)
    {
        var jobs = await VisibleJobs()
            .OrderByDescending(j => j.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return jobs.Select(BatchJobMapping.ToDto).ToList();
    }

    [HttpGet("{id:guid}", Name = "GetBatchJob")]
    [ProducesResponseType<BatchJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchJobDto>> GetById(Guid id, CancellationToken ct)
    {
        var job = await VisibleJobs().SingleOrDefaultAsync(j => j.Id == id, ct);

        return job is null ? JobNotFound(id) : BatchJobMapping.ToDto(job);
    }

    [HttpGet("{id:guid}/results", Name = "GetBatchJobResults")]
    [ProducesResponseType<PagedResponse<BatchResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<BatchResultDto>>> GetResults(Guid id, [Range(1, 100_000)] int page = 1, [Range(1, 100)] int pageSize = 50, CancellationToken ct = default)
    {
        if (!await VisibleJobs().AnyAsync(j => j.Id == id, ct))
        {
            return JobNotFound(id);
        }

        var results = db.BatchResults.AsNoTracking().Where(r => r.JobId == id);
        var total = await results.CountAsync(ct);

        var items = await results
            .OrderBy(r => r.RowNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<BatchResultDto>(items.Select(BatchJobMapping.ToDto).ToList(), page, pageSize, total);
    }

    [HttpGet("{id:guid}/export", Name = "ExportBatchJob")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var job = await VisibleJobs().SingleOrDefaultAsync(j => j.Id == id, ct);

        if (job is null)
        {
            return JobNotFound(id);
        }

        if (job.Status is BatchJobStatus.Queued or BatchJobStatus.Running)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Batch job is not finished yet");
        }

        var results = await db.BatchResults
            .AsNoTracking()
            .Where(r => r.JobId == id)
            .OrderBy(r => r.RowNo)
            .ToListAsync(ct);

        byte[] csv = [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(BatchCsvWriter.Write(results))];
        var fileName = $"{Path.GetFileNameWithoutExtension(job.FileName)}-predictions.csv";

        return File(csv, "text/csv", fileName);
    }
    [HttpGet("template", Name = "GetBatchJobTemplate")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "text/csv")]
    public IActionResult GetTemplate()
    {
        return File(BatchCsv.TemplateBytes(), "text/csv", "batch-template.csv");
    }
    private IQueryable<BatchJob> VisibleJobs()
    {
        var jobs = db.BatchJobs.AsNoTracking();

        if (User.IsInRole(Roles.Admin))
        {
            return jobs;
        }

        var userId = CurrentUserId();

        return jobs.Where(j => j.UserId == userId);
    }

    private ObjectResult JobNotFound(Guid id) => Problem(statusCode: StatusCodes.Status404NotFound, title: $"Batch job '{id}' not found");
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}