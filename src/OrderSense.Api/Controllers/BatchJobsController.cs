using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/batch-jobs")]
[Authorize(Roles = $"{Roles.Analyst},{Roles.Admin}")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
public class BatchJobsController : ControllerBase
{
    [HttpPost(Name = "CreateBatchJob")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<BatchJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<BatchJobDto> Create(IFormFile file) => NotImplementedYet();

    [HttpGet(Name = "GetBatchJobs")]
    [ProducesResponseType<IReadOnlyList<BatchJobDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<BatchJobDto>> GetAll() => NotImplementedYet();

    [HttpGet("{id:guid}", Name = "GetBatchJob")]
    [ProducesResponseType<BatchJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<BatchJobDto> GetById(Guid id) => NotImplementedYet();

    [HttpGet("{id:guid}/results", Name = "GetBatchJobResults")]
    [ProducesResponseType<PagedResponse<BatchResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<PagedResponse<BatchResultDto>> GetResults(Guid id, [Range(1, 100_000)] int page = 1, [Range(1, 100)] int pageSize = 50) => NotImplementedYet();

    [HttpGet("{id:guid}/export", Name = "ExportBatchJob")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult Export(Guid id) => NotImplementedYet();

    [HttpGet("template", Name = "GetBatchJobTemplate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    public IActionResult GetTemplate() => NotImplementedYet();

    private ObjectResult NotImplementedYet() => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Batch predictions are not implemented yet");
}