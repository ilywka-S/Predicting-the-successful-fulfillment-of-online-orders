using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using OrderSense.Client.Core;
using OrderSense.Maui.ViewModels;
using OrderSense.Maui.Views;
using OrderSense.Client.Core.ViewModels;
namespace OrderSense.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

			builder.Services.AddTransient<MainViewModel>();
			builder.Services.AddTransient<DetailsViewModel>();

			builder.Services.AddTransient<MainPage>();
			builder.Services.AddTransient<DetailsPage>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<LoadingViewModel>();
            builder.Services.AddTransient<LoadingPage>();

            builder.Services.AddTransient<JwtAuthHandler>();
            builder.Services.AddSingleton<ITokenStorage, SecureTokenStorage>();

            builder.Services.AddSingleton<IAuthNavigation, MauiAuthNavigation>();

            string baseAddress = "https://ordersense-api.onrender.com"; 

            #if DEBUG
                baseAddress = DeviceInfo.Platform == DevicePlatform.Android 
                    ? "https://10.0.2.2:7250" 
                    : "https://localhost:7250";
            #endif

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