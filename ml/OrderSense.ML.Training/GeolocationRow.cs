using CsvHelper.Configuration.Attributes;

namespace OrderSense.ML.Training;

public sealed class GeolocationRow
{
    [Name("geolocation_zip_code_prefix")]
    public string ZipCodePrefix 
    { 
        get; 
        set; 
    } = "";

    [Name("geolocation_lat")]
    public double Lat 
    { 
        get; 
        set; 
    }

    [Name("geolocation_lng")]
    public double Lng 
    { 
        get; 
        set; 
    }
}