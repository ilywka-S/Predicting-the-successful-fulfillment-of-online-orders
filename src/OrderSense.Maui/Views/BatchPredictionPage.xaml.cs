using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class BatchPredictionPage : ContentPage
{
    public BatchPredictionPage(BatchPredictionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is BatchPredictionViewModel vm) vm.Subscribe();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is BatchPredictionViewModel vm) vm.Unsubscribe();
    }
}