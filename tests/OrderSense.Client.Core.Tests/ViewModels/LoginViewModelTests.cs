using Xunit;
using Moq;
using FluentAssertions;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Tests.ViewModels;

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
            .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _tokenStorageMock.Verify(x => x.SaveTokensAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        _navigationMock.Verify(x => x.NavigateToMainAsync(), Times.Never);

        _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ShowsInvalidCredentials()
    {
        _viewModel.Email = "test@example.com";
        _viewModel.Password = "WrongPass1";
        SetupLoginFailure("Invalid email or password");

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Невірний email або пароль.");
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ShowsLockoutMessage()
    {
        _viewModel.Email = "test@example.com";
        _viewModel.Password = "Password1";
        SetupLoginFailure("Account is temporarily locked after too many failed attempts");

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Забагато невдалих спроб входу. Спробуйте через 5 хвилин.");
        _navigationMock.Verify(x => x.NavigateToMainAsync(), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ServerError_ShowsServerMessage()
    {
        _viewModel.Email = "test@example.com";
        _viewModel.Password = "Password1";
        _apiClientMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new ApiException("Server error", 500, null, null, null));

        await _viewModel.LoginCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Помилка сервера. Спробуйте пізніше.");
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

    private void SetupLoginFailure(string title) =>
        _apiClientMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new ApiException<ProblemDetails>("Unauthorized", 401, null, null, new ProblemDetails { Title = title }, null));
}
