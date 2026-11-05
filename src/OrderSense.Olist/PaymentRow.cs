using CsvHelper.Configuration.Attributes;

namespace OrderSense.Olist;

public sealed class PaymentRow
{
    [Name("order_id")]
    public string OrderId 
    { 
        get; 
        set; 
    } = "";

    [Name("payment_type")]
    public string PaymentType 
    { 
        get; 
        set; 
    } = "";

    [Name("payment_installments")]
    public int PaymentInstallments 
    { 
        get; 
        set; 
    }

    [Name("payment_value")]
    public decimal PaymentValue 
    { 
        get; 
        set; 
    }
}