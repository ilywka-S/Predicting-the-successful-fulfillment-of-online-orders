using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OrderSense.Client.Core.ViewModels;

public partial class ModelInfoViewModel : ObservableObject
{
    private readonly IClient _apiClient;

    public ModelInfoViewModel(IClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyPropertyChangedFor(nameof(PrAucLift))]
    private ModelInfoDto? _model;

    [ObservableProperty]
    private IReadOnlyList<FeatureImportanceDto> _importances = [];

    public bool HasError => ErrorMessage.Length > 0;

    public bool IsContentVisible => !IsLoading && !HasError && Model is not null;

    public double? PrAucLift => Model is { PrAuc: { } prAuc, BaselinePrAuc: > 0 } model
        ? prAuc / model.BaselinePrAuc
        : null;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var model = await _apiClient.GetModelAsync();

            Importances = model.FeatureImportances
                .OrderByDescending(f => f.Importance)
                .ToList();
            Model = model;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            ErrorMessage = "Інформація про модель поки недоступна.";
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException)
        {
            ErrorMessage = ApiErrorMessages.For(ex, "Не вдалося завантажити інформацію про модель.");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
