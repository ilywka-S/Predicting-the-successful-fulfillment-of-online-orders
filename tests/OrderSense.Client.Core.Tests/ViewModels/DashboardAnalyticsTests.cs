using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class DashboardAnalyticsTests
{
    private readonly Mock<ISignalRService> _signalRMock = new();
    private readonly Mock<IAnalyticsService> _analyticsMock = new();
    private readonly List<(AnalyticsDimension Dimension, DateTime? From, DateTime? To, int Top)> _requests = [];
    private readonly DashboardViewModel _viewModel;

    private Func<AnalyticsDimension, IReadOnlyList<AnalyticsPointDto>> _respond = Sample;

    public DashboardAnalyticsTests()
    {
        _analyticsMock
            .Setup(x => x.GetRiskAsync(It.IsAny<AnalyticsDimension>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((AnalyticsDimension dimension, DateTime? from, DateTime? to, int top, CancellationToken _) =>
            {
                _requests.Add((dimension, from, to, top));
                return Task.FromResult(_respond(dimension));
            });

        _viewModel = new DashboardViewModel(_signalRMock.Object, _analyticsMock.Object);
    }

    [Fact]
    public async Task Load_FillsAllChartsAndTotals()
    {
        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);

        _requests.Select(r => r.Dimension).Should().BeEquivalentTo(
            [AnalyticsDimension.Day, AnalyticsDimension.Category, AnalyticsDimension.State, AnalyticsDimension.Seller]);
        _requests.Should().OnlyContain(r => r.From == null && r.To == null && r.Top == DashboardViewModel.TopCount);
        _viewModel.RiskByDay.Should().HaveCount(2);
        _viewModel.RiskByCategory.Should().ContainSingle();
        _viewModel.RiskByState.Should().ContainSingle();
        _viewModel.RiskBySeller.Should().ContainSingle();
        _viewModel.TotalOrders.Should().Be(200);
        _viewModel.PredictedOrders.Should().Be(180);
        _viewModel.RiskyOrders.Should().Be(45);
        _viewModel.RiskyShare.Should().Be(0.25);
        _viewModel.IsAnalyticsVisible.Should().BeTrue();
        _viewModel.IsAnalyticsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task PeriodChange_ReloadsWithDates()
    {
        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);
        _requests.Clear();

        _viewModel.PeriodTo = new DateTime(2018, 3, 31);
        await Task.Delay(20);

        _requests.Should().HaveCount(4).And.OnlyContain(r => r.From == null && r.To == new DateTime(2018, 3, 31));
        _viewModel.HasPeriod.Should().BeTrue();
    }

    [Fact]
    public async Task PeriodChange_BeforeFirstLoad_DoesNotRequest()
    {
        _viewModel.PeriodFrom = new DateTime(2018, 1, 1);
        await Task.Delay(20);

        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ReversedPeriod_IsSentInOrder()
    {
        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);
        _viewModel.PeriodFrom = new DateTime(2018, 6, 1);
        _viewModel.PeriodTo = new DateTime(2018, 1, 1);
        await Task.Delay(20);

        _requests[^1].From.Should().Be(new DateTime(2018, 1, 1));
        _requests[^1].To.Should().Be(new DateTime(2018, 6, 1));
    }

    [Fact]
    public async Task ResetPeriod_ClearsDatesWithOneReload()
    {
        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);
        _viewModel.PeriodFrom = new DateTime(2018, 1, 1);
        await Task.Delay(20);
        _requests.Clear();

        _viewModel.ResetPeriodCommand.Execute(null);
        await Task.Delay(20);

        _requests.Should().HaveCount(4).And.OnlyContain(r => r.From == null && r.To == null);
        _viewModel.HasPeriod.Should().BeFalse();
    }

    [Fact]
    public async Task EmptyPeriod_ShowsEmptyState()
    {
        _respond = _ => [];

        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);

        _viewModel.IsAnalyticsEmpty.Should().BeTrue();
        _viewModel.IsAnalyticsVisible.Should().BeFalse();
        _viewModel.RiskyShare.Should().BeNull();
    }

    [Fact]
    public async Task Error_ShowsMessageAndRetryRecovers()
    {
        _respond = _ => throw new HttpRequestException();

        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);

        _viewModel.AnalyticsError.Should().Be("Немає підключення до сервера. Перевірте мережу.");
        _viewModel.IsAnalyticsVisible.Should().BeFalse();

        _respond = Sample;
        await _viewModel.LoadAnalyticsCommand.ExecuteAsync(null);

        _viewModel.HasAnalyticsError.Should().BeFalse();
        _viewModel.IsAnalyticsVisible.Should().BeTrue();
    }

    private static IReadOnlyList<AnalyticsPointDto> Sample(AnalyticsDimension dimension) => dimension == AnalyticsDimension.Day
        ?
        [
            Point("2018-01-01", orders: 120, predicted: 100, medium: 20, high: 5),
            Point("2018-01-02", orders: 80, predicted: 80, medium: 15, high: 5),
        ]
        : [Point("SP", orders: 200, predicted: 180, medium: 35, high: 10)];

    private static AnalyticsPointDto Point(string key, int orders, int predicted, int medium, int high) => new()
    {
        Key = key,
        Orders = orders,
        Predicted = predicted,
        Low = predicted - medium - high,
        Medium = medium,
        High = high,
        RiskyShare = (double)(medium + high) / predicted,
    };
}
