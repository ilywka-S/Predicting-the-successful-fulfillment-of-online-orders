using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui;

public class MauiAuthNavigation : IAuthNavigation
{
    public async Task NavigateToMainAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
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