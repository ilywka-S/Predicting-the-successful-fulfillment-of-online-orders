using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class WhatIfPage : ContentPage, IQueryAttributable
{
    private readonly WhatIfViewModel _viewModel;

    public WhatIfPage(WhatIfViewModel viewModel)
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
