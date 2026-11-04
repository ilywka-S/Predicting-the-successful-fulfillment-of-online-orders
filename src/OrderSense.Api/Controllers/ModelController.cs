using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/model")]
[Authorize]
public class ModelController : ControllerBase
{
    [HttpGet(Name = "GetModel")]
    [ProducesResponseType<ModelInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
    public ActionResult<ModelInfoDto> Get() => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Model info is not implemented yet");
}