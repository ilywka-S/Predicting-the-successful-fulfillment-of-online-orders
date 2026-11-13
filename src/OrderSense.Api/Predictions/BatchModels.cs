namespace OrderSense.Api.Predictions;
public sealed class BatchRunResult
{
    public int Predicted 
    { 
        get; 
        set; 
    }
    public int Skipped 
    { 
        get; 
        set; 
    }
    public int Batches 
    { 
        get; 
        set; 
    }
}
public sealed class BatchOrder
{
    public string Id 
    { 
        get; 
        set; 
    } = "";
    public DateTime PurchasedAt 
    { 
        get; 
        set; 
    }
    public DateTime EstimatedDeliveryAt 
    { 
        get; 
        set; 
    }
    public string CustomerState 
    { 
        get; 
        set; 
    } = "";
    public string CustomerZip 
    { 
        get; 
        set; 
    } = "";
    public List<BatchItem> Items 
    { 
        get; 
        set; 
    } = [];
    public List<BatchPayment> Payments 
    { 
        get; 
        set; 
    } = [];
}
public sealed class BatchItem
{
    public decimal Price 
    { 
        get; 
        set; 
    }
    public decimal FreightValue 
    { 
        get; 
        set; 
    }
    public string? Category 
    { 
        get; 
        set; 
    }
    public int? WeightG 
    { 
        get; 
        set; 
    }
    public string SellerId 
    { 
        get; 
        set; 
    } = "";
    public string SellerState 
    { 
        get; 
        set; 
    } = "";
    public string SellerZip 
    { 
        get; 
        set; 
    } = "";
}
public sealed class BatchPayment
{
    public string Type 
    { 
        get; 
        set; 
    } = "";
    public int Installments 
    { 
        get; 
        set; 
    }
    public decimal Value 
    { 
        get; 
        set; 
    }
}