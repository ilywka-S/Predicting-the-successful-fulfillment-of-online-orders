using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels; // Обов'язково додай цей using

namespace OrderSense.Maui;

public class MauiAuthNavigation : IAuthNavigation
{
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
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }
}