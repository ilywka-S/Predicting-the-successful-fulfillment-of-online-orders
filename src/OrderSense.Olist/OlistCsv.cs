using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace OrderSense.Olist;

public static class OlistCsv
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture);

    public static List<T> Read<T>(string folder, string fileName)
    {
        using var reader = new StreamReader(Path.Combine(folder, fileName));
        using var csv = new CsvReader(reader, Config);
        return csv.GetRecords<T>().ToList();
    }
}