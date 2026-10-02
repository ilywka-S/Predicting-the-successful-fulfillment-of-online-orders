using CsvHelper.Configuration.Attributes;

namespace OrderSense.ML.Training;

public sealed class CustomerRow
{
    [Name("customer_id")]
    public string CustomerId 
    { 
        get; 
        set; 
    } = "";

    [Name("customer_zip_code_prefix")]
    public string CustomerZipCodePrefix 
    { 
        get; 
        set; 
    } = "";

    [Name("customer_state")]
    public string CustomerState 
    { 
        get; 
        set; 
    } = "";
}