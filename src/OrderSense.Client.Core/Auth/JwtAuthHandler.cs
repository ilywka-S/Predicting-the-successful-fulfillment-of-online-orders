using System.Net.Http.Headers;

namespace OrderSense.Client.Core.Auth;

public class JwtAuthHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;

    public JwtAuthHandler(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // TODO: Логіка 401 Unauthorized та Refresh токена
        
        return response;
    }
}