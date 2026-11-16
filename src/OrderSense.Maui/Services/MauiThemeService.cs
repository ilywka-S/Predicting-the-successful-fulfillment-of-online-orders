using OrderSense.Client.Core.Interfaces;
using OrderSense.Maui.Controls;

namespace OrderSense.Maui.Services;

public class MauiThemeService : IThemeService
{
    public string Current => ThemePreference.Current;

    public void Apply(string theme) => ThemePreference.Set(theme);
}
