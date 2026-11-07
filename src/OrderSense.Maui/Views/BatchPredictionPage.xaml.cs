using OrderSense.Maui.ViewModels;

namespace OrderSense.Maui.Views;

public partial class BatchPredictionPage : ContentPage
{
    public BatchPredictionPage(BatchPredictionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}