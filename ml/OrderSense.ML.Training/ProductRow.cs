using CsvHelper.Configuration.Attributes;

namespace OrderSense.ML.Training;

public sealed class ProductRow
{
    [Name("product_id")]
    public string ProductId { get; set; } = "";

    [Name("product_category_name")]
    public string? ProductCategoryName 
    { 
        get; 
        set; 
    }

    [Name("product_weight_g")]
    public float? ProductWeightG 
    { 
        get; 
        set; 
    }

    [Name("product_length_cm")]
    public float? ProductLengthCm 
    { 
        get; 
        set; 
    }

    [Name("product_height_cm")]
    public float? ProductHeightCm 
    { 
        get; 
        set; 
    }

    [Name("product_width_cm")]
    public float? ProductWidthCm 
    { 
        get; 
        set; 
    }
}