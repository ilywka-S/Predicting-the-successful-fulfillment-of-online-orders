using OrderSense.Maui.Controls;

namespace OrderSense.Maui;

public partial class AppShell
{
    private bool _themeRestored;

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (_themeRestored || Parent is null)
        {
            return;
        }

        _themeRestored = true;
        ThemePreference.Restore();
    }
}
