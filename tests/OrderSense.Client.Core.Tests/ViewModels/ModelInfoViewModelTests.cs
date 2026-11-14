using FluentAssertions;
using Moq;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class ModelInfoViewModelTests
{
    private readonly Mock<IClient> _apiClientMock = new();
    private readonly ModelInfoViewModel _viewModel;

    public ModelInfoViewModelTests()
    {
        _viewModel = new ModelInfoViewModel(_apiClientMock.Object);
    }

    [Fact]
    public async Task Load_ShowsModelWithImportancesFromMostImportant()
    {
        _apiClientMock.Setup(x => x.GetModelAsync()).ReturnsAsync(Model());

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.IsContentVisible.Should().BeTrue();
        _viewModel.HasError.Should().BeFalse();
        _viewModel.Model!.Version.Should().Be("v2");
        _viewModel.Importances.Select(f => f.Feature).Should().Equal("PromisedDays", "SellerState", "CustomerState");
        _viewModel.PrAucLift.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public async Task Load_WhenModelInfoMissing_ShowsUnavailableMessage()
    {
        _apiClientMock.Setup(x => x.GetModelAsync()).ThrowsAsync(new ApiException("Not found", 404, null, null, null));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Be("Інформація про модель поки недоступна.");
        _viewModel.IsContentVisible.Should().BeFalse();
    }

    [Fact]
    public async Task Load_WhenPredictionServiceDown_ShowsServiceMessage()
    {
        _apiClientMock.Setup(x => x.GetModelAsync()).ThrowsAsync(new ApiException("Unavailable", 503, null, null, null));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Сервіс прогнозування тимчасово недоступний. Спробуйте за хвилину.");
    }

    [Fact]
    public async Task Load_WithoutNetwork_ShowsConnectionMessage()
    {
        _apiClientMock.Setup(x => x.GetModelAsync()).ThrowsAsync(new HttpRequestException("offline"));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Немає підключення до сервера. Перевірте мережу.");
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Load_AfterError_ClearsErrorAndShowsModel()
    {
        _apiClientMock.SetupSequence(x => x.GetModelAsync())
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(Model());

        await _viewModel.LoadCommand.ExecuteAsync(null);
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.HasError.Should().BeFalse();
        _viewModel.IsContentVisible.Should().BeTrue();
    }

    [Fact]
    public void PrAucLift_WithoutBaseline_IsEmpty()
    {
        _viewModel.Model = new ModelInfoDto { PrAuc = 0.08f, BaselinePrAuc = 0 };

        _viewModel.PrAucLift.Should().BeNull();
    }

    private static ModelInfoDto Model() => new()
    {
        Version = "v2",
        TrainedAt = new DateTimeOffset(2026, 11, 13, 20, 45, 0, TimeSpan.Zero),
        RocAuc = 0.677f,
        PrAuc = 0.08f,
        F1 = 0.133f,
        BaselinePrAuc = 0.04f,
        TrainRows = 64380,
        TestRows = 18689,
        TrainPeriod = "2017-01 – 2018-03",
        TestPeriod = "2018-06 – 2018-08",
        RiskMediumFrom = 0.09f,
        RiskHighFrom = 0.14f,
        FeatureImportances =
        [
            new FeatureImportanceDto { Feature = "SellerState", Importance = 0.006f },
            new FeatureImportanceDto { Feature = "CustomerState", Importance = -0.006f },
            new FeatureImportanceDto { Feature = "PromisedDays", Importance = 0.041f },
        ],
    };
}
