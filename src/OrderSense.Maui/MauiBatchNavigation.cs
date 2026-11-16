using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui;

public class MauiBatchNavigation : IBatchNavigation
{
    public const string SummaryRoute = "BatchSummary";

    public Task OpenSummaryAsync(Guid jobId) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync($"{SummaryRoute}?id={jobId}"));
}
