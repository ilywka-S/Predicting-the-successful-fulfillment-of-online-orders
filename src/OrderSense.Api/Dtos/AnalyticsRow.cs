namespace OrderSense.Api.Dtos;
public sealed class AnalyticsRow
{
    public string Key 
    { 
        get; 
        set; 
    } = "";
    public int Orders 
    { 
        get; 
        set; 
    }
    public int Predicted 
    { 
        get; 
        set; 
    }
    public int Low 
    { 
        get; 
        set; 
    }
    public int Medium 
    { 
        get; 
        set; 
    }
    public int High 
    { 
        get; 
        set; 
    }
}