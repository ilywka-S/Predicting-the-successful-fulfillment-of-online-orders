using System.Windows.Input;

namespace OrderSense.Maui.Controls;

public partial class TextField : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(TextField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TextField), defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(TextField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(TextField), propertyChanged: OnChromeChanged);

    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
        nameof(IsPassword), typeof(bool), typeof(TextField), false, propertyChanged: OnChromeChanged);

    public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(
        nameof(Keyboard), typeof(Keyboard), typeof(TextField), Keyboard.Default);

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
        nameof(ReturnType), typeof(ReturnType), typeof(TextField), ReturnType.Default);

    public static readonly BindableProperty ReturnCommandProperty = BindableProperty.Create(
        nameof(ReturnCommand), typeof(ICommand), typeof(TextField));

    public static readonly BindableProperty ErrorTextProperty = BindableProperty.Create(
        nameof(ErrorText), typeof(string), typeof(TextField), propertyChanged: OnChromeChanged);

    private bool _focused;
    private bool _revealed;

    public TextField()
    {
        InitializeComponent();
        UpdateChrome();
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Placeholder
    {
        get => (string?)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }

    public ICommand? ReturnCommand
    {
        get => (ICommand?)GetValue(ReturnCommandProperty);
        set => SetValue(ReturnCommandProperty, value);
    }

    public string? ErrorText
    {
        get => (string?)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    private bool HasError => !string.IsNullOrEmpty(ErrorText);

    public new bool Focus() => Input.Focus();

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((TextField)bindable).UpdateChrome();

    private void OnInputFocusChanged(object? sender, FocusEventArgs e)
    {
        _focused = e.IsFocused;
        UpdateChrome();
    }

    private void OnRevealTapped(object? sender, TappedEventArgs e)
    {
        _revealed = !_revealed;
        UpdateChrome();
    }

    private void UpdateChrome()
    {
        LabelView.IsVisible = !string.IsNullOrEmpty(Label);
        IconView.IsVisible = !string.IsNullOrEmpty(Icon);
        ErrorView.IsVisible = HasError;

        RevealToggle.IsVisible = IsPassword;
        Input.IsPassword = IsPassword && !_revealed;
        RevealIcon.Text = _revealed ? Icons.EyeOff : Icons.Eye;
        SemanticProperties.SetDescription(RevealToggle, _revealed ? "Сховати пароль" : "Показати пароль");
        SemanticProperties.SetDescription(Input, Label ?? Placeholder);

        VisualStateManager.GoToState(InputBorder, HasError ? "Invalid" : _focused ? "Focused" : "Normal");
    }
}
