using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using OrderSense.Client.Core;
using OrderSense.Maui.ViewModels;
using OrderSense.Maui.Views;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.Services;
using CommunityToolkit.Maui.Storage;

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
        builder.Services.AddTransient<DetailsViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoadingViewModel>();
        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<BatchPredictionViewModel>();
        builder.Services.AddTransient<BatchPredictionPage>();

        // Services & Handlers
        builder.Services.AddTransient<JwtAuthHandler>();
        builder.Services.AddSingleton<ITokenStorage, SecureTokenStorage>();
        builder.Services.AddSingleton<IAuthNavigation, MauiAuthNavigation>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);

        // SignalR Service
        builder.Services.AddSingleton<ISignalRService>(sp => 
        {
            var tokenStorage = sp.GetRequiredService<ITokenStorage>();
            return new SignalRService(tokenStorage, baseAddress);
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