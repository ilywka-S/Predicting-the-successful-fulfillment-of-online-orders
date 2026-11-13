namespace OrderSense.Client.Core.Interfaces;

public interface IAuthTokenManager
{
    Task<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}