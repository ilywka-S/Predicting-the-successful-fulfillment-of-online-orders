namespace OrderSense.Contracts;

public sealed class FeatureContribution
{
    public string Feature { get; set; } = "";
    public float Contribution { get; set; }   // > 0 підвищує ризик проблеми
}

public sealed class OrderExplanation
{
    public string ModelVersion { get; set; } = "";
    public List<FeatureContribution> Factors { get; set; } = [];
}