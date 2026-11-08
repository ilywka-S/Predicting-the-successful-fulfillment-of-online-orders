using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui;

public class MauiOrderNavigation : IOrderNavigation
{
    public const string OrderDetailsRoute = "OrderDetails";
    public const string WhatIfRoute = "WhatIf";

    public Task OpenOrderAsync(string orderId) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync($"{OrderDetailsRoute}?id={Uri.EscapeDataString(orderId)}"));

    public Task OpenWhatIfAsync(string orderId) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync($"{WhatIfRoute}?id={Uri.EscapeDataString(orderId)}"));
}
