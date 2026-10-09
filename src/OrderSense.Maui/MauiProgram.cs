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

            builder.Services.AddHttpClient("AuthClient", client => 
            {
                client.BaseAddress = new Uri("https://localhost:7250"); 
            });

            builder.Services.AddHttpClient<OrderClient>(client =>
            {
                var baseUrl = DeviceInfo.Platform == DevicePlatform.Android 
                    ? "http://10.0.2.2:5266" 
                    : "http://localhost:5266";
                    
                client.BaseAddress = new Uri(baseUrl);
            });
        return builder.Build();
    }
}