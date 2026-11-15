using System.Text.Json;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Predictions;

public static class BatchSummary
{
    public const int MaxFactors = 5;

    public static BatchJobSummaryDto Build(BatchJob job, IReadOnlyList<BatchResult> results, IReadOnlyDictionary<string, string> categoryNames, int top)
    {
        List<SummaryRow> rows = results.Select(ToRow).ToList();
        List<SummaryRow> predicted = rows.Where(r => r.Risk is not null).ToList();
        double? average = predicted.Count > 0 ? predicted.Average(r => (double)r.Probability!.Value) : null;

        return new BatchJobSummaryDto(
            job.Id,
            job.FileName,
            rows.Count,
            predicted.Count,
            predicted.Count(r => r.Risk == RiskLevel.Low),
            predicted.Count(r => r.Risk == RiskLevel.Medium),
            predicted.Count(r => r.Risk == RiskLevel.High),
            average,
            Group(rows, r => categoryNames.GetValueOrDefault(r.Category, r.Category), top),
            Group(rows, r => r.CustomerState, top),
            Group(rows, r => r.PaymentType, top),
            TopFactors(predicted));
    }

    private static List<AnalyticsPointDto> Group(List<SummaryRow> rows, Func<SummaryRow, string> key, int top)
    {
        return rows
            .GroupBy(key)
            .Select(g =>
            {
                int predicted = g.Count(r => r.Risk is not null);
                int medium = g.Count(r => r.Risk == RiskLevel.Medium);
                int high = g.Count(r => r.Risk == RiskLevel.High);
                double? share = predicted > 0 ? (double)(medium + high) / predicted : null;

                return new AnalyticsPointDto(g.Key, g.Count(), predicted, g.Count(r => r.Risk == RiskLevel.Low), medium, high, share);
            })
            .OrderByDescending(p => p.Orders)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .Take(top)
            .ToList();
    }

    private static List<BatchFactorCountDto> TopFactors(List<SummaryRow> predicted)
    {
        return predicted
            .Where(r => r.Risk is RiskLevel.Medium or RiskLevel.High)
            .Select(r => r.Factors.Where(f => f.Contribution > 0).MaxBy(f => f.Contribution))
            .OfType<RiskFactor>()
            .GroupBy(f => f.Feature)
            .Select(g => new BatchFactorCountDto(g.Key, RiskFactorMapping.Label(g.Key), g.Count()))
            .OrderByDescending(f => f.Rows)
            .ThenBy(f => f.Feature, StringComparer.Ordinal)
            .Take(MaxFactors)
            .ToList();
    }

    private static SummaryRow ToRow(BatchResult result)
    {
        JsonElement? input = ParseInput(result.Input);
        RiskLevel? risk = result.Probability is null ? null : result.RiskLevel;

        return new SummaryRow(
            Text(input, "MainCategory") ?? string.Empty,
            Text(input, "CustomerState") ?? string.Empty,
            Text(input, "PaymentType") ?? string.Empty,
            result.Probability,
            risk,
            result.Factors);
    }

    private static JsonElement? ParseInput(string input)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(input);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement? root, string name)
    {
        return root is { ValueKind: JsonValueKind.Object } element
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : null;
    }

    private sealed record SummaryRow(
        string Category,
        string CustomerState,
        string PaymentType,
        float? Probability,
        RiskLevel? Risk,
        List<RiskFactor> Factors);
}
