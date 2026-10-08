using OrderSense.Client.Core.Auth;

namespace OrderSense.Maui.Services;

public class SecureTokenStorage : ITokenStorage
{
    public async Task<string?> GetAccessTokenAsync()
    {
        return await SecureStorage.Default.GetAsync("jwt_access_token");
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        return await SecureStorage.Default.GetAsync("jwt_refresh_token");
    }

    public async Task SaveTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync("jwt_access_token", accessToken);
        await SecureStorage.Default.SetAsync("jwt_refresh_token", refreshToken);
    }

    public Task ClearTokensAsync()
    {
        SecureStorage.Default.Remove("jwt_access_token");
        SecureStorage.Default.Remove("jwt_refresh_token");
        return Task.CompletedTask;
    }
}