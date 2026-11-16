using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OrderSense.Client.Core;
using SkiaSharp;

namespace OrderSense.Maui.Controls;

public static class ModelCharts
{
    private static readonly SKColor Accent = SKColor.Parse("#F54A00");
    private static readonly SKColor Muted = SKColor.Parse("#8A8A8A");
    private static readonly SKColor Grid = SKColor.Parse("#8A8A8A").WithAlpha(48);

    public static ISeries[] ImportanceSeries(IReadOnlyList<FeatureImportanceDto> importances)
    {
        var rows = Rows(importances);

        return
        [
            Series("Допомагає прогнозу", rows.Select(f => f.Importance > 0 ? (double?)f.Importance : null), Accent),
            Series("Не допомагає на перевірці", rows.Select(f => f.Importance <= 0 ? (double?)f.Importance : null), Muted),
        ];
    }

    public static ICartesianAxis[] LabelAxes(IReadOnlyList<FeatureImportanceDto> importances) =>
    [
        new Axis
        {
            Labels = Rows(importances).Select(f => Formats.Feature(f.Feature)).ToArray(),
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(Muted),
            SeparatorsPaint = null,
            MinStep = 1,
            ForceStepToMin = true,
        },
    ];

    public static ICartesianAxis[] ValueAxes(IReadOnlyList<FeatureImportanceDto> importances) =>
    [
        new Axis
        {
            Name = "Падіння PR-AUC без ознаки",
            NamePaint = new SolidColorPaint(Muted),
            NameTextSize = 11,
            MinLimit = ValueLimit(importances, -1),
            MaxLimit = ValueLimit(importances, 1),
            MinStep = ValueStep(importances),
            Labeler = value => value.ToString("0.000", CultureInfo.InvariantCulture),
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(Muted),
            SeparatorsPaint = new SolidColorPaint(Grid, 1),
        },
    ];

    public static string Importance(double value) =>
        (value > 0 ? "+" : string.Empty) + value.ToString("0.0000", CultureInfo.InvariantCulture);

    public static double? ValueLimit(IReadOnlyList<FeatureImportanceDto> importances, int side)
    {
        var (min, max) = Range(importances);
        if (max == min)
        {
            return null;
        }

        var edge = side < 0 ? min : max;

        return edge == 0 ? 0 : edge + (side * (max - min) * 0.15);
    }

    public static double ValueStep(IReadOnlyList<FeatureImportanceDto> importances)
    {
        var (min, max) = Range(importances);
        if (max == min)
        {
            return 0;
        }

        var raw = (max - min) / 3;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var residual = raw / magnitude;

        return magnitude * (residual > 5 ? 10 : residual > 2 ? 5 : residual > 1 ? 2 : 1);
    }

    private static (double Min, double Max) Range(IReadOnlyList<FeatureImportanceDto> importances) =>
        importances.Count == 0
            ? (0, 0)
            : (Math.Min(0, importances.Min(f => (double)f.Importance)), Math.Max(0, importances.Max(f => (double)f.Importance)));

    private static List<FeatureImportanceDto> Rows(IReadOnlyList<FeatureImportanceDto> importances) =>
        importances.Reverse().ToList();

    private static RowSeries<double?> Series(string name, IEnumerable<double?> values, SKColor color) => new()
    {
        Name = name,
        Values = values.ToArray(),
        Fill = new SolidColorPaint(color),
        Stroke = null,
        MaxBarWidth = 16,
        Padding = 4,
        IgnoresBarPosition = true,
        XToolTipLabelFormatter = point => Importance(point.Coordinate.PrimaryValue),
        DataLabelsPaint = new SolidColorPaint(Muted),
        DataLabelsSize = 11,
        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
        DataLabelsFormatter = point => Importance(point.Coordinate.PrimaryValue),
    };
}

public class ImportanceSeriesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ModelCharts.ImportanceSeries(value as IReadOnlyList<FeatureImportanceDto> ?? []);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class ImportanceAxesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ModelCharts.LabelAxes(value as IReadOnlyList<FeatureImportanceDto> ?? []);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class ImportanceValueAxesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ModelCharts.ValueAxes(value as IReadOnlyList<FeatureImportanceDto> ?? []);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
