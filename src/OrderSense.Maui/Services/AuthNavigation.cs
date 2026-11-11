using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Maui.ApplicationModel;          
using Microsoft.Maui.Controls;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Maui.Views;

namespace OrderSense.Maui.Services;

public class AuthNavigation : IAuthNavigation
{
    private readonly IServiceProvider _serviceProvider;

    public AuthNavigation(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

            await Shell.Current.GoToAsync("//MainPage");
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