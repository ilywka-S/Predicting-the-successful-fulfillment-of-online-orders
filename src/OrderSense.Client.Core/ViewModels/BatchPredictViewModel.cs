using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.ViewModels;

public partial class BatchPredictionViewModel : ObservableObject, IDisposable
{
    private readonly IClient _apiClient;
    private readonly ISignalRService _signalRService;
    private readonly IFileService _fileService;
    private readonly SynchronizationContext? _syncContext;
    
    private Guid? _currentJobId;
    private CancellationTokenSource? _pollingCts;
    
    private FileSelectionResult? _selectedFile;

    [ObservableProperty]
    private string _selectedFileName = "Файл не вибрано";

    [ObservableProperty]
    private string _statusMessage = "Очікування файлу...";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBatchJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectCsvCommand))]    
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isSuccess;

    public BatchPredictionViewModel(IClient apiClient, ISignalRService signalRService, IFileService fileService)
    {
        _apiClient = apiClient;
        _signalRService = signalRService;
        _fileService = fileService;
        _syncContext = SynchronizationContext.Current;
    }

    private bool CanInteract() => !IsProcessing;

    public void Subscribe()
    {
        _signalRService.OnBatchJobProgress += HandleProgress;

        if (_currentJobId.HasValue && !IsFinished)
        {
            StartPolling(_currentJobId.Value);
        }
    }

    public void Unsubscribe()
    {
        _signalRService.OnBatchJobProgress -= HandleProgress;
        StopPolling();
    }

    private void HandleProgress(BatchJobProgress data)
    {
        if (_currentJobId == null || data.JobId != _currentJobId.Value) 
            return;

        PostToUI(() =>
        {
            Progress = data.TotalRows > 0 ? (double)data.ProcessedRows / data.TotalRows : 0;
            StatusMessage = $"Обробка: {data.ProcessedRows} / {data.TotalRows}";

            var statusStr = data.Status?.ToString();
            
            if (string.Equals(statusStr, "done", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(statusStr, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var isDone = string.Equals(statusStr, "done", StringComparison.OrdinalIgnoreCase);
                IsFinished = true;
                IsSuccess = isDone; 
                IsProcessing = false;
                StatusMessage = isDone ? "Готово! Можна завантажувати." : "Помилка обробки.";
                StopPolling();
            } 
        });
    }

    private void StartPolling(Guid jobId)
    {
        StopPolling();
        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && !IsFinished)
            {
                try
                {
                    await Task.Delay(5000, token); 

                    var jobStatus = await _apiClient.GetBatchJobAsync(jobId, token);
                    if (jobStatus != null)
                    {
                        PostToUI(() =>
                        {
                            var currentProgress = jobStatus.TotalRows > 0 
                                ? (double)jobStatus.ProcessedRows / jobStatus.TotalRows 
                                : 0;

                            Progress = currentProgress;
                            StatusMessage = $"Обробка: {jobStatus.ProcessedRows} / {jobStatus.TotalRows}";

                            if (jobStatus.Status is BatchJobStatus.Done or BatchJobStatus.Failed)
                            {
                                var isDone = jobStatus.Status == BatchJobStatus.Done;
                                IsFinished = true;
                                IsSuccess = isDone;
                                IsProcessing = false;
                                StatusMessage = isDone ? "Готово! Можна завантажувати." : "Помилка обробки.";
                                StopPolling();
                            }
                        });
                    }
                }
                catch (TaskCanceledException) { break; }
                catch (Exception)
                {
                }
            }
        }, token);
    }

    [RelayCommand]
    public async Task DownloadResultAsync()
    {
        if (_currentJobId == null) return;

        try
        {
            StatusMessage = "Завантаження результату...";
            
            var response = await _apiClient.ExportBatchJobAsync(_currentJobId.Value);
            using var stream = response.Stream; 
            
            var isSaved = await _fileService.SaveFileAsync($"batch_result_{_currentJobId}.csv", stream);
            
            if (isSaved)
            {
                StatusMessage = "Файл успішно збережено!";
            }
            else
            {
                StatusMessage = "Збереження файлу скасовано.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Помилка завантаження файлу.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    public async Task SelectCsvAsync()
    {
        try
        {
            var result = await _fileService.PickCsvAsync();

            if (result != null)
            {
                _selectedFile = result;
                SelectedFileName = result.FileName;
                StatusMessage = "Файл вибрано. Натисніть 'Почати'.";
            }
        }
        catch
        {
            StatusMessage = "Помилка вибору файлу.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    public async Task StartBatchJobAsync()
    {
        if (IsProcessing) return;

        if (_selectedFile == null)
        {
            StatusMessage = "Спочатку виберіть файл!";
            return;
        }

        _currentJobId = null;
        StopPolling();

        IsFinished = false;
        IsSuccess = false;
        IsProcessing = true;
        Progress = 0;
        StatusMessage = "Відправка файлу на сервер...";
        
        try
        {
            using var stream = await _selectedFile.OpenReadAsync();
            
            var response = await _apiClient.CreateBatchJobAsync(new FileParameter(stream, _selectedFile.FileName));
            _currentJobId = response.Id; 
            
            if (_currentJobId.HasValue)
            {
                StatusMessage = "Файл відправлено. Очікування даних...";
                StartPolling(_currentJobId.Value);
            }
        }
        catch (ApiException ex) when (ex.StatusCode == 413)
        {
            IsProcessing = false;
            StatusMessage = "Помилка: файл перевищує ліміт у 1 ГБ.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            IsProcessing = false;
            StatusMessage = "Помилка валідації файлу (400 Bad Request).";
        }
        catch (ApiException ex) when (ex.StatusCode == 501)
        {
            IsProcessing = false;
            StatusMessage = "Метод ще не реалізований на сервері (501 Not Implemented).";
        }
        catch (Exception)
        {
            IsProcessing = false;
            StatusMessage = "Помилка мережі або ліміту сервера (5 МБ).";
        }
    }

    private void PostToUI(Action action)
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    public void Dispose()
    {
        Unsubscribe();
    }
}