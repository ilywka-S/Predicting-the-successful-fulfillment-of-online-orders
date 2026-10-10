using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui;

public class MauiAuthNavigation : IAuthNavigation
{
    public void NavigateToLogin()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Shell.Current.GoToAsync("//LoginPage");
        });
    }

    public void NavigateToMain()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Shell.Current.GoToAsync("//MainPage"); 
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