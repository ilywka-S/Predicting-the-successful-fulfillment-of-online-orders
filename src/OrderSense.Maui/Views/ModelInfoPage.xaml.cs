using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class ModelInfoPage : ContentPage
{
    private readonly ModelInfoViewModel _viewModel;

    public ModelInfoPage(ModelInfoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.Model is null && !_viewModel.IsLoading)
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
