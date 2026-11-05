using CsvHelper.Configuration.Attributes;

namespace OrderSense.Olist;

public sealed class CategoryTranslationRow
{
    [Name("product_category_name")]
    public string Category 
    { 
        get; 
        set; 
    } = "";

    [Name("product_category_name_english")]
    public string CategoryEn 
    { 
        get; 
        set; 
    } = "";
}