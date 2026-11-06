using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.Services;

public class SignalRService : ISignalRService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ITokenStorage _tokenStorage;
    private readonly HttpClient _httpClient;
    public event Action<OrderFeatures>? OnOrderCreated;
    public event Action<OrderPrediction>? OnPredictionUpdated;
    public event Action<int>? OnBatchJobProgress;

    public SignalRService(ITokenStorage tokenStorage, HttpClient httpClient)
    {
        _tokenStorage = tokenStorage;
        _httpClient = httpClient;
    }

    public async Task StartConnectionAsync()
    {
        if (_hubConnection is not null && _hubConnection.State != HubConnectionState.Disconnected)
            return;

        var baseUrl = _httpClient.BaseAddress?.ToString() ?? "https://localhost:7123";
        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/orders";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await _tokenStorage.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<OrderFeatures>("OrderCreated", (order) => OnOrderCreated?.Invoke(order));
        _hubConnection.On<OrderPrediction>("PredictionUpdated", (prediction) => OnPredictionUpdated?.Invoke(prediction));
        _hubConnection.On<int>("BatchJobProgress", (progress) => OnBatchJobProgress?.Invoke(progress));

        try
        {
            await _hubConnection.StartAsync();
            Console.WriteLine("SignalR підключений.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка підключення до SignalR: {ex.Message}");
        }
    }

    public async Task StopConnectionAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopConnectionAsync();
    }
}