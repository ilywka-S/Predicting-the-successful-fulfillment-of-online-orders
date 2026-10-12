using OrderSense.Contracts;
namespace OrderSense.ML.Training;
public sealed class TrainingRow
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
        get; set;
    }
    public bool Label 
    { 
        get; 
        set; 
    }            
    public DateTime PurchasedAt 
    { 
        get; 
        set;
    }  // лише для поділу за часом

    public bool IsProblem 
    { 
        get; 
        set; 
    }        // true = проблема (для навчання й метрик)

    public static TrainingRow From(OrderFeatures f, bool isSuccess, DateTime purchasedAt) => new()
    {
        ItemsCount = f.ItemsCount,
        TotalPrice = f.TotalPrice,
        FreightShare = f.FreightShare,
        TotalWeightG = f.TotalWeightG,
        PromisedDays = f.PromisedDays,
        DistanceKm = f.DistanceKm,
        CustomerState = f.CustomerState,
        SellerState = f.SellerState,
        MainCategory = f.MainCategory,
        PaymentType = f.PaymentType,
        Installments = f.Installments,
        PurchaseDayOfWeek = f.PurchaseDayOfWeek,
        Label = isSuccess,
        IsProblem = !isSuccess,
        PurchasedAt = purchasedAt
    };
}