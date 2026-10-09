using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrderSense.Client.Core;

namespace OrderSense.Maui;

public class JwtAuthHandler(ITokenStorage tokenStorage, IHttpClientFactory httpClientFactory) : DelegatingHandler
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Додаємо актуальний токен до запиту
        await AddTokenAsync(request);

        // 2. Відправляємо запит
        var response = await base.SendAsync(request, cancellationToken);

        // 3. Якщо отримали 401 Unauthorized — намагаємося оновити токен і повторити запит
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await HandleUnauthorizedAsync(request, response, cancellationToken);
        }

        return response;
    }

    private async Task AddTokenAsync(HttpRequestMessage request)
    {
        var token = await tokenStorage.GetAccessTokenAsync(); // або SecureStorage.GetAsync(...)
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<HttpResponseMessage> HandleUnauthorizedAsync(HttpRequestMessage request, HttpResponseMessage originalResponse, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var currentToken = await tokenStorage.GetAccessTokenAsync();
            if (request.Headers.Authorization?.Parameter != currentToken && !string.IsNullOrWhiteSpace(currentToken))
            {
                return await RetryRequestAsync(request, currentToken, cancellationToken);
            }

            var refreshToken = await tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken) || !await PerformRefreshAsync(refreshToken, cancellationToken))
            {
                await tokenStorage.ClearAsync();
                return originalResponse;
            }

            var newToken = await tokenStorage.GetAccessTokenAsync();
            return await RetryRequestAsync(request, newToken!, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> PerformRefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("AuthClient");
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, cancellationToken);
        
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (result is null) return false;

        await tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken);
        return true;
    }

    private async Task<HttpResponseMessage> RetryRequestAsync(HttpRequestMessage request, string newToken, CancellationToken cancellationToken)
    {
        var retryRequest = CloneRequest(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content,
            Version = request.Version
        };
        foreach (var header in request.Headers) 
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        
        return clone;
    }
}

public record AuthResponse(string AccessToken, string RefreshToken);