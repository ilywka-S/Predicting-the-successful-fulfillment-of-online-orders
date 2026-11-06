using System;
using System.Threading.Tasks;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.Interfaces;

public interface ISignalRService
{
    event Action<OrderFeatures>? OnOrderCreated;
    event Action<OrderPrediction>? OnPredictionUpdated;
    event Action<int>? OnBatchJobProgress;

    Task StartConnectionAsync();
    Task StopConnectionAsync();
}