using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISignalRService _signalRService;
    private readonly SynchronizationContext? _syncContext;

    public ObservableCollection<OrderFeatures> Orders { get; } = new();
    public ObservableCollection<OrderPrediction> Predictions { get; } = new();

    [ObservableProperty]
    private int _batchJobProgress;
    [ObservableProperty]
    private OrderPrediction? _latestPrediction;

    public DashboardViewModel(ISignalRService signalRService)
    {
        _signalRService = signalRService;
        
        _syncContext = SynchronizationContext.Current;

        _signalRService.OnOrderCreated += HandleOrderCreated;
        _signalRService.OnPredictionUpdated += HandlePredictionUpdated;
        _signalRService.OnBatchJobProgress += HandleBatchProgress;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await _signalRService.StartConnectionAsync();
    }

    private void HandleOrderCreated(OrderFeatures order)
    {
        PostToUI(() => Orders.Insert(0, order));
    }

    private void HandlePredictionUpdated(OrderPrediction prediction)
    {
        PostToUI(() =>
        {
            LatestPrediction = prediction;
            Predictions.Insert(0, prediction);
        });
    }

    private void HandleBatchProgress(BatchJobProgress data)
    {
        PostToUI(() => 
        {
            var progressValue = data.TotalRows > 0 
                ? (data.ProcessedRows * 100) / data.TotalRows 
                : 0;

            BatchJobProgress = progressValue; 
        });
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
}