using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<IClient> _apiClientMock = new();
    private readonly Mock<ITokenStorage> _tokenStorageMock = new();
    private readonly Mock<IAuthNavigation> _navigationMock = new();
    private readonly Mock<IThemeService> _themeMock = new();

    public SettingsViewModelTests()
    {
        _themeMock.SetupGet(x => x.Current).Returns("dark");
        _tokenStorageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync("refresh-token");
    }

    [Fact]
    public void Constructor_ShowsSavedThemeWithoutReapplyingIt()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedTheme.Should().Be("dark");
        _themeMock.Verify(x => x.Apply(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SelectedTheme_WhenChanged_AppliesTheme()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedTheme = "light";

        _themeMock.Verify(x => x.Apply("light"), Times.Once);
    }

    [Fact]
    public async Task Load_ShowsCurrentUser()
    {
        _apiClientMock.Setup(x => x.MeAsync())
            .ReturnsAsync(new UserDto { Name = "Olha Yezerska", Email = "olha@ordersense.test", Role = "analyst" });
        var viewModel = CreateViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.Name.Should().Be("Olha Yezerska");
        viewModel.Email.Should().Be("olha@ordersense.test");
        viewModel.Role.Should().Be("analyst");
        viewModel.Initials.Should().Be("OY");
        viewModel.HasUser.Should().BeTrue();
        viewModel.HasProfileError.Should().BeFalse();
        viewModel.IsProfileSkeletonVisible.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "admin@ordersense.test", "AO")]
    [InlineData("", "manager", "M")]
    [InlineData("", "", "?")]
    public void Initials_WithoutName_UseEmail(string name, string email, string expected)
    {
        var viewModel = CreateViewModel();

        viewModel.Name = name;
        viewModel.Email = email;

        viewModel.Initials.Should().Be(expected);
    }

    [Fact]
    public async Task Load_WhenProfileFails_ShowsErrorAndSavedRole()
    {
        _apiClientMock.Setup(x => x.MeAsync()).ThrowsAsync(new HttpRequestException("offline"));
        _tokenStorageMock.Setup(x => x.GetRoleAsync()).ReturnsAsync("manager");
        var viewModel = CreateViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.HasProfileError.Should().BeTrue();
        viewModel.ProfileError.Should().Be("Немає підключення до сервера. Перевірте мережу.");
        viewModel.Role.Should().Be("manager");
        viewModel.HasUser.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_RevokesTokenClearsSessionAndOpensLogin()
    {
        _apiClientMock.Setup(x => x.MeAsync())
            .ReturnsAsync(new UserDto { Name = "Admin", Email = "admin@ordersense.test", Role = "admin" });
        var viewModel = CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.LogoutCommand.ExecuteAsync(null);

        _apiClientMock.Verify(x => x.LogoutAsync(It.Is<RefreshRequest>(r => r.RefreshToken == "refresh-token")), Times.Once);
        _tokenStorageMock.Verify(x => x.ClearTokensAsync(), Times.Once);
        _navigationMock.Verify(x => x.NavigateToLoginAsync(), Times.Once);
        viewModel.HasUser.Should().BeFalse();
        viewModel.HasRole.Should().BeFalse();
        viewModel.IsLoggingOut.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_WhenServerFails_StillLogsOutLocally()
    {
        _apiClientMock.Setup(x => x.LogoutAsync(It.IsAny<RefreshRequest>()))
            .ThrowsAsync(new ApiException("Server error", 500, null, null, null));
        var viewModel = CreateViewModel();

        await viewModel.LogoutCommand.ExecuteAsync(null);

        _tokenStorageMock.Verify(x => x.ClearTokensAsync(), Times.Once);
        _navigationMock.Verify(x => x.NavigateToLoginAsync(), Times.Once);
    }

    [Fact]
    public async Task Logout_WithoutRefreshToken_SkipsServerCall()
    {
        _tokenStorageMock.Setup(x => x.GetRefreshTokenAsync()).ReturnsAsync((string?)null);
        var viewModel = CreateViewModel();

        await viewModel.LogoutCommand.ExecuteAsync(null);

        _apiClientMock.Verify(x => x.LogoutAsync(It.IsAny<RefreshRequest>()), Times.Never);
        _navigationMock.Verify(x => x.NavigateToLoginAsync(), Times.Once);
    }

    private SettingsViewModel CreateViewModel() =>
        new(_apiClientMock.Object, _tokenStorageMock.Object, _navigationMock.Object, _themeMock.Object);
}
