using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OrderSense.Client.Core.Auth;

public class JwtAuthHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;
    private readonly string _baseAddress;

    public JwtAuthHandler(ITokenStorage tokenStorage, string baseAddress)
    {
        _tokenStorage = tokenStorage;
        _baseAddress = baseAddress;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            var refreshed = await TryRefreshTokenAsync(cancellationToken);
            if (refreshed)
            {
                token = await _tokenStorage.GetAccessTokenAsync();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(_baseAddress);

            var response = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>(cancellationToken: cancellationToken);
                if (result != null)
                {
                    await _tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken);
                    return true;
                }
            }
        }
        catch
        {
            // Помилка мережі
        }

        await _tokenStorage.ClearTokensAsync();
        return false;
    }
}

public class TokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}