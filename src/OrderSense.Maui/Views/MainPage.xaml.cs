using OrderSense.Client.Core.ViewModels;
namespace OrderSense.Maui.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}