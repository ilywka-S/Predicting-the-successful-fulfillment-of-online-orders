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

namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/batch-jobs")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
public class BatchJobsController(AppDbContext db) : ControllerBase
{
    [HttpPost(Name = "CreateBatchJob")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<BatchJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
    public ActionResult<BatchJobDto> Create(IFormFile file) => NotImplementedYet();

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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
    public IActionResult GetTemplate()
    {
        byte[] csv = BatchCsvTemplate.Bytes();
        return File(csv, "text/csv", "batch-order-template.csv");
    }

    private IQueryable<BatchJob> VisibleJobs()
    {
        var jobs = db.BatchJobs.AsNoTracking();

        if (User.IsInRole(Roles.Admin))
        {
            return jobs;
        }

        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return jobs.Where(j => j.UserId == userId);
    }

    private ObjectResult JobNotFound(Guid id) => Problem(statusCode: StatusCodes.Status404NotFound, title: $"Batch job '{id}' not found");

    private ObjectResult NotImplementedYet() => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Batch predictions are not implemented yet");
}