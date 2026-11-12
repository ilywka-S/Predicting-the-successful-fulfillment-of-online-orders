using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OrderSense.Client.Core.ViewModels;

public sealed record BatchRowError(int? RowNo, string Message)
{
    public string Location => RowNo is { } rowNo ? $"Рядок {rowNo}" : "Файл";
}

public partial class BatchPredictionViewModel
{
    public const int ResultsPageSize = 20;

    private int _resultsPage;

    public ObservableCollection<BatchResultDto> Results { get; } = [];

    public ObservableCollection<BatchRowError> RowErrors { get; } = [];

    [ObservableProperty]
    private int _resultsTotal;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreResultsCommand))]
    private bool _isLoadingResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResultsError))]
    private string _resultsError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJobMessage))]
    private string _jobMessage = string.Empty;

    public bool HasResults => Results.Count > 0;

    public bool HasMoreResults => Results.Count < ResultsTotal;

    public bool HasRowErrors => RowErrors.Count > 0;

    public bool HasResultsError => !string.IsNullOrEmpty(ResultsError);

    public bool HasJobMessage => !string.IsNullOrEmpty(JobMessage);

    partial void OnIsProcessingChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        Results.Clear();
        RowErrors.Clear();
        _resultsPage = 0;
        ResultsTotal = 0;
        ResultsError = string.Empty;
        JobMessage = string.Empty;
        NotifyResults();
    }

    partial void OnIsFinishedChanged(bool value)
    {
        if (value && _currentJobId is { } jobId)
        {
            _ = LoadFinishedJobAsync(jobId);
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadMoreResults))]
    private Task LoadMoreResultsAsync() =>
        _currentJobId is { } jobId ? LoadResultsPageAsync(jobId, _resultsPage + 1) : Task.CompletedTask;

    [RelayCommand]
    private async Task DownloadTemplateAsync()
    {
        try
        {
            using var response = await _apiClient.GetBatchJobTemplateAsync();
            var saved = await _fileService.SaveFileAsync("ordersense-template.csv", response.Stream);
            StatusMessage = saved ? "Шаблон збережено." : "Збереження шаблону скасовано.";
        }
        catch (Exception exception)
        {
            StatusMessage = ApiErrorMessages.For(exception, "Не вдалося завантажити шаблон.");
        }
    }

    private bool CanLoadMoreResults() => !IsLoadingResults && HasMoreResults;

    private async Task LoadFinishedJobAsync(Guid jobId)
    {
        try
        {
            var job = await _apiClient.GetBatchJobAsync(jobId);
            if (_currentJobId != jobId)
            {
                return;
            }

            JobMessage = job.Error ?? string.Empty;

            if (job.Status == BatchJobStatus.Done)
            {
                await LoadResultsPageAsync(jobId, 1);
            }
        }
        catch (Exception exception)
        {
            ResultsError = ApiErrorMessages.For(exception, "Не вдалося завантажити результати.");
        }
    }

    private async Task LoadResultsPageAsync(Guid jobId, int page)
    {
        IsLoadingResults = true;
        ResultsError = string.Empty;

        try
        {
            var response = await _apiClient.GetBatchJobResultsAsync(jobId, page, ResultsPageSize);
            if (_currentJobId != jobId)
            {
                return;
            }

            foreach (var result in response.Items)
            {
                Results.Add(result);
            }

            _resultsPage = page;
            ResultsTotal = response.Total;
            NotifyResults();
        }
        catch (Exception exception)
        {
            ResultsError = ApiErrorMessages.For(exception, "Не вдалося завантажити результати.");
        }
        finally
        {
            IsLoadingResults = false;
        }
    }

    private void ShowRowErrors(ApiException exception)
    {
        RowErrors.Clear();

        if (exception is ApiException<ValidationProblemDetails> { Result.Errors: { } errors })
        {
            foreach (var (key, messages) in errors.OrderBy(error => RowNumber(error.Key) ?? 0))
            {
                foreach (var message in messages)
                {
                    RowErrors.Add(new BatchRowError(RowNumber(key), message));
                }
            }
        }

        OnPropertyChanged(nameof(HasRowErrors));
    }

    private void NotifyResults()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasMoreResults));
        OnPropertyChanged(nameof(HasRowErrors));
        LoadMoreResultsCommand.NotifyCanExecuteChanged();
    }

    private static int? RowNumber(string key) =>
        key.StartsWith("row ", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(key.AsSpan(4), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rowNo)
            ? rowNo
            : null;
}
