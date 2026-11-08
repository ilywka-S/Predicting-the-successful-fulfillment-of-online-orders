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
    private bool _isProcessing;

    public BatchPredictionViewModel(IClient apiClient, ISignalRService signalRService, IFileService fileService)
    {
        _apiClient = apiClient;
        _signalRService = signalRService;
        _fileService = fileService;
    }

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

        Progress = data.TotalRows > 0 ? (double)data.ProcessedRows / data.TotalRows : 0;
        StatusMessage = $"Обробка: {data.ProcessedRows} / {data.TotalRows}";

        if (data.Status.ToString() == "Done" || data.Status.ToString() == "Failed")
        {
            IsFinished = true;
            IsProcessing = false;
            StatusMessage = data.Status.ToString() == "Done" ? "Готово! Можна завантажувати." : "Помилка обробки.";
            StopPolling();
        } 
    }

    [RelayCommand]
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

    [RelayCommand]
    public async Task StartBatchJobAsync()
    {
        if (_selectedFile == null)
        {
            StatusMessage = "Спочатку виберіть файл!";
            return;
        }

        IsFinished = false;
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
        catch (Exception)
        {
            IsProcessing = false;
            StatusMessage = "Помилка відправки файлу.";
        }
    }

    private void StartPolling(Guid jobId)
    {
        StopPolling();
        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested && !IsFinished)
                {
                    await Task.Delay(5000, token); 

                    var jobStatus = await _apiClient.GetBatchJobAsync(jobId, token);
                    if (jobStatus != null)
                    {
                        var currentProgress = jobStatus.TotalRows > 0 
                            ? (double)jobStatus.ProcessedRows / jobStatus.TotalRows 
                            : 0;

                        Progress = currentProgress;
                        StatusMessage = $"Обробка: {jobStatus.ProcessedRows} / {jobStatus.TotalRows}";

                        if (jobStatus.Status.ToString() == "Done" || jobStatus.Status.ToString() == "Failed")
                        {
                            IsFinished = true;
                            IsProcessing = false;
                            StatusMessage = jobStatus.Status.ToString() == "Done" ? "Готово! Можна завантажувати." : "Помилка обробки.";
                            break;
                        }
                    }
                }
            }
            catch (TaskCanceledException) {  }
            catch {  }
        }, token);
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
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
            await _fileService.SaveFileAsync($"batch_result_{_currentJobId}.csv", stream);
            
            StatusMessage = "Файл успішно збережено!";
        }
        catch (Exception)
        {
            StatusMessage = "Помилка завантаження файлу.";
        }
    }

    public void Dispose()
    {
        Unsubscribe();
    }
}