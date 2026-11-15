using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OrderSense.Client.Core.ViewModels;

public sealed record BatchRiskItem(string Level, int Rows, double Share);

public sealed record BatchFactorItem(string Label, int Rows, double Share);

public partial class BatchSummaryViewModel : ObservableObject
{
    public const int Top = 8;

    private readonly IClient _apiClient;
    private Guid? _jobId;

    public BatchSummaryViewModel(IClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(RiskyShare))]
    private BatchJobSummaryDto? _summary;

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _byCategory = [];

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _byCustomerState = [];

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _byPaymentType = [];

    [ObservableProperty]
    private IReadOnlyList<BatchRiskItem> _riskLevels = [];

    [ObservableProperty]
    private IReadOnlyList<BatchFactorItem> _factors = [];

    public bool HasError => ErrorMessage.Length > 0;

    public bool IsContentVisible => !IsLoading && !HasError && Summary is { PredictedRows: > 0 };

    public bool IsEmpty => !IsLoading && !HasError && Summary is { PredictedRows: 0 };

    public double? RiskyShare => Summary is { PredictedRows: > 0 } summary
        ? (double)(summary.Medium + summary.High) / summary.PredictedRows
        : null;

    public Task OpenAsync(Guid jobId)
    {
        _jobId = jobId;
        return LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_jobId is not { } jobId)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var summary = await _apiClient.GetBatchJobSummaryAsync(jobId, Top);
            if (_jobId != jobId)
            {
                return;
            }

            ByCategory = summary.ByCategory.ToList();
            ByCustomerState = summary.ByCustomerState.ToList();
            ByPaymentType = summary.ByPaymentType.ToList();
            RiskLevels = BuildRiskLevels(summary);
            Factors = BuildFactors(summary);
            Summary = summary;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            ErrorMessage = "Пакет не знайдено або він вам недоступний.";
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            ErrorMessage = "Пакет ще обробляється. Відкрийте дашборд, коли обробка завершиться.";
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException)
        {
            ErrorMessage = ApiErrorMessages.For(ex, "Не вдалося завантажити дашборд пакета.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static List<BatchRiskItem> BuildRiskLevels(BatchJobSummaryDto summary) =>
    [
        new("low", summary.Low, Share(summary.Low, summary.PredictedRows)),
        new("medium", summary.Medium, Share(summary.Medium, summary.PredictedRows)),
        new("high", summary.High, Share(summary.High, summary.PredictedRows)),
    ];

    private static List<BatchFactorItem> BuildFactors(BatchJobSummaryDto summary)
    {
        var risky = summary.Medium + summary.High;

        return summary.TopFactors
            .Select(f => new BatchFactorItem(f.Label, f.Rows, Share(f.Rows, risky)))
            .ToList();
    }

    private static double Share(int part, int total) => total > 0 ? (double)part / total : 0;
}
