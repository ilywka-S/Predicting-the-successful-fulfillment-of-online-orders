using CsvHelper.Configuration.Attributes;

namespace OrderSense.ML.Training;

public sealed class OrderItemRow
{
    [Name("order_id")]
    public string OrderId 
    { 
        get; 
        set; 
    } = "";

    [Name("order_item_id")]
    public int OrderItemId 
    { 
        get; 
        set; 
    }

    [Name("product_id")]
    public string ProductId 
    { 
        get; 
        set; 
    } = "";

    [Name("seller_id")]
    public string SellerId 
    { 
        get; 
        set; 
    } = "";

    [Name("shipping_limit_date")]
    public DateTime? ShippingLimitDate 
    { 
        get; 
        set; 
    }

    [Name("price")]
    public decimal Price 
    { 
        get; 
        set; 
    }

    [Name("freight_value")]
    public decimal FreightValue 
    { 
        get; 
        set; 
    }
}