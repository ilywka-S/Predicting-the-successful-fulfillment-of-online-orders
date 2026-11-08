using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.Services;

namespace OrderSense.Client.Core.Tests.Services;

public class ApiServicesTests
{
    private readonly Mock<IClient> _apiClientMock = new(MockBehavior.Strict);

    [Fact]
    public async Task Analytics_ByDay_CallsDayEndpointWithDates()
    {
        var from = new DateTimeOffset(2018, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2018, 3, 31, 0, 0, 0, TimeSpan.Zero);
        _apiClientMock
            .Setup(x => x.GetRiskByDayAsync(from, to, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AnalyticsPointDto { Key = "2018-01-01" }]);
        var service = new ApiAnalyticsService(_apiClientMock.Object);

        var points = await service.GetRiskAsync(AnalyticsDimension.Day, new DateTime(2018, 1, 1, 15, 0, 0), new DateTime(2018, 3, 31), 10);

        points.Should().ContainSingle().Which.Key.Should().Be("2018-01-01");
    }

    [Theory]
    [InlineData(AnalyticsDimension.Category)]
    [InlineData(AnalyticsDimension.State)]
    [InlineData(AnalyticsDimension.Seller)]
    public async Task Analytics_TopLists_PassTop(AnalyticsDimension dimension)
    {
        _apiClientMock
            .Setup(x => x.GetRiskByCategoryAsync(null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AnalyticsPointDto { Key = "category" }]);
        _apiClientMock
            .Setup(x => x.GetRiskByStateAsync(null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AnalyticsPointDto { Key = "state" }]);
        _apiClientMock
            .Setup(x => x.GetRiskBySellerAsync(null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AnalyticsPointDto { Key = "seller" }]);
        var service = new ApiAnalyticsService(_apiClientMock.Object);

        var points = await service.GetRiskAsync(dimension, null, null, 10);

        points.Single().Key.Should().Be(dimension.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task WhatIf_PostsRequestToOrder()
    {
        var request = new WhatIfRequest { PromisedDays = 30 };
        var result = new WhatIfResultDto();
        _apiClientMock
            .Setup(x => x.WhatIfAsync("a1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var service = new ApiWhatIfService(_apiClientMock.Object);

        var response = await service.RunAsync("a1", request);

        response.Should().BeSameAs(result);
    }
}
