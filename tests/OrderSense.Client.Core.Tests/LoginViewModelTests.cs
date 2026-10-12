using Xunit;
using Moq;
using FluentAssertions;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Tests;

public class LoginViewModelTests
{
    private readonly Mock<IClient> _apiClientMock;
    private readonly Mock<ITokenStorage> _tokenStorageMock;
    private readonly Mock<IAuthNavigation> _navigationMock;
    private readonly LoginViewModel _viewModel;

    public LoginViewModelTests()
    {
        _apiClientMock = new Mock<IClient>();
        _tokenStorageMock = new Mock<ITokenStorage>();
        _navigationMock = new Mock<IAuthNavigation>();

        _viewModel = new LoginViewModel(
            _apiClientMock.Object, 
            _tokenStorageMock.Object, 
            _navigationMock.Object
        );
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_SavesTokensAndNavigates()
    {
        // Arrange
        _viewModel.Email = "test@example.com";
        _viewModel.Password = "password123";

        var loginResponse = new AuthResponse
        { 
            AccessToken = "fake_access", 
            RefreshToken = "fake_refresh" 
        };

        var userProfile = new UserDto { Role = "manager" };

        _apiClientMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(loginResponse);

        _apiClientMock
            .Setup(x => x.MeAsync())
            .ReturnsAsync(userProfile);

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _tokenStorageMock.Verify(x => x.SaveTokensAsync("fake_access", "fake_refresh"), Times.Once);
        _tokenStorageMock.Verify(x => x.SetRoleAsync("manager"), Times.Once);
        _navigationMock.Verify(x => x.NavigateToMainAsync(), Times.Once);
        _viewModel.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_ApiThrowsException_SetsErrorMessageAndDoesNotNavigate()
    {
        // Arrange
        _viewModel.Email = "test@example.com";
        _viewModel.Password = "wrong_password";

        _apiClientMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new Exception("Invalid credentials")); 

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _tokenStorageMock.Verify(x => x.SaveTokensAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        
        _navigationMock.Verify(x => x.NavigateToMainAsync(), Times.Never);
        
        _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_EmptyCredentials_DoesNotCallApi()
    {
        // Arrange
        _viewModel.Email = "";
        _viewModel.Password = "";

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _apiClientMock.Verify(x => x.LoginAsync(It.IsAny<LoginRequest>()), Times.Never);
        
        _navigationMock.Verify(x => x.NavigateToMainAsync(), Times.Never);
    }
}