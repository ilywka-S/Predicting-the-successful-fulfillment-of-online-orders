using Microsoft.Maui.Handlers;
using OrderSense.Maui.Controls;

namespace OrderSense.Maui;

public static partial class DesignSystem
{
    private static void RemoveDatePickerChrome()
    {
        DatePickerHandler.Mapper.AppendToMapping("OrderSenseBorderless", (handler, view) =>
        {
            if (view is not BorderlessDatePicker)
            {
                return;
            }

#if ANDROID
            handler.PlatformView.BackgroundTintList =
                global::Android.Content.Res.ColorStateList.ValueOf(global::Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif WINDOWS
            handler.PlatformView.BorderThickness = new global::Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Padding = new global::Microsoft.UI.Xaml.Thickness(0, 5, 0, 7);
            handler.PlatformView.Background =
                new global::Microsoft.UI.Xaml.Media.SolidColorBrush(global::Microsoft.UI.Colors.Transparent);
            handler.PlatformView.PlaceholderText = string.Empty;
            handler.PlatformView.HorizontalAlignment = global::Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
#endif
        });
    }
}
