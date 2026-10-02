namespace OrderSense.ML.Training;

public sealed class OrderCard
{
    public string OrderId 
    { 
        get; 
        set; 
    } = "";

    public int ItemsCount 
    { 
        get; 
        set; 
    }
    public decimal TotalPrice 
    { 
        get; 
        set; 
    }
    public decimal TotalFreight 
    { 
        get; 
        set; 
    }
    public double FreightShare 
    { 
        get; 
        set; 
    }

    public int SellersCount 
    { 
        get; 
        set; 
    }
    public string? MainCategory 
    { 
        get; 
        set; 
    }
    public float? TotalWeightG 
    { 
        get; 
        set; 
    }
    public float? TotalVolumeCm3 
    { 
        get; 
        set; 
    }

    public string? CustomerState 
    { 
        get; 
        set; 
    }
    public string? SellerState 
    { 
        get; 
        set; 
    }

    public string? MainPaymentType 
    { 
        get; 
        set; 
    }
    public int PaymentsCount 
    { 
        get; 
        set; 
    }
    public int? PaymentInstallments 
    { 
        get; 
        set; 
    }

    public double? PromisedDays 
    { 
        get; 
        set; 
    }
    public int PurchaseDayOfWeek 
    { 
        get; 
        set; 
    }
    public int PurchaseHour 
    { 
        get; 
        set; 
    }
    public double? DistanceKm 
    { 
        get; 
        set; 
    }

    public string? Label { get; set; } // "success" / "problem" / null (не враховуємо)
}