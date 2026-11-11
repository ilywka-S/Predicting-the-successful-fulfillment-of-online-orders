using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OrderSense.Client.Core;
using SkiaSharp;

namespace OrderSense.Maui.Controls;

public static class RiskCharts
{
    private static readonly SKColor Accent = SKColor.Parse("#F54A00");
    private static readonly SKColor Muted = SKColor.Parse("#8A8A8A");
    private static readonly SKColor Grid = SKColor.Parse("#8A8A8A").WithAlpha(48);

    public static ISeries[] DaySeries(IReadOnlyList<AnalyticsPointDto> points) =>
    [
        new LineSeries<double?>
        {
            Name = "Ризикові",
            Values = points.Select(Share).ToArray(),
            Stroke = new SolidColorPaint(Accent, 2),
            Fill = new SolidColorPaint(Accent.WithAlpha(40)),
            GeometrySize = 0,
            GeometryFill = null,
            GeometryStroke = null,
            LineSmoothness = 0.3,
            YToolTipLabelFormatter = point => Percent(point.Coordinate.PrimaryValue),
        },
    ];

    public static ISeries[] RowSeries(IReadOnlyList<AnalyticsPointDto> points) =>
    [
        new RowSeries<double?>
        {
            Name = "Ризикові",
            Values = points.Reverse().Select(Share).ToArray(),
            Fill = new SolidColorPaint(Accent),
            Stroke = null,
            MaxBarWidth = 18,
            Padding = 4,
            XToolTipLabelFormatter = point => Percent(point.Coordinate.PrimaryValue),
            DataLabelsPaint = new SolidColorPaint(Muted),
            DataLabelsSize = 11,
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
            DataLabelsFormatter = point => Percent(point.Coordinate.PrimaryValue),
        },
    ];

    public static ICartesianAxis[] LabelAxes(IReadOnlyList<AnalyticsPointDto> points, string dimension)
    {
        var labels = points.Select(point => Label(point.Key, dimension));
        if (dimension != "day")
        {
            labels = labels.Reverse();
        }

        return
        [
            new Axis
            {
                Labels = labels.ToArray(),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted),
                SeparatorsPaint = null,
                MinStep = 1,
            },
        ];
    }

    public static ICartesianAxis[] PercentAxes() =>
    [
        new Axis
        {
            MinLimit = 0,
            Labeler = Percent,
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(Muted),
            SeparatorsPaint = new SolidColorPaint(Grid, 1),
        },
    ];

    public static string Label(string? key, string dimension) => dimension switch
    {
        "day" when DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            => day.ToString("dd.MM", CultureInfo.InvariantCulture),
        "category" => Formats.Category(key),
        "seller" => Formats.ShortId(key),
        _ => key ?? "—",
    };

    private static double? Share(AnalyticsPointDto point) => point.RiskyShare * 100;

    private static string Percent(double value) => value.ToString("0", CultureInfo.InvariantCulture) + " %";

    private static string Percent(double? value) => value is { } number ? Percent(number) : "—";
}

public class RiskSeriesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var points = value as IReadOnlyList<AnalyticsPointDto> ?? [];
        return parameter as string == "rows" ? RiskCharts.RowSeries(points) : RiskCharts.DaySeries(points);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class RiskLabelAxesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        RiskCharts.LabelAxes(value as IReadOnlyList<AnalyticsPointDto> ?? [], parameter as string ?? "day");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class PercentAxesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        RiskCharts.PercentAxes();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
