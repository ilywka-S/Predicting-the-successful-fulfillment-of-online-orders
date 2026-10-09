using OrderSense.Client.Core;

namespace OrderSense.Maui;

public class MauiAuthNavigation : IAuthNavigation
{
    public void NavigateToLogin()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Microsoft.Maui.Controls.Shell.Current.GoToAsync("//LoginPage");
        });
    }
}