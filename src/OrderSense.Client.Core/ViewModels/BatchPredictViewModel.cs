using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.ViewModels;

public partial class BatchPredictionViewModel : ObservableObject
{
    private readonly IClient _apiClient;
    private readonly ISignalRService _signalRService;
    private readonly IFileService _fileService;
    private CancellationTokenSource? _pollingCts;

    [ObservableProperty]
    private string _selectedFileName = "Файл не обрано";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    private Stream? _selectedStream;
    private Guid _currentJobId = Guid.Empty;

    public BatchPredictionViewModel(IClient apiClient, ISignalRService signalRService, IFileService fileService)
    {
        _apiClient = apiClient;
        _signalRService = signalRService;
        _fileService = fileService;
    }
    
    public void Subscribe() => _signalRService.OnBatchJobProgress += HandleProgress;

    public void Unsubscribe() 
    {
        _signalRService.OnBatchJobProgress -= HandleProgress;
        _pollingCts?.Cancel();
    }

    private void HandleProgress(BatchJobProgress data)
    {
        if (data.JobId != _currentJobId) return;

        if (data.TotalRows > 0)
        {
            Progress = (double)data.ProcessedRows / data.TotalRows;
        }

        if (data.Status is "done" or "failed")
        {
            IsProcessing = false;
            IsFinished = data.Status == "done";
            _pollingCts?.Cancel();
        }
    }

    private async Task StartPollingFallbackAsync(Guid jobId)
    {
        _pollingCts?.Cancel();
        _pollingCts = new CancellationTokenSource();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        try
        {
            while (await timer.WaitForNextTickAsync(_pollingCts.Token))
            {
                if (IsFinished || !_isProcessing) break;

                var job = await _apiClient.GetBatchJobAsync(jobId, _pollingCts.Token);
                
                var currentProgress = job.TotalRows > 0 ? (double)job.ProcessedRows / job.TotalRows : 0;
                
                if (currentProgress > Progress) Progress = currentProgress;

                if (job.Status is BatchJobStatus.Done or BatchJobStatus.Failed)
                {
                    IsProcessing = false;
                    IsFinished = job.Status == BatchJobStatus.Done;
                    _pollingCts.Cancel();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = $"Помилка синхронізації: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PickFileAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            
            var (stream, fileName) = await _fileService.PickCsvFileAsync();
            
            if (stream != null)
            {
                _selectedStream?.Dispose();
                
                _selectedStream = stream;
                SelectedFileName = fileName;
                IsFinished = false;
                Progress = 0;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Помилка вибору файлу: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task UploadAndStartJobAsync()
    {
        if (_selectedStream == null)
        {
            ErrorMessage = "Будь ласка, оберіть файл.";
            return;
        }

        IsProcessing = true;
        IsFinished = false;
        Progress = 0;
        ErrorMessage = string.Empty;

        try
        {
            if (_selectedStream.CanSeek)
            {
                _selectedStream.Position = 0;
            }

            var fileParameter = new FileParameter(_selectedStream, SelectedFileName, "text/csv");
            
            var batchJob = await _apiClient.CreateBatchJobAsync(fileParameter);
            _currentJobId = batchJob.Id;

            _ = StartPollingFallbackAsync(_currentJobId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Помилка завантаження CSV: {ex.Message}";
            IsProcessing = false;
        }
    }

    [RelayCommand]
    public async Task DownloadResultsAsync()
    {
        if (_currentJobId == Guid.Empty) return;

        try
        {
            ErrorMessage = string.Empty;
            
            var response = await _apiClient.ExportBatchJobAsync(_currentJobId);
            
            using var stream = new MemoryStream(response.FileContents);
            
            var isSaved = await _fileService.SaveFileAsync($"predictions_job_{_currentJobId}.csv", stream);
            
            if (!isSaved)
            {
                ErrorMessage = "Не вдалося зберегти файл або дію скасовано.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Помилка експорту: {ex.Message}";
        }
    }
}