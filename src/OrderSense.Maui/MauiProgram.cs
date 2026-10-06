using CommunityToolkit.Maui;

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


        return builder.Build();
    }
}