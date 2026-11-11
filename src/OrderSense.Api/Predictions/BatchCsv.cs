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
    private const int MaxHeaderChars = 10000;
    private const int MaxFieldChars = 10000;
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
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
        string? headerLine = ReadHeaderLine(reader, out bool tooLong);
        if (tooLong)
        {
            result.FileErrors.Add("The header row is too long.");

            return result;
        }
        if (headerLine is null)
        {
            result.FileErrors.Add("The file is empty.");

            return result;
        }
        char delimiter = DetectDelimiter(headerLine);
        CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = false,
            MissingFieldFound = null,
            MaxFieldSize = MaxFieldChars,
        };
        try
        {
            Dictionary<string, int>? columns = ReadHeader(headerLine, config, result);
            if (columns is null)
            {
                return result;
            }
            using CsvReader csv = new CsvReader(reader, config);

            ReadRows(csv, columns, delimiter, result);
        }
        catch (CsvHelperException e)
        {
            string firstLine = e.Message.Split('\n')[0];
            result.FileErrors.Add($"The file is not a valid CSV: {firstLine}");
        }
        return result;
    }
    private static string? ReadHeaderLine(StreamReader reader, out bool tooLong)
    {
        tooLong = false;
        StringBuilder line = new StringBuilder();
        while (true)
        {
            int c = reader.Read();
            if (c < 0)
            {
                string last = line.ToString().TrimEnd('\r');

                return string.IsNullOrWhiteSpace(last) ? null : last;
            }
            if (c == '\n')
            {
                string text = line.ToString().TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(text))
                {
                    line.Clear();

                    continue;
                }
                return text;
            }
            line.Append((char)c);
            if (line.Length > MaxHeaderChars)
            {
                tooLong = true;

                return null;
            }
        }
    }
    private static Dictionary<string, int>? ReadHeader(string headerLine, CsvConfiguration config, BatchCsvParseResult result)
    {
        using StringReader headerReader = new StringReader(headerLine);
        using CsvParser parser = new CsvParser(headerReader, config);
        if (!parser.Read() || parser.Record is null)
        {
            result.FileErrors.Add("The file has no header row.");

            return null;
        }
        Dictionary<string, int> columns = new Dictionary<string, int>();
        for (int i = 0; i < parser.Record.Length; i++)
        {
            columns.TryAdd(parser.Record[i].Trim().ToLowerInvariant(), i);
        }
        List<string> missing = new List<string>();
        foreach (string column in Columns)
        {
            if (!columns.ContainsKey(column.ToLowerInvariant()))
            {
                missing.Add(column);
            }
        }
        if (missing.Count > 0)
        {
            result.FileErrors.Add($"Missing columns: {string.Join(", ", missing)}.");

            return null;
        }
        return columns;
    }
    private static char DetectDelimiter(string firstLine)
    {
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
    private static void ReadRows(CsvReader csv, Dictionary<string, int> columns, char delimiter, BatchCsvParseResult result)
    {
        int rowNo = 0;
        while (csv.Read())
        {
            rowNo++;
            if (rowNo > MaxRows)
            {
                result.FileErrors.Add($"Too many rows: the limit is {MaxRows}.");

                return;
            }
            result.Rows.Add(ParseRow(csv, columns, delimiter, rowNo));
        }
        if (result.Rows.Count == 0)
        {
            result.FileErrors.Add("The file has a header but no data rows.");
        }
    }
    private static ParsedRow ParseRow(CsvReader csv, Dictionary<string, int> columns, char delimiter, int rowNo)
    {
        List<string> errors = new List<string>();
        OrderFeatures features = new OrderFeatures
        {
            ItemsCount = ReadNumber(csv, columns, "itemsCount", delimiter, errors),
            TotalPrice = ReadNumber(csv, columns, "totalPrice", delimiter, errors),
            FreightShare = ReadNumber(csv, columns, "freightShare", delimiter, errors),
            TotalWeightG = ReadNumber(csv, columns, "totalWeightG", delimiter, errors),
            PromisedDays = ReadNumber(csv, columns, "promisedDays", delimiter, errors),
            DistanceKm = ReadNumber(csv, columns, "distanceKm", delimiter, errors),
            Installments = ReadNumber(csv, columns, "installments", delimiter, errors),
            PurchaseDayOfWeek = ReadNumber(csv, columns, "purchaseDayOfWeek", delimiter, errors),
            CustomerState = ReadText(csv, columns, "customerState", errors),
            SellerState = ReadText(csv, columns, "sellerState", errors),
            MainCategory = ReadText(csv, columns, "mainCategory", errors),
            PaymentType = ReadText(csv, columns, "paymentType", errors),
        };
        errors.AddRange(BatchRowRules.Validate(features));
        if (errors.Count > 0)
        {
            return new ParsedRow(rowNo, null, string.Join("; ", errors));
        }
        return new ParsedRow(rowNo, features, null);
    }
    private static string Field(CsvReader csv, Dictionary<string, int> columns, string column)
    {
        int index = columns[column.ToLowerInvariant()];
        if (index >= csv.Parser.Count)
        {
            return "";
        }
        return (csv.GetField(index) ?? "").Trim();
    }
    private static float ReadNumber(CsvReader csv, Dictionary<string, int> columns, string column, char delimiter, List<string> errors)
    {
        string text = Field(csv, columns, column);
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
    private static string ReadText(CsvReader csv, Dictionary<string, int> columns, string column, List<string> errors)
    {
        string text = Field(csv, columns, column);
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