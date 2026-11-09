using System.Diagnostics;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui;

public class MauiAuthNavigation : IAuthNavigation
{
    private readonly IServiceProvider _services;

    public MauiAuthNavigation(IServiceProvider services)
    {
        _services = services;
    }

    public async Task NavigateToMainAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is AppShell currentShell &&
                currentShell.BindingContext is AppShellViewModel shellVm)
            {
                await shellVm.UpdateVisibilityAsync();
            }

            await Shell.Current.GoToAsync("//OrdersPage");
        });
    }

    public async Task NavigateToLoginAsync()
    {
        StopSignalRInBackground();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }

    private void StopSignalRInBackground()
    {
        var signalRService = _services.GetRequiredService<ISignalRService>();

        _ = Task.Run(async () =>
        {
            try
            {
                await signalRService.StopConnectionAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR ERROR] Не вдалося зупинити: {ex.Message}");
            }
        });
    }
}
