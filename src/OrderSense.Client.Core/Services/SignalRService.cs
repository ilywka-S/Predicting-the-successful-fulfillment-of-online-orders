using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;
using System.Net.Http.Json;

namespace OrderSense.Client.Core.Services;

public class SignalRService : ISignalRService
{
    private readonly HubConnection _hubConnection;
    private readonly ITokenStorage _tokenStorage;
    private readonly IHttpClientFactory _httpClientFactory;

    public event Action<OrderFeatures>? OnOrderCreated;
    public event Action<OrderPrediction>? OnPredictionUpdated;
    public event Action<int>? OnBatchJobProgress;

    public SignalRService(ITokenStorage tokenStorage, IHttpClientFactory httpClientFactory, string baseUrl = "https://localhost:7250")
    {
        _tokenStorage = tokenStorage;
        _httpClientFactory = httpClientFactory;
        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/orders";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var token = await _tokenStorage.GetAccessTokenAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                    }

                    var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var freshAccessToken = await TryRefreshTokenAsync(refreshToken);
                        if (!string.IsNullOrEmpty(freshAccessToken))
                        {
                            return freshAccessToken;
                        }
                    }

                    return token;
                };

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
        {
            Debug.WriteLine($">>> [SignalR] З'єднання закрите: {exception?.Message} <<<");
        };

        _hubConnection.Reconnected += async (connectionId) =>
        {
            Debug.WriteLine($">>> [SignalR] Успішно перепідключено! ConnectionId: {connectionId} <<<");
        };

        RegisterHandlers();
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
                
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Debug.WriteLine(">>> [SignalR] Токен недійсний. Спробуймо оновити сесію... <<<");
                }
            }
        }
    }

    private async Task<string?> TryRefreshTokenAsync(string refreshToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("AuthClient");
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result is null) return null;

            await _tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken);
            return result.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    private void RegisterHandlers()
    {
        _hubConnection.On<OrderFeatures>("OrderCreated", data => OnOrderCreated?.Invoke(data));
        _hubConnection.On<OrderPrediction>("PredictionUpdated", data => OnPredictionUpdated?.Invoke(data));
        _hubConnection.On<int>("BatchJobProgress", progress => OnBatchJobProgress?.Invoke(progress));
    }

    public async Task StopConnectionAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            await _hubConnection.StopAsync();
        }
    }
}