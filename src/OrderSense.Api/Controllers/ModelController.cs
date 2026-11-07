using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderSense.Api.Dtos;
using OrderSense.Api.Predictions;
using OrderSense.Contracts;
namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/model")]
[Authorize]
public class ModelController(PredictionClient prediction) : ControllerBase
{
    [HttpGet(Name = "GetModel")]
    [ProducesResponseType<ModelInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ModelInfoDto>> Get(CancellationToken ct)
    {
        try
        {
            ModelDetails? details = await prediction.GetModelAsync(ct);
            if (details == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Model info is not available");
            }
            return ModelMapping.ToDto(details);
        }
        catch (PredictionServiceUnavailableException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Prediction service is unavailable");
        }
    }
}