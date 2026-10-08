using CommunityToolkit.Maui;
using OrderSense.Client.Core.Auth;
using Microsoft.Extensions.DependencyInjection;
using OrderSense.Maui.Services;
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

            builder.Services.AddSingleton<ITokenStorage, SecureTokenStorage>();

            var baseAddress = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5000" : "http://localhost:5000";

            builder.Services.AddTransient<JwtAuthHandler>(sp => 
                new JwtAuthHandler(sp.GetRequiredService<ITokenStorage>(), baseAddress));

            builder.Services.AddHttpClient<OrderClient>(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
            })
            .AddHttpMessageHandler<JwtAuthHandler>();


        return builder.Build();
    }
}