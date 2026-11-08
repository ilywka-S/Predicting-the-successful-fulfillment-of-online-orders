using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui;

public class MauiOrderNavigation : IOrderNavigation
{
    public const string OrderDetailsRoute = "OrderDetails";

    public Task OpenOrderAsync(string orderId) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync($"{OrderDetailsRoute}?id={Uri.EscapeDataString(orderId)}"));
}
