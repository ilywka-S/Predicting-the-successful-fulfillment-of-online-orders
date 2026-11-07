using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.Services;

public class SignalRService : ISignalRService
{
    private readonly HubConnection _hubConnection;

    public event Action<OrderFeatures>? OnOrderCreated;
    public event Action<OrderPrediction>? OnPredictionUpdated;
    public event Action<BatchJobProgress>? OnBatchJobProgress;

    public SignalRService(ITokenStorage tokenStorage, string baseUrl = "https://localhost:7250")
    {
        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/orders";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await tokenStorage.GetAccessTokenAsync();

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
                Debug.WriteLine(">>> [SignalR] УСПІШНО ПІДДЄДНАНО ДО ХАБУ <<<");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($">>> [SignalR ERROR] Не вдалося підключитися: {ex.Message} <<<");
                Debug.WriteLine(ex.ToString());
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