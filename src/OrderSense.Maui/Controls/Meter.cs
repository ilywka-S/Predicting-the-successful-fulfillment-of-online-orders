namespace OrderSense.Maui.Controls;

public class Meter : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(Meter), 0d,
        propertyChanged: (bindable, _, _) => ((Meter)bindable).UpdateValue());

    public static readonly BindableProperty LevelProperty = BindableProperty.Create(
        nameof(Level), typeof(object), typeof(Meter),
        propertyChanged: (bindable, _, _) => ((Meter)bindable).UpdateLevel());

    private readonly Grid _grid;
    private readonly Border _fill;

    public Meter()
    {
        _fill = new Border();
        _grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
            },
            Children = { _fill },
        };

        var track = new Border { Content = _grid };
        track.SetDynamicResource(StyleProperty, "MeterTrack");
        Content = track;

        UpdateValue();
        UpdateLevel();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public object? Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    private void UpdateValue()
    {
        var value = double.IsNaN(Value) ? 0 : Math.Clamp(Value, 0, 1);
        _grid.ColumnDefinitions[0].Width = new GridLength(value, GridUnitType.Star);
        _grid.ColumnDefinitions[1].Width = new GridLength(1 - value, GridUnitType.Star);
        SemanticProperties.SetDescription(this, Formats.Percent(value));
    }

    private void UpdateLevel()
    {
        var style = Risk.Normalize(Level) switch
        {
            Risk.Low => "MeterFillLow",
            Risk.Medium => "MeterFillMedium",
            Risk.High => "MeterFillHigh",
            _ => "MeterFill",
        };
        _fill.SetDynamicResource(StyleProperty, style);
    }
}
