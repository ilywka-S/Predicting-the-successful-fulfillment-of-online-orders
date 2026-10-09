using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using OrderSense.Client.Core;
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

			builder.Services.AddTransient<OrderSense.Maui.ViewModels.MainViewModel>();
			builder.Services.AddTransient<OrderSense.Maui.ViewModels.DetailsViewModel>();

			builder.Services.AddTransient<OrderSense.Maui.Views.MainPage>();
			builder.Services.AddTransient<OrderSense.Maui.Views.DetailsPage>();

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

            builder.Services.AddHttpClient<MyNamespace.IClient, MyNamespace.Client>(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
            })
            .AddHttpMessageHandler<JwtAuthHandler>();

        return builder.Build();
    }
}