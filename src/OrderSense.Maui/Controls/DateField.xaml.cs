namespace OrderSense.Maui.Controls;

public partial class DateField : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(DateField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(DateField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty DateProperty = BindableProperty.Create(
        nameof(Date), typeof(DateTime?), typeof(DateField), defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnChromeChanged);

    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(
        nameof(MinimumDate), typeof(DateTime?), typeof(DateField), propertyChanged: OnLimitsChanged);

    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(
        nameof(MaximumDate), typeof(DateTime?), typeof(DateField), propertyChanged: OnLimitsChanged);

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

    public DateTime? MinimumDate
    {
        get => (DateTime?)GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    public DateTime? MaximumDate
    {
        get => (DateTime?)GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((DateField)bindable).UpdateChrome();

    private static void OnLimitsChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((DateField)bindable).UpdateLimits();

    private void UpdateLimits()
    {
        if (MinimumDate is { } minimum)
        {
            Input.MinimumDate = minimum.Date;
        }
        else
        {
            Input.ClearValue(DatePicker.MinimumDateProperty);
        }

        if (MaximumDate is { } maximum)
        {
            Input.MaximumDate = maximum.Date;
        }
        else
        {
            Input.ClearValue(DatePicker.MaximumDateProperty);
        }
    }
   
    private void OnInputFocusChanged(object? sender, FocusEventArgs e) =>
        VisualStateManager.GoToState(InputBorder, e.IsFocused ? "Focused" : "Normal");

    private void OnFieldTapped(object? sender, TappedEventArgs e)
    {
        Input.IsOpen = false;
        Input.IsOpen = true;
    }

    private void OnClearTapped(object? sender, TappedEventArgs e) => Date = null;

    private void UpdateChrome()
    {
        LabelView.IsVisible = !string.IsNullOrEmpty(Label);
        PlaceholderView.IsVisible = Date is null;
        ClearButton.IsVisible = Date is not null;
        SemanticProperties.SetDescription(Input, Label ?? Placeholder);
        SemanticProperties.SetDescription(OpenArea, Label ?? Placeholder);
        SemanticProperties.SetDescription(ClearButton, "Очистити дату");
    }
}
