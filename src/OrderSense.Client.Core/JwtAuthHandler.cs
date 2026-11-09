using System.Net;
using System.Net.Http.Headers;
using OrderSense.Client.Core.Services;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core;

public class JwtAuthHandler(IAuthTokenManager tokenManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var token = await tokenManager.GetAccessTokenAsync(forceRefresh: false, cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (request.Content != null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }
        bool isFileContent = request.Content is MultipartFormDataContent || request.Content is StreamContent;
        
        if (request.Content != null && !isFileContent)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newToken = await tokenManager.GetAccessTokenAsync(forceRefresh: true, cancellationToken);
            if (!string.IsNullOrEmpty(newToken) && newToken != token)
            {
                if (isFileContent)
                {
                    return response;
                }

                var retryRequest = await CloneRequestAsync(request, cancellationToken);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                return await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
        
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content != null)
        {
            var buffer = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var byteArrayContent = new ByteArrayContent(buffer);

            foreach (var header in request.Content.Headers)
            {
                byteArrayContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = byteArrayContent;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var prop in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(prop.Key), prop.Value);
        }

        return clone;
    }
}