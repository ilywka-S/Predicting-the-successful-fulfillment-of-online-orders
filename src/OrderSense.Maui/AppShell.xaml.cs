using OrderSense.Maui.Views;

namespace OrderSense.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        Routing.RegisterRoute("DetailsPage", typeof(DetailsPage));
    }
}