namespace OrderSense.Maui.Controls;

public static class ThemePreference
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    private const string PreferenceKey = "ui_theme";

    public static string Current => Preferences.Default.Get(PreferenceKey, System);

    public static void Set(string theme)
    {
        Preferences.Default.Set(PreferenceKey, theme);
        Apply(theme);
    }

    public static void Restore() => Apply(Current);

    private static void Apply(string theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = theme switch
        {
            Light => AppTheme.Light,
            Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
    }
}
