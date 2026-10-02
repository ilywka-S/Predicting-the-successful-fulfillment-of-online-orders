namespace OrderSense.Contracts;

public sealed class OrderFeatures
{
    public float ItemsCount 
    { 
        get; 
        set; 
    }
    public float TotalPrice 
    { 
        get; 
        set; 
    }
    public float FreightShare 
    { 
        get; 
        set; 
    }
    public float TotalWeightG 
    { 
        get; 
        set; 
    }
    public float PromisedDays 
    { 
        get; 
        set; 
    }
    public float DistanceKm 
    { 
        get; 
        set; 
    }
    public string CustomerState 
    { 
        get; 
        set; 
    } = "";
    public string SellerState 
    { 
        get; 
        set; 
    } = "";
    public string MainCategory 
    { 
        get; 
        set; 
    } = "";
    public string PaymentType 
    { 
        get; 
        set; 
    } = "";
    public float Installments 
    { 
        get; 
        set; 
    }
    public float PurchaseDayOfWeek 
    { 
        get; 
        set; 
    }
}