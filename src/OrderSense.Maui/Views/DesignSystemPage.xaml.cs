using OrderSense.Maui.Controls;

namespace OrderSense.Maui.Views;

public partial class DesignSystemPage : ContentPage
{
    public DesignSystemPage()
    {
        InitializeComponent();

        SampleSelect.ItemsSource = new[] { "Створено", "Відправлено", "Доставлено", "Скасовано" };

        ThemeSwitch.SelectedValue = ThemePreference.Current;
        ThemeSwitch.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SegmentedControl.SelectedValue) && ThemeSwitch.SelectedValue is string theme)
            {
                ThemePreference.Set(theme);
            }
        };
    }
}
