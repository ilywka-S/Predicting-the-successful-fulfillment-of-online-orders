using OrderSense.Client.Core.ViewModels;
namespace OrderSense.Maui;

public partial class AppShell : Shell
{
    static AppShell()
    {
        Routing.RegisterRoute(MauiOrderNavigation.OrderDetailsRoute, typeof(Views.OrderDetailsPage));
        Routing.RegisterRoute(MauiOrderNavigation.WhatIfRoute, typeof(Views.WhatIfPage));
    }

    public AppShell()
    {
        InitializeComponent();
    }
    
    public AppShell(AppShellViewModel? viewModel = null)
    {
        InitializeComponent();
        if (viewModel != null)
        {
            BindingContext = viewModel;
        }
    }
}