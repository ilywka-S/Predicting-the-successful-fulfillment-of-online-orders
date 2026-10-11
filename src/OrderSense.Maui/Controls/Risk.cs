namespace OrderSense.Maui.Controls;

public static class Risk
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static string? Normalize(object? level) =>
        level?.ToString()?.Trim().ToLowerInvariant() switch
        {
            Low => Low,
            Medium => Medium,
            High => High,
            _ => null,
        };

    public static string Label(string? level) => level switch
    {
        Low => "Низький ризик",
        Medium => "Середній ризик",
        High => "Високий ризик",
        _ => "Без прогнозу",
    };

    public static string ShortLabel(string? level) => level switch
    {
        Low => "Низький",
        Medium => "Середній",
        High => "Високий",
        _ => "Без прогнозу",
    };

    public static string Icon(string? level) => level switch
    {
        Low => Icons.CircleCheck,
        Medium => Icons.TriangleAlert,
        High => Icons.OctagonAlert,
        _ => Icons.CircleDashed,
    };
}
