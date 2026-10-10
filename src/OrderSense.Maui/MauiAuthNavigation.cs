using OrderSense.Client.Core;

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
}