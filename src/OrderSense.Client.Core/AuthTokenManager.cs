using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts; // Твій AuthResponse

namespace OrderSense.Client.Core.Services;

public interface IAuthTokenManager
{
    Task<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public class AuthTokenManager : IAuthTokenManager
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthNavigation _authNavigation;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AuthTokenManager(ITokenStorage tokenStorage, IHttpClientFactory httpClientFactory, IAuthNavigation authNavigation)
    {
        _tokenStorage = tokenStorage;
        _httpClientFactory = httpClientFactory;
        _authNavigation = authNavigation;
    }

    public async Task<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var token = await _tokenStorage.GetAccessTokenAsync();

        if (!forceRefresh && !IsTokenExpired(token))
        {
            return token;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            token = await _tokenStorage.GetAccessTokenAsync();
            if (!forceRefresh && !IsTokenExpired(token))
            {
                return token;
            }

            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                await ForceLogoutAsync();
                return null;
            }

            using var client = _httpClientFactory.CreateClient("AuthClient");
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await ForceLogoutAsync();
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                await ForceLogoutAsync();
                return null;
            }

            await _tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken);
            return result.AccessToken;
        }
        catch
        {
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ForceLogoutAsync()
    {
        await _tokenStorage.ClearAsync();
        await _authNavigation.NavigateToLoginAsync();
    }

    private bool IsTokenExpired(string? token)
    {
        if (string.IsNullOrEmpty(token)) return true;
        
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1];
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            payload = payload.Replace('-', '+').Replace('_', '/');
            
            var jsonBytes = Convert.FromBase64String(payload);
            var jsonDoc = JsonDocument.Parse(jsonBytes);
            
            if (jsonDoc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
                
                return expirationTime <= DateTime.UtcNow.AddMinutes(1);
            }
        }
        catch { } 

        return true;
    }
}