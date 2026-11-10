using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using OrderSense.Contracts;
namespace OrderSense.Api.Predictions;
public sealed record ParsedRow(int RowNo, OrderFeatures? Features, string? Error);
public sealed class BatchCsvParseResult
{
    public List<string> FileErrors 
    { 
        get; 
    } = new List<string>();
    public List<ParsedRow> Rows 
    { 
        get; 
    } = new List<ParsedRow>();
}
public static class BatchCsv
{
    public const int MaxRows = 20000;
    public static readonly string[] Columns =
    [
        "itemsCount", "totalPrice", "freightShare", "totalWeightG", "promisedDays", "distanceKm",
        "customerState", "sellerState", "mainCategory", "paymentType", "installments", "purchaseDayOfWeek",
    ];
    public static string Header => string.Join(",", Columns);
    public static byte[] TemplateBytes()
    {
        string text = Header + "\r\n"
        + "1,120.50,0.15,800,20,450,SP,RJ,cama_mesa_banho,credit_card,1,2\r\n"
        + "2,310.00,0.22,,12,1800,AM,SP,informatica_acessorios,boleto,1,4\r\n";
        return [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(text)];
    }
    public static BatchCsvParseResult Parse(Stream stream)
    {
        BatchCsvParseResult result = new BatchCsvParseResult();
        string text;
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
        {
            text = reader.ReadToEnd();
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            result.FileErrors.Add("The file is empty.");

            return result;
        }
        char delimiter = DetectDelimiter(text);
        CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            PrepareHeaderForMatch = PrepareHeader,
            MissingFieldFound = null,
            HeaderValidated = null,
        };
        try
        {
            using StringReader stringReader = new StringReader(text);
            using CsvReader csv = new CsvReader(stringReader, config);

            ReadRows(csv, delimiter, result);
        }
        catch (CsvHelperException e)
        {
            string firstLine = e.Message.Split('\n')[0];
            result.FileErrors.Add($"The file is not a valid CSV: {firstLine}");
        }
        return result;
    }
    private static string PrepareHeader(PrepareHeaderForMatchArgs args)
    {
        return args.Header.Trim().ToLowerInvariant();
    }
    private static char DetectDelimiter(string text)
    {
        int end = text.IndexOf('\n');
        string firstLine = end < 0 ? text : text[..end];
        int commas = 0;
        int semicolons = 0;
        foreach (char c in firstLine)
        {
            if (c == ',')
            {
                commas++;
            }
            else if (c == ';')
            {
                semicolons++;
            }
        }
        return semicolons > commas ? ';' : ',';
    }
    private static void ReadRows(CsvReader csv, char delimiter, BatchCsvParseResult result)
    {
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            result.FileErrors.Add("The file has no header row.");

            return;
        }
        HashSet<string> headers = new HashSet<string>();
        foreach (string header in csv.HeaderRecord)
        {
            headers.Add(header.Trim().ToLowerInvariant());
        }
        List<string> missing = new List<string>();
        foreach (string column in Columns)
        {
            if (!headers.Contains(column.ToLowerInvariant()))
            {
                missing.Add(column);
            }
        }
        if (missing.Count > 0)
        {
            result.FileErrors.Add($"Missing columns: {string.Join(", ", missing)}.");
            return;
        }
        int rowNo = 0;
        while (csv.Read())
        {
            rowNo++;
            if (rowNo > MaxRows)
            {
                result.FileErrors.Add($"Too many rows: the limit is {MaxRows}.");

                return;
            }
            result.Rows.Add(ParseRow(csv, delimiter, rowNo));
        }
        if (result.Rows.Count == 0)
        {
            result.FileErrors.Add("The file has a header but no data rows.");
        }
    }
    private static ParsedRow ParseRow(CsvReader csv, char delimiter, int rowNo)
    {
        List<string> errors = new List<string>();
        OrderFeatures features = new OrderFeatures
        {
            ItemsCount = ReadNumber(csv, "itemsCount", delimiter, errors),
            TotalPrice = ReadNumber(csv, "totalPrice", delimiter, errors),
            FreightShare = ReadNumber(csv, "freightShare", delimiter, errors),
            TotalWeightG = ReadNumber(csv, "totalWeightG", delimiter, errors),
            PromisedDays = ReadNumber(csv, "promisedDays", delimiter, errors),
            DistanceKm = ReadNumber(csv, "distanceKm", delimiter, errors),
            Installments = ReadNumber(csv, "installments", delimiter, errors),
            PurchaseDayOfWeek = ReadNumber(csv, "purchaseDayOfWeek", delimiter, errors),
            CustomerState = ReadText(csv, "customerState", errors),
            SellerState = ReadText(csv, "sellerState", errors),
            MainCategory = ReadText(csv, "mainCategory", errors),
            PaymentType = ReadText(csv, "paymentType", errors),
        };
        errors.AddRange(BatchRowRules.Validate(features));
        if (errors.Count > 0)
        {
            return new ParsedRow(rowNo, null, string.Join("; ", errors));
        }
        return new ParsedRow(rowNo, features, null);
    }
    private static float ReadNumber(CsvReader csv, string column, char delimiter, List<string> errors)
    {
        string text = (csv.GetField(column.ToLowerInvariant()) ?? "").Trim();
        if (text.Length == 0 || text.Equals("nan", StringComparison.OrdinalIgnoreCase))
        {
            return float.NaN;
        }
        if (delimiter == ';')
        {
            text = text.Replace(',', '.');
        }
        bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
        if (!parsed || !float.IsFinite(value))
        {
            errors.Add($"{column}: '{text}' is not a number");
            return float.NaN;
        }
        return value;
    }
    private static string ReadText(CsvReader csv, string column, List<string> errors)
    {
        string text = (csv.GetField(column.ToLowerInvariant()) ?? "").Trim();
        if (text.Length > 100)
        {
            errors.Add($"{column}: the value is longer than 100 characters");
            return text[..100];
        }
        return text;
    }
}
public static class BatchRowRules
{
    public static List<string> Validate(OrderFeatures f)
    {
        List<string> errors = new List<string>();
        Required("itemsCount", f.ItemsCount, 1, 100000, errors);
        Required("totalPrice", f.TotalPrice, 0, 100000000, errors);
        Required("freightShare", f.FreightShare, 0, 1, errors);
        Optional("totalWeightG", f.TotalWeightG, 0, 100000000, errors);
        Optional("promisedDays", f.PromisedDays, -365, 3650, errors);
        Optional("distanceKm", f.DistanceKm, 0, 20000, errors);
        Optional("installments", f.Installments, 0, 100, errors);
        Optional("purchaseDayOfWeek", f.PurchaseDayOfWeek, 0, 6, errors);
        return errors;
    }
    private static void Required(string name, float value, float min, float max, List<string> errors)
    {
        if (!float.IsFinite(value) || value < min || value > max)
        {
            errors.Add($"{name} must be a number from {min} to {max}");
        }
    }
    private static void Optional(string name, float value, float min, float max, List<string> errors)
    {
        if (float.IsNaN(value))
        {
            return;
        }
        if (!float.IsFinite(value) || value < min || value > max)
        {
            errors.Add($"{name} must be empty or a number from {min} to {max}");
        }
    }
}