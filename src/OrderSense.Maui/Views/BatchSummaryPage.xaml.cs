using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Maui.Views;

public partial class BatchSummaryPage : ContentPage, IQueryAttributable
{
    private readonly BatchSummaryViewModel _viewModel;

    public BatchSummaryPage(BatchSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && Guid.TryParse(id?.ToString(), out var jobId))
        {
            _ = _viewModel.OpenAsync(jobId);
        }
    }
}
