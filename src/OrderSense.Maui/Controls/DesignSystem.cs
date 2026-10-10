using Microsoft.Maui.Handlers;
using OrderSense.Maui.Controls;

namespace OrderSense.Maui;

public static class DesignSystem
{
    public static MauiAppBuilder UseDesignSystem(this MauiAppBuilder builder)
    {
        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("Inter-Regular.ttf", "Inter");
            fonts.AddFont("Inter-Medium.ttf", "InterMedium");
            fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
            fonts.AddFont("Inter-Bold.ttf", "InterBold");
            fonts.AddFont("Lucide.ttf", "Lucide");
        });

        RemoveNativeInputChrome();
        return builder;
    }

    private static void RemoveNativeInputChrome()
    {
        EntryHandler.Mapper.AppendToMapping("OrderSenseBorderless", (handler, view) =>
        {
            if (view is not BorderlessEntry)
            {
                return;
            }

#if ANDROID
            handler.PlatformView.BackgroundTintList =
                global::Android.Content.Res.ColorStateList.ValueOf(global::Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
            handler.PlatformView.BorderStyle = global::UIKit.UITextBorderStyle.None;
#elif WINDOWS
            var transparent = new global::Microsoft.UI.Xaml.Media.SolidColorBrush(global::Microsoft.UI.Colors.Transparent);
            handler.PlatformView.BorderThickness = new global::Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Padding = new global::Microsoft.UI.Xaml.Thickness(0, 6, 0, 6);
            handler.PlatformView.Background = transparent;
            handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new global::Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Resources["TextControlBackgroundFocused"] = transparent;
            handler.PlatformView.Resources["TextControlBackgroundPointerOver"] = transparent;
#endif
        });

        PickerHandler.Mapper.AppendToMapping("OrderSenseBorderless", (handler, view) =>
        {
            if (view is not BorderlessPicker)
            {
                return;
            }

#if ANDROID
            handler.PlatformView.BackgroundTintList =
                global::Android.Content.Res.ColorStateList.ValueOf(global::Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
            handler.PlatformView.BorderStyle = global::UIKit.UITextBorderStyle.None;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new global::Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Padding = new global::Microsoft.UI.Xaml.Thickness(0, 5, 0, 7);
            handler.PlatformView.Background =
                new global::Microsoft.UI.Xaml.Media.SolidColorBrush(global::Microsoft.UI.Colors.Transparent);
#endif
        });
    }
}
