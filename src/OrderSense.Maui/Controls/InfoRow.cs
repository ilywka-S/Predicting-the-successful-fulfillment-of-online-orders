namespace OrderSense.Maui.Controls;

public class InfoRow : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(InfoRow),
        propertyChanged: (bindable, _, value) => ((InfoRow)bindable)._icon.Text = (string?)value);

    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(InfoRow),
        propertyChanged: (bindable, _, value) => ((InfoRow)bindable)._label.Text = (string?)value);

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(InfoRow),
        propertyChanged: (bindable, _, value) => ((InfoRow)bindable)._value.Text = (string?)value);

    private readonly Label _icon = new();
    private readonly Label _label = new();
    private readonly Label _value = new() { HorizontalTextAlignment = TextAlignment.End };

    public InfoRow()
    {
        _icon.SetDynamicResource(StyleProperty, "IconMuted");
        _label.SetDynamicResource(StyleProperty, "TextMuted");
        _value.SetDynamicResource(StyleProperty, "TextMedium");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 10,
            Padding = new Thickness(0, 6),
        };
        grid.Add(_icon, 0);
        grid.Add(_label, 1);
        grid.Add(_value, 2);
        Content = grid;
    }

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
