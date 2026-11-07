using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;
using Xunit;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class LoadingViewModelTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange
        var tokenStorageMock = new Mock<ITokenStorage>();
        var authNavigationMock = new Mock<IAuthNavigation>();

        // Act
        var viewModel = new LoadingViewModel(tokenStorageMock.Object, authNavigationMock.Object);

        // Assert
        Assert.NotNull(viewModel);
    }
}