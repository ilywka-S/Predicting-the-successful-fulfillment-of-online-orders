using System.Collections;

namespace OrderSense.Maui.Controls;

public partial class SelectField : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(SelectField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(SelectField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IList), typeof(SelectField));

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(object), typeof(SelectField), defaultBindingMode: BindingMode.TwoWay);

    public SelectField()
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

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((SelectField)bindable).UpdateChrome();

    private void OnInputFocusChanged(object? sender, FocusEventArgs e) =>
        VisualStateManager.GoToState(InputBorder, e.IsFocused ? "Focused" : "Normal");

    private void UpdateChrome()
    {
        LabelView.IsVisible = !string.IsNullOrEmpty(Label);
        SemanticProperties.SetDescription(Input, Label ?? Placeholder);
    }
}
