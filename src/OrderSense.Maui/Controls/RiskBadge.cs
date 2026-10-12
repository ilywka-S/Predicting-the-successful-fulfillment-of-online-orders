namespace OrderSense.Maui.Controls;

public class RiskBadge : ContentView
{
    public static readonly BindableProperty LevelProperty = BindableProperty.Create(
        nameof(Level), typeof(object), typeof(RiskBadge),
        propertyChanged: (bindable, _, _) => ((RiskBadge)bindable).Update());

    public static readonly BindableProperty CompactProperty = BindableProperty.Create(
        nameof(Compact), typeof(bool), typeof(RiskBadge), false,
        propertyChanged: (bindable, _, _) => ((RiskBadge)bindable).Update());

    private readonly Border _frame;
    private readonly Label _icon;
    private readonly Label _text;

    public RiskBadge()
    {
        _icon = new Label();
        _text = new Label();
        _frame = new Border
        {
            Content = new HorizontalStackLayout { Spacing = 4, Children = { _icon, _text } },
        };

        Content = _frame;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;
        Update();
    }

    public object? Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private void Update()
    {
        var level = Risk.Normalize(Level);
        var style = level switch
        {
            Risk.Low => "BadgeRiskLow",
            Risk.Medium => "BadgeRiskMedium",
            Risk.High => "BadgeRiskHigh",
            _ => "BadgeOutline",
        };

        _frame.SetDynamicResource(StyleProperty, style);
        _text.SetDynamicResource(StyleProperty, style + "Text");
        _icon.SetDynamicResource(StyleProperty, style + "Icon");

        _icon.Text = Risk.Icon(level);
        _text.Text = Compact ? Risk.ShortLabel(level) : Risk.Label(level);
        SemanticProperties.SetDescription(this, Risk.Label(level));
    }
}
