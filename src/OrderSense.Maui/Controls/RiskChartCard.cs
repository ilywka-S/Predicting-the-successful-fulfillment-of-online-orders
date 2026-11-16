using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Maui;
using OrderSense.Client.Core;

namespace OrderSense.Maui.Controls;

public class RiskChartCard : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(RiskChartCard),
        propertyChanged: (bindable, _, value) => ((RiskChartCard)bindable)._title.Text = (string?)value);

    public static readonly BindableProperty PointsProperty = BindableProperty.Create(
        nameof(Points), typeof(IReadOnlyList<AnalyticsPointDto>), typeof(RiskChartCard),
        propertyChanged: (bindable, _, _) => ((RiskChartCard)bindable).UpdateChart());

    public static readonly BindableProperty DimensionProperty = BindableProperty.Create(
        nameof(Dimension), typeof(string), typeof(RiskChartCard), "category",
        propertyChanged: (bindable, _, _) => ((RiskChartCard)bindable).UpdateChart());

    private readonly Label _title = new();
    private readonly CartesianChart _chart = new() { HeightRequest = 340, TooltipPosition = TooltipPosition.Right };

    public RiskChartCard()
    {
        _title.SetDynamicResource(StyleProperty, "TextH3");

        var card = new Border
        {
            Padding = DeviceInfo.Idiom == DeviceIdiom.Phone ? new Thickness(12, 16) : new Thickness(20),
            Content = new VerticalStackLayout { Spacing = 12, Children = { _title, _chart } },
        };
        card.SetDynamicResource(StyleProperty, "Card");
        Content = card;

        UpdateChart();
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IReadOnlyList<AnalyticsPointDto>? Points
    {
        get => (IReadOnlyList<AnalyticsPointDto>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string Dimension
    {
        get => (string)GetValue(DimensionProperty);
        set => SetValue(DimensionProperty, value);
    }

    private void UpdateChart()
    {
        var points = Points ?? [];

        _chart.Series = RiskCharts.RowSeries(points);
        _chart.YAxes = RiskCharts.LabelAxes(points, Dimension);
        _chart.XAxes = RiskCharts.PercentAxes(points, rows: true);
    }
}
