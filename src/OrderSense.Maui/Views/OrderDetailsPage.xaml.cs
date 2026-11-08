using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class OrderDetailsPage : ContentPage, IQueryAttributable
{
    private readonly OrderDetailsViewModel _viewModel;

    public OrderDetailsPage(OrderDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is string orderId)
        {
            _viewModel.LoadCommand.Execute(orderId);
        }
    }
}
