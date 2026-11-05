namespace OrderSense.Maui.Controls;

public partial class DateField : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(DateField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(DateField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty DateProperty = BindableProperty.Create(
        nameof(Date), typeof(DateTime?), typeof(DateField), defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnChromeChanged);

    public DateField()
    {
        InitializeComponent();
        UpdateChrome();
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Placeholder
    {
        get => (string?)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public DateTime? Date
    {
        get => (DateTime?)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((DateField)bindable).UpdateChrome();

    private void OnInputFocusChanged(object? sender, FocusEventArgs e) =>
        VisualStateManager.GoToState(InputBorder, e.IsFocused ? "Focused" : "Normal");

    private void OnClearTapped(object? sender, TappedEventArgs e) => Date = null;

    private void UpdateChrome()
    {
        LabelView.IsVisible = !string.IsNullOrEmpty(Label);
        PlaceholderView.IsVisible = Date is null;
        ClearButton.IsVisible = Date is not null;
        SemanticProperties.SetDescription(Input, Label ?? Placeholder);
        SemanticProperties.SetDescription(ClearButton, "Очистити дату");
    }
}
