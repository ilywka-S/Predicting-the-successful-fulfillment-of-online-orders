using CsvHelper.Configuration.Attributes;

namespace OrderSense.Olist;

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
    [Name("seller_city")] 
    public string SellerCity 
    { 
        get; 
        set; 
    } = "";
}