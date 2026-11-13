using System;
using System.Threading.Tasks;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.Interfaces;

public interface ISignalRService
{
    event Action<OrderFeatures>? OnOrderCreated;
    event Action<OrderPrediction>? OnPredictionUpdated;
    public event Action<BatchJobProgress>? OnBatchJobProgress;

    Task StartConnectionAsync();
    Task StopConnectionAsync();
}