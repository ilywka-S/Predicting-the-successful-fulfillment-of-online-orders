using OrderSense.Contracts;
namespace OrderSense.PredictionService;
public static class ModelEndpoints
{
    public static IResult Get(ModelInfoService service)
    {
        ModelDetails? details = service.Details;
        if (details == null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Model info is not available");
        }
        return Results.Ok(details);
    }
}