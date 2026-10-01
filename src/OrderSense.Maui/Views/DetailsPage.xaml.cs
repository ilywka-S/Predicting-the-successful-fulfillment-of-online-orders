using OrderSense.Maui.ViewModels;

namespace OrderSense.Maui.Views;

public partial class DetailsPage : ContentPage
{
    public DetailsPage(DetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}