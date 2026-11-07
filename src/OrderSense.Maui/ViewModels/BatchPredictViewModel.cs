using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using OrderSense.Client.Core.Interfaces;
// Переконайся, що цей using відповідає неймспейсу твого IClient
using OrderSense.Client.Core; 

namespace OrderSense.Maui.ViewModels;

public partial class BatchPredictionViewModel : ObservableObject
{
    private readonly IClient _apiClient;
    private readonly ISignalRService _signalRService;
    private readonly IFileSaver _fileSaver;

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

    private FileResult? _selectedFile;
    private Guid _currentJobId = Guid.Empty;

    public BatchPredictionViewModel(IClient apiClient, ISignalRService signalRService, IFileSaver fileSaver)
    {
        _apiClient = apiClient;
        _signalRService = signalRService;
        _fileSaver = fileSaver;

        _signalRService.OnBatchJobProgress += HandleJobProgress;
    }

    private void HandleJobProgress(int progressPercent)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Progress = progressPercent / 100.0;
            if (progressPercent >= 100)
            {
                IsProcessing = false;
                IsFinished = true;
            }
        });
    }

    [RelayCommand]
    public async Task PickFileAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.Android, new[] { "text/comma-separated-values", "text/csv" } },
                });

            var options = new PickOptions
            {
                PickerTitle = "Оберіть CSV файл із замовленнями",
                FileTypes = customFileType,
            };

            var result = await FilePicker.Default.PickAsync(options);
            
            if (result != null)
            {
                _selectedFile = result;
                SelectedFileName = result.FileName;
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
        if (_selectedFile == null)
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
            using var stream = await _selectedFile.OpenReadAsync();
            var fileParameter = new OrderSense.Client.Core.FileParameter(stream, _selectedFile.FileName, "text/csv");
            
            var batchJob = await _apiClient.CreateBatchJobAsync(fileParameter);
            _currentJobId = batchJob.Id;
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
            
            byte[] fileBytes = response.FileContents;

            using var stream = new MemoryStream(fileBytes);
            var fileSaverResult = await _fileSaver.SaveAsync($"predictions_job_{_currentJobId}.csv", stream, default);
            
            if (!fileSaverResult.IsSuccessful)
            {
                ErrorMessage = $"Не вдалося зберегти файл. Помилка: {fileSaverResult.Exception?.Message}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Помилка експорту: {ex.Message}";
        }
    }
}