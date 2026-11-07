namespace OrderSense.Contracts;

public sealed class FeatureImportance
{
    public string Feature { get; set; } = "";

    public float Importance { get; set; }
}

public sealed class ModelDetails
{
    public string Version { get; set; } = "";

    public DateTimeOffset TrainedAt { get; set; }

    public int TrainRows { get; set; }

    public int TestRows { get; set; }

    public string TrainPeriod { get; set; } = "";

    public string TestPeriod { get; set; } = "";

    public float RocAuc { get; set; }

    public float PrAuc { get; set; }

    public float F1 { get; set; }

    public float BaselinePrAuc 
    { 
        get; 
        set; 
    }

    public float RiskMediumFrom 
    { 
        get; 
        set; 
    }

    public float RiskHighFrom 
    { 
        get; 
        set; 
    }

    public List<FeatureImportance> FeatureImportances 
    { 
        get; 
        set; 
    } = [];
}