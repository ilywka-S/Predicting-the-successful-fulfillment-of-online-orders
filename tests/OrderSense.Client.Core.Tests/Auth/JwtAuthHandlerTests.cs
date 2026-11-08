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
        tokenManagerMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("mocked_access_token");

        var innerHandler = new TestHttpMessageHandler();
        
        var handler = new JwtAuthHandler(tokenManagerMock.Object)
        {
            InnerHandler = innerHandler
        };

        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ordersense.local/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", innerHandler.ReceivedRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("mocked_access_token", innerHandler.ReceivedRequest?.Headers.Authorization?.Parameter);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? ReceivedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ReceivedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}