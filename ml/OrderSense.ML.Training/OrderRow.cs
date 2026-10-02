using CsvHelper.Configuration.Attributes;

namespace OrderSense.ML.Training;

public sealed class OrderRow
{
    [Name("order_id")]
    public string OrderId 
    { 
        get; 
        set; 
    } = "";

    [Name("customer_id")]
    public string CustomerId 
    { 
        get; 
        set; 
    } = "";

    [Name("order_status")]
    public string OrderStatus 
    { 
        get; 
        set; 
    } = "";

    [Name("order_purchase_timestamp")]
    public DateTime? OrderPurchaseTimestamp 
    { 
        get; 
        set; 
    }

    [Name("order_approved_at")]
    public DateTime? OrderApprovedAt 
    { 
        get; 
        set; 
    }

    [Name("order_delivered_carrier_date")]
    public DateTime? OrderDeliveredCarrierDate 
    { 
        get; 
        set; 
    }

    [Name("order_delivered_customer_date")]
    public DateTime? OrderDeliveredCustomerDate 
    { 
        get; 
        set; 
    }

    [Name("order_estimated_delivery_date")]
    public DateTime? OrderEstimatedDeliveryDate 
    { 
        get; 
        set; 
    }
}