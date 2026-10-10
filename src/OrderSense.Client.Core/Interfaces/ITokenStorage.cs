namespace OrderSense.Client.Core.Interfaces;

public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SaveTokensAsync(string accessToken, string refreshToken);
    Task ClearAsync();
    Task ClearTokensAsync();
    
}