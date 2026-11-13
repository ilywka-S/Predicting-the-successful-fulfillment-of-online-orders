using Microsoft.ML.Data;

namespace OrderSense.PredictionService;

public sealed class ModelOutput
{
    [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}

public sealed record ModelInfo(string Version);

public sealed class RiskThresholds
{
    public float MediumFrom { get; set; } = 0.09f;
    public float HighFrom { get; set; } = 0.14f;

    public string Level(float pProblem) =>
        pProblem >= HighFrom ? "high" : pProblem >= MediumFrom ? "medium" : "low";
}