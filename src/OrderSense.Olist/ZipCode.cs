namespace OrderSense.Olist;
public static class ZipCode
{
    public static string Normalize(string? zip)
    {
        string value = (zip ?? "").Trim();

        return value.PadLeft(5, '0');
    }
}