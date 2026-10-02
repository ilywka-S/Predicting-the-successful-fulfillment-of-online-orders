namespace OrderSense.Contracts;

public sealed class OrderPrediction
{
    public float Probability 
    { 
        get; 
        set; 
    }
    public string RiskLevel 
    { 
        get; 
        set; 
    } = ""; // "low" / "medium" / "high"
}