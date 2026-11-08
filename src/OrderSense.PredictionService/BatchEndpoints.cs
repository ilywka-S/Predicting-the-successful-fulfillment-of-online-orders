using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;
using OrderSense.Contracts;
namespace OrderSense.PredictionService;
public static class BatchEndpoints
{
    public const int MaxBatchSize = 1000;
    public static IResult Predict(List<OrderFeatures> batch,
        PredictionEnginePool<OrderFeatures, ModelOutput> pool,
        IOptions<RiskThresholds> risk,
        ModelInfo model)
    {
        if (batch.Count > MaxBatchSize)
        {
            Dictionary<string, string[]> sizeErrors = new Dictionary<string, string[]>
            {
                ["batch"] = [$"Не більше {MaxBatchSize} елементів за запит."],
            };
            return Results.ValidationProblem(sizeErrors);
        }
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        for (int i = 0; i < batch.Count; i++)
        {
            Dictionary<string, string[]> itemErrors = PredictValidator.Validate(batch[i]);
            foreach (KeyValuePair<string, string[]> error in itemErrors)
            {
                errors[$"[{i}].{error.Key}"] = error.Value;
            }
        }
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }
        List<OrderPrediction> result = new List<OrderPrediction>(batch.Count);
        for (int i = 0; i < batch.Count; i++)
        {
            float pProblem = pool.Predict(model.Version, batch[i]).Probability;
            result.Add(new OrderPrediction
            {
                Probability = 1f - pProblem,
                RiskLevel = risk.Value.Level(pProblem),
                ModelVersion = model.Version,
            });
        }
        return Results.Ok(result);
    }
}