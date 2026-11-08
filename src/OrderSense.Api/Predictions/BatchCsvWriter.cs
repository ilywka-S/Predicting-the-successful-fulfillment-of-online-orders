using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Predictions;

public static class BatchCsvWriter
{
    private const char Separator = ';';

    public static string Write(IReadOnlyList<BatchResult> results)
    {
        var inputs = results.Select(r => JsonNode.Parse(r.Input) as JsonObject ?? []).ToList();
        var columns = inputs.SelectMany(i => i.Select(p => p.Key)).Distinct().ToList();

        var csv = new StringBuilder();
        AppendRow(csv, ["row_no", .. columns, "probability", "risk_level", "factors"]);

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var factors = BatchJobMapping.TopFactors(result).Select(f => RiskFactorMapping.ToDto(f).Label);

            AppendRow(csv, [
                result.RowNo.ToString(CultureInfo.InvariantCulture),
                .. columns.Select(c => inputs[i][c]?.ToString() ?? ""),
                result.Probability?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                result.RiskLevel?.ToString().ToLowerInvariant() ?? "",
                string.Join(", ", factors),
            ]);
        }

        return csv.ToString();
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> values)
    {
        csv.AppendJoin(Separator, values.Select(Escape)).Append("\r\n");
    }

    private static string Escape(string value) => value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}