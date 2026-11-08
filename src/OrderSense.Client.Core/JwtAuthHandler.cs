using System.Net;
using System.Net.Http.Headers;
using OrderSense.Client.Core.Services;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core;

public class JwtAuthHandler(IAuthTokenManager tokenManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenManager.GetAccessTokenAsync(forceRefresh: false, cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newToken = await tokenManager.GetAccessTokenAsync(forceRefresh: true, cancellationToken);
            if (!string.IsNullOrEmpty(newToken) && newToken != token)
            {
                var retryRequest = CloneRequest(request);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                return await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
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