using OrderSense.Maui.Controls;
using OrderSense.Maui.Views;

namespace OrderSense.Maui;

public partial class AppShell
{
    private bool _designSystemAttached;

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (_designSystemAttached || Parent is null)
        {
            return;
        }

        _designSystemAttached = true;
#if DEBUG
        AddDesignSystemTab();
#endif
        ThemePreference.Restore();
    }

#if DEBUG

    private void AddDesignSystemTab()
    {
        var target = Items.OfType<TabBar>().FirstOrDefault() ?? Items.FirstOrDefault();
        if (target is null)
        {
            return;
        }

        var icon = new FontImageSource { FontFamily = "Lucide", Glyph = Icons.Palette };
        if (Application.Current?.Resources is { } resources
            && resources.TryGetValue("ForegroundLight", out var light)
            && resources.TryGetValue("ForegroundDark", out var dark))
        {
            icon.SetAppThemeColor(FontImageSource.ColorProperty, (Color)light, (Color)dark);
        }

        target.Items.Add(new Tab
        {
            Title = "UI kit",
            Icon = icon,
            Items =
            {
                new ShellContent { Route = "DesignSystemPage", ContentTemplate = new DataTemplate(typeof(DesignSystemPage)) },
            },
        });
    }
#endif
}
