using CsvHelper.Configuration.Attributes;

namespace OrderSense.ML.Training;

public sealed class SellerRow
{
    [Name("seller_id")]
    public string SellerId 
    { 
        get; 
        set; 
    } = "";

    [Name("seller_zip_code_prefix")]
    public string SellerZipCodePrefix 
    { 
        get; 
        set; 
    } = "";

    [Name("seller_state")]
    public string SellerState 
    { 
        get; 
        set; 
    } = "";
}