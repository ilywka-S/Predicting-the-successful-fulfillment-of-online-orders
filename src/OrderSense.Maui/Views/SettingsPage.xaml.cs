using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_viewModel.IsLoading)
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
