using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Maui.ApplicationModel;          
using Microsoft.Maui.Controls;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Maui.Views;

namespace OrderSense.Maui.Services;

public class AuthNavigation : IAuthNavigation
{
    private readonly IServiceProvider _serviceProvider;

    public AuthNavigation(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task NavigateToMainAsync()
    {
        var dashboardPage = _serviceProvider.GetRequiredService<MainPage>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Application.Current!.MainPage = new NavigationPage(dashboardPage);
        });

        return Task.CompletedTask;
    }

    public Task NavigateToLoginAsync()
    {
        var loginPage = _serviceProvider.GetRequiredService<LoginPage>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Application.Current!.MainPage = new NavigationPage(loginPage);
        });

        return Task.CompletedTask;
    }
}