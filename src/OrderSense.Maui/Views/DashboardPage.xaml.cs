using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is DashboardViewModel viewModel)
        {
            if (viewModel.RiskByDay.Count == 0 && !viewModel.IsAnalyticsLoading)
            {
                viewModel.LoadAnalyticsCommand.Execute(null);
            }

            await viewModel.InitializeAsync();
        }
    }
}
