using System.Net;
using System.Net.Http.Headers;
using Moq;
using Xunit;
using OrderSense.Client.Core;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Tests.Auth;

public class JwtAuthHandlerTests
{
    [Fact]
    public async Task SendAsync_ShouldAddBearerToken_WhenTokenIsAvailable()
    {
        // Arrange
        var tokenManagerMock = new Mock<IAuthTokenManager>();
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("mocked_token");

        var innerHandler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new JwtAuthHandler(tokenManagerMock.Object) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ordersense.local/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("mocked_token", innerHandler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal(1, innerHandler.CallCount);
    }

    [Fact]
    public async Task SendAsync_ShouldRetryWithNewToken_WhenApiReturns401()
    {
        // Arrange
        var tokenManagerMock = new Mock<IAuthTokenManager>();
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("old_token");
        
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync("new_token");

        var innerHandler = new TestHttpMessageHandler(req => 
        {
            if (req.Headers.Authorization?.Parameter == "old_token")
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new JwtAuthHandler(tokenManagerMock.Object) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ordersense.local/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, innerHandler.CallCount);
        Assert.Equal("new_token", innerHandler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_ShouldNotRetry_WhenRefreshReturnsNullAfter401()
    {
        // Arrange
        var tokenManagerMock = new Mock<IAuthTokenManager>();
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("old_token");
        
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var innerHandler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var handler = new JwtAuthHandler(tokenManagerMock.Object) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ordersense.local/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, innerHandler.CallCount);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFunc;

        public HttpRequestMessage? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFunc)
        {
            _responseFunc = responseFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(_responseFunc(request));
        }
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/auth/logout")]
    public async Task SendAsync_ShouldBypassTokenManager_ForSpecificAuthRoutes(string path)
    {
        // Arrange
        var tokenManagerMock = new Mock<IAuthTokenManager>();
        var innerHandler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new JwtAuthHandler(tokenManagerMock.Object) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.ordersense.local{path}");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        tokenManagerMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(innerHandler.LastRequest?.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_ShouldAddBearerToken_ForApiAuthMe()
    {
        // Arrange
        var tokenManagerMock = new Mock<IAuthTokenManager>();
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("mocked_me_token");

        var innerHandler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new JwtAuthHandler(tokenManagerMock.Object) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ordersense.local/api/auth/me");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        tokenManagerMock.Verify(x => x.GetAccessTokenAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("mocked_me_token", innerHandler.LastRequest?.Headers.Authorization?.Parameter);
    }
}