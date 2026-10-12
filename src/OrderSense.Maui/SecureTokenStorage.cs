using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui;

public class SecureTokenStorage : ITokenStorage
{
    public Task<string?> GetAccessTokenAsync() => SecureStorage.Default.GetAsync("access_token");
    
    public Task<string?> GetRefreshTokenAsync() => SecureStorage.Default.GetAsync("refresh_token");

    public async Task SaveTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync("access_token", accessToken);
        await SecureStorage.Default.SetAsync("refresh_token", refreshToken);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove("access_token");
        SecureStorage.Default.Remove("refresh_token");
        return Task.CompletedTask;
    }

    public async Task SetRoleAsync(string role)
    {
        await SecureStorage.Default.SetAsync("user_role", role ?? string.Empty);
    }

    public async Task<string> GetRoleAsync()
    {
        return await SecureStorage.Default.GetAsync("user_role");
    }

    public async Task ClearTokensAsync()
    {
        SecureStorage.Default.Remove("access_token");
        SecureStorage.Default.Remove("refresh_token");
        SecureStorage.Default.Remove("user_role");
        await Task.CompletedTask;
    }
}