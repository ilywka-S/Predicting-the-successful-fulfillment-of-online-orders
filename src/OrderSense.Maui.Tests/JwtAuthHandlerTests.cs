using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using OrderSense.Client.Core;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui.Tests;

public class JwtAuthHandlerTests
{
    private readonly Mock<ITokenStorage> _tokenStorageMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IAuthNavigation> _authNavigationMock;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;
    private readonly Mock<HttpMessageHandler> _authHandlerMock;
    
    public JwtAuthHandlerTests()
    {
        _tokenStorageMock = new Mock<ITokenStorage>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _authNavigationMock = new Mock<IAuthNavigation>();
        _innerHandlerMock = new Mock<HttpMessageHandler>();
        _authHandlerMock = new Mock<HttpMessageHandler>();

        var authClient = new HttpClient(_authHandlerMock.Object) { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock.Setup(f => f.CreateClient("AuthClient")).Returns(authClient);
    }

    [Fact]
    public async Task SendAsync_ShouldAddBearerToken_WhenTokenExists()
    {
        // Arrange
        _tokenStorageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync("valid_token");
        
        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new JwtAuthHandler(_tokenStorageMock.Object, _httpClientFactoryMock.Object, _authNavigationMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
        var client = new HttpClient(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/data");
        await client.SendAsync(request);

        // Assert
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization!.Parameter == "valid_token"),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task SendAsync_ShouldRefreshAndRetry_WhenUnauthorized()
    {
        // Arrange
        _tokenStorageMock.SetupSequence(x => x.GetAccessTokenAsync())
            .ReturnsAsync("old_token")  
            .ReturnsAsync("old_token")  
            .ReturnsAsync("new_token"); 

        _tokenStorageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync("valid_refresh_token");
        _innerHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var authResponse = new { accessToken = "new_token", refreshToken = "new_refresh_token" };
        _authHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(authResponse))
            });

        var handler = new JwtAuthHandler(_tokenStorageMock.Object, _httpClientFactoryMock.Object, _authNavigationMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
        var client = new HttpClient(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/data");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _tokenStorageMock.Verify(x => x.SaveTokensAsync("new_token", "new_refresh_token"), Times.Once);
        
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.Is<HttpRequestMessage>(req => true),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task SendAsync_ShouldClearTokens_WhenRefreshFails()
    {
        // Arrange
        _tokenStorageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync("old_token");
        _tokenStorageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync("invalid_refresh_token");

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        _authHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var handler = new JwtAuthHandler(_tokenStorageMock.Object, _httpClientFactoryMock.Object, _authNavigationMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
        var client = new HttpClient(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/data");
        
        try
        {
            await client.SendAsync(request);
        }
        catch {}

        // Assert
        _tokenStorageMock.Verify(x => x.ClearAsync(), Times.Once);
        
        _authNavigationMock.Verify(x => x.NavigateToLogin(), Times.Once);
    }
}