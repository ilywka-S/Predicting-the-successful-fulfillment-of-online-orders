using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.Services;
using OrderSense.Contracts;
using Xunit;

namespace OrderSense.Client.Core.Tests.Auth;

public class AuthTokenManagerTests
{
    private readonly Mock<ITokenStorage> _storageMock = new();
    private readonly Mock<IHttpClientFactory> _factoryMock = new();
    private readonly Mock<IAuthNavigation> _navigationMock = new();
    private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
    private readonly AuthTokenManager _tokenManager;

    public AuthTokenManagerTests()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.ordersense.local")
        };
        _factoryMock.Setup(x => x.CreateClient("AuthClient")).Returns(httpClient);

        _tokenManager = new AuthTokenManager(
            _storageMock.Object, 
            _factoryMock.Object, 
            _navigationMock.Object);
    }

    private static string GenerateJwt(bool isExpired)
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(isExpired ? -10 : 10).ToUnixTimeSeconds();
        var payloadJson = $"{{\"exp\": {exp}}}";
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        return $"header.{payloadBase64}.signature";
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsToken_WhenTokenIsValid()
    {
        // Arrange
        var validToken = GenerateJwt(isExpired: false);
        _storageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(validToken);

        // Act
        var result = await _tokenManager.GetAccessTokenAsync();

        // Assert
        Assert.Equal(validToken, result);
        _httpHandlerMock.Protected().Verify(
            "SendAsync", 
            Times.Never(), 
            ItExpr.IsAny<HttpRequestMessage>(), 
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_CallsRefresh_WhenTokenIsExpired()
    {
        // Arrange
        var expiredToken = GenerateJwt(isExpired: true);
        _storageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(expiredToken);
        _storageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync("valid_refresh_token");

        var authResponse = new AuthResponse { AccessToken = "new_access", RefreshToken = "new_refresh" };
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(authResponse)
        };

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/auth/refresh"), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _tokenManager.GetAccessTokenAsync();

        // Assert
        Assert.Equal("new_access", result);
        _storageMock.Verify(x => x.SaveTokensAsync("new_access", "new_refresh"), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ClearsTokensAndRedirectsToLogin_WhenRefreshFails()
    {
        // Arrange
        var expiredToken = GenerateJwt(isExpired: true);
        _storageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(expiredToken);
        _storageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync("valid_refresh_token");

        var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _tokenManager.GetAccessTokenAsync();

        // Assert
        Assert.Null(result);
        _storageMock.Verify(x => x.ClearAsync(), Times.Once);
        _navigationMock.Verify(x => x.NavigateToLoginAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ClearsTokensAndRedirects_WhenRefreshTokenIsEmpty()
    {
        // Arrange
        var expiredToken = GenerateJwt(isExpired: true);
        _storageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(expiredToken);
        _storageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync(string.Empty);

        // Act
        var result = await _tokenManager.GetAccessTokenAsync();

        // Assert
        Assert.Null(result);
        _httpHandlerMock.Protected().Verify(
            "SendAsync", 
            Times.Never(), 
            ItExpr.IsAny<HttpRequestMessage>(), 
            ItExpr.IsAny<CancellationToken>());
            
        _storageMock.Verify(x => x.ClearAsync(), Times.Once);
        _navigationMock.Verify(x => x.NavigateToLoginAsync(), Times.Once);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]        // 502
    [InlineData(HttpStatusCode.ServiceUnavailable)] // 503
    [InlineData(HttpStatusCode.InternalServerError)]// 500
    public async Task GetAccessTokenAsync_ReturnsNullButDoesNotClearTokens_WhenRefreshFailsWith5xx(HttpStatusCode statusCode)
    {
        // Arrange
        var expiredToken = GenerateJwt(isExpired: true);
        _storageMock.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(expiredToken);
        _storageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync("valid_refresh_token");

        var httpResponse = new HttpResponseMessage(statusCode);

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _tokenManager.GetAccessTokenAsync();

        // Assert
        Assert.Null(result);
        _storageMock.Verify(x => x.ClearAsync(), Times.Never);
        _navigationMock.Verify(x => x.NavigateToLoginAsync(), Times.Never);
    }
}