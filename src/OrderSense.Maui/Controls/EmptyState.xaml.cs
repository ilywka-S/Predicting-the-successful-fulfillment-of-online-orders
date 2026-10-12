using System.Windows.Input;

namespace OrderSense.Maui.Controls;

public partial class EmptyState : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(EmptyState), Icons.Inbox);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(EmptyState));

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(EmptyState), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(
        nameof(ActionText), typeof(string), typeof(EmptyState), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty ActionCommandProperty = BindableProperty.Create(
        nameof(ActionCommand), typeof(ICommand), typeof(EmptyState));

    public static readonly BindableProperty IsErrorProperty = BindableProperty.Create(
        nameof(IsError), typeof(bool), typeof(EmptyState), false, propertyChanged: OnChromeChanged);

    public EmptyState()
    {
        InitializeComponent();
        UpdateChrome();
    }

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? ActionText
    {
        get => (string?)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((EmptyState)bindable).UpdateChrome();

    private void UpdateChrome()
    {
        IconTile.SetDynamicResource(StyleProperty, IsError ? "EmptyIconTileError" : "EmptyIconTile");
        IconView.SetDynamicResource(StyleProperty, IsError ? "EmptyIconError" : "EmptyIcon");
        DescriptionView.IsVisible = !string.IsNullOrEmpty(Description);
        ActionButton.IsVisible = !string.IsNullOrEmpty(ActionText);
    }
}
