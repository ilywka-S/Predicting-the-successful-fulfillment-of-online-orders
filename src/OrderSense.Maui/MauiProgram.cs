using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using OrderSense.Client.Core;
using OrderSense.Maui.Views;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.Services;

namespace OrderSense.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseDesignSystem();

#if DEBUG
        var baseAddress = DeviceInfo.Platform == DevicePlatform.Android 
            ? "https://10.0.2.2:7250" 
            : "https://localhost:7250";
#else
        var baseAddress = "https://ordersense-api.onrender.com"; 
#endif

        // ViewModels & Pages
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<OrderDetailsViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<OrderDetailsPage>();
        builder.Services.AddTransient<WhatIfViewModel>();
        builder.Services.AddTransient<WhatIfPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoadingViewModel>();
        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<OrdersViewModel>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<AppShell>();

        // Services & Handlers
        builder.Services.AddSingleton<ITokenStorage, SecureTokenStorage>();
        builder.Services.AddSingleton<IAuthTokenManager, AuthTokenManager>();
        builder.Services.AddTransient<JwtAuthHandler>();
        builder.Services.AddSingleton<IAuthNavigation, MauiAuthNavigation>();
        builder.Services.AddSingleton<IOrderNavigation, MauiOrderNavigation>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<IAnalyticsService, ApiAnalyticsService>();
        builder.Services.AddTransient<IWhatIfService, ApiWhatIfService>();

        // SignalR Service
        builder.Services.AddSingleton<ISignalRService>(sp => 
        {
            var tokenManager = sp.GetRequiredService<IAuthTokenManager>();
            return new SignalRService(tokenManager, baseAddress); 
        });

        // HTTP Clients
        builder.Services.AddHttpClient("AuthClient", client => 
        {
            client.BaseAddress = new Uri(baseAddress);
        });

        builder.Services.AddHttpClient<IClient, Client.Core.Client>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        })
        .AddHttpMessageHandler<JwtAuthHandler>();

        return builder.Build();
    }
}