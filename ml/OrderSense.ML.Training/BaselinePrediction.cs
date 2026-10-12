namespace OrderSense.ML.Training;

public sealed class BaselinePrediction
{
    public bool IsProblem 
    { 
        get; 
        set; 
    }
    public float Score 
    { 
        get; 
        set; 
    }
    public float Probability 
    { 
        get; 
        set; 
    }
    public bool PredictedLabel 
    { 
        get; 
        set; 
    }
}