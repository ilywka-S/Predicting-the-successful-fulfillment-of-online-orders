using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class BatchPredictionPage : ContentPage
{
    private readonly BatchPredictionViewModel _viewModel;

    public BatchPredictionPage(BatchPredictionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Subscribe();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Unsubscribe();
    }
}