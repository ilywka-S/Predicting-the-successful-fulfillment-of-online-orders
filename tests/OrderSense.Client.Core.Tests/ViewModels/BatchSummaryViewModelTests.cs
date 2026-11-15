using FluentAssertions;
using Moq;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class BatchSummaryViewModelTests
{
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Mock<IClient> _apiClientMock = new();
    private readonly BatchSummaryViewModel _viewModel;

    public BatchSummaryViewModelTests()
    {
        _viewModel = new BatchSummaryViewModel(_apiClientMock.Object);
    }

    [Fact]
    public async Task Open_LoadsSummaryAndBuildsSections()
    {
        _apiClientMock.Setup(x => x.GetBatchJobSummaryAsync(_jobId, BatchSummaryViewModel.Top)).ReturnsAsync(Summary());

        await _viewModel.OpenAsync(_jobId);

        _viewModel.IsContentVisible.Should().BeTrue();
        _viewModel.IsEmpty.Should().BeFalse();
        _viewModel.RiskyShare.Should().BeApproximately(0.4, 0.0001);
        _viewModel.RiskLevels.Select(r => (r.Level, r.Rows)).Should().Equal(("low", 6), ("medium", 3), ("high", 1));
        _viewModel.RiskLevels[0].Share.Should().BeApproximately(0.6, 0.0001);
        _viewModel.Factors.Select(f => f.Label).Should().Equal("Обіцяний термін доставки", "Штат продавця");
        _viewModel.Factors[0].Share.Should().BeApproximately(0.75, 0.0001);
        _viewModel.ByCategory.Select(p => p.Key).Should().Equal("cama_mesa_banho", "unknown");
        _viewModel.ByCustomerState.Should().ContainSingle();
        _viewModel.ByPaymentType.Should().ContainSingle();
    }

    [Fact]
    public async Task Open_WithoutPredictions_ShowsEmptyState()
    {
        var summary = new BatchJobSummaryDto { JobId = _jobId, FileName = "orders.csv", TotalRows = 3 };
        _apiClientMock.Setup(x => x.GetBatchJobSummaryAsync(_jobId, It.IsAny<int?>())).ReturnsAsync(summary);

        await _viewModel.OpenAsync(_jobId);

        _viewModel.IsEmpty.Should().BeTrue();
        _viewModel.IsContentVisible.Should().BeFalse();
        _viewModel.RiskyShare.Should().BeNull();
        _viewModel.RiskLevels.Should().OnlyContain(r => r.Share == 0);
    }

    [Fact]
    public async Task Open_UnknownJob_ShowsNotFoundMessage()
    {
        _apiClientMock.Setup(x => x.GetBatchJobSummaryAsync(_jobId, It.IsAny<int?>()))
            .ThrowsAsync(new ApiException("Not found", 404, null, null, null));

        await _viewModel.OpenAsync(_jobId);

        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Be("Пакет не знайдено або він вам недоступний.");
        _viewModel.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Reload_AfterNetworkError_RetriesSameJob()
    {
        _apiClientMock.SetupSequence(x => x.GetBatchJobSummaryAsync(_jobId, It.IsAny<int?>()))
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(Summary());

        await _viewModel.OpenAsync(_jobId);
        _viewModel.ErrorMessage.Should().Be("Немає підключення до сервера. Перевірте мережу.");

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.HasError.Should().BeFalse();
        _viewModel.IsContentVisible.Should().BeTrue();
        _apiClientMock.Verify(x => x.GetBatchJobSummaryAsync(_jobId, It.IsAny<int?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Load_BeforeOpen_DoesNothing()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _apiClientMock.Verify(x => x.GetBatchJobSummaryAsync(It.IsAny<Guid>(), It.IsAny<int?>()), Times.Never);
        _viewModel.IsLoading.Should().BeFalse();
    }

    private BatchJobSummaryDto Summary() => new()
    {
        JobId = _jobId,
        FileName = "orders-november.csv",
        TotalRows = 12,
        PredictedRows = 10,
        Low = 6,
        Medium = 3,
        High = 1,
        AverageProbability = 0.08,
        ByCategory =
        [
            new AnalyticsPointDto { Key = "cama_mesa_banho", Orders = 8, Predicted = 7, Low = 4, Medium = 2, High = 1, RiskyShare = 3 / 7.0 },
            new AnalyticsPointDto { Key = "unknown", Orders = 4, Predicted = 3, Low = 2, Medium = 1, RiskyShare = 1 / 3.0 },
        ],
        ByCustomerState = [new AnalyticsPointDto { Key = "SP", Orders = 12, Predicted = 10, Low = 6, Medium = 3, High = 1, RiskyShare = 0.4 }],
        ByPaymentType = [new AnalyticsPointDto { Key = "credit_card", Orders = 12, Predicted = 10, Low = 6, Medium = 3, High = 1, RiskyShare = 0.4 }],
        TopFactors =
        [
            new BatchFactorCountDto { Feature = "PromisedDays", Label = "Обіцяний термін доставки", Rows = 3 },
            new BatchFactorCountDto { Feature = "SellerState", Label = "Штат продавця", Rows = 1 },
        ],
    };
}
