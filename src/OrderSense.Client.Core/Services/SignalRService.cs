using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.Services; // Для IAuthTokenManager
using OrderSense.Contracts;

namespace OrderSense.Client.Core.Services;

public class SignalRService : ISignalRService
{
    private readonly HubConnection _hubConnection;

    public event Action<OrderFeatures>? OnOrderCreated;
    public event Action<OrderPrediction>? OnPredictionUpdated;
    public event Action<BatchJobProgress>? OnBatchJobProgress;

    public SignalRService(IAuthTokenManager tokenManager, string baseUrl)
    {
        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/orders";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => tokenManager.GetAccessTokenAsync(forceRefresh: false);

#if DEBUG
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                    }
                    return handler;
                };
#endif
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.Closed += async (exception) =>
            Debug.WriteLine($"[SignalR] З'єднання закрите: {exception?.Message}");

        _hubConnection.Reconnected += async (connectionId) =>
            Debug.WriteLine($"[SignalR] Успішно перепідключено! ConnectionId: {connectionId}");

            RegisterHandlers();
        }

    private void RegisterHandlers()
    {
        _hubConnection.On<OrderFeatures>("OrderCreated", data => OnOrderCreated?.Invoke(data));
        _hubConnection.On<OrderPrediction>("PredictionUpdated", data => OnPredictionUpdated?.Invoke(data));
        _hubConnection.On<BatchJobProgress>("BatchJobProgress", data => OnBatchJobProgress?.Invoke(data));
    }
    public async Task StartConnectionAsync()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _hubConnection.StartAsync();
                Debug.WriteLine("[SignalR] УСПІШНО ПІД'ЄДНАНО ДО ХАБУ");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR ERROR] Не вдалося підключитися: {ex.Message}");
            }
        }
    }

    public async Task StopConnectionAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            await _hubConnection.StopAsync();
        }
    }
}