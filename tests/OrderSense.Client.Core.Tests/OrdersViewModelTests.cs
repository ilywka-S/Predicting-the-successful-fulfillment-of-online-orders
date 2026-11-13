using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests;

public class OrdersViewModelTests
{
    private readonly Mock<IClient> _apiClientMock = new();
    private readonly Mock<IOrderNavigation> _navigationMock = new();
    private readonly List<OrdersRequest> _requests = [];
    private readonly OrdersViewModel _viewModel;

    private Func<OrdersRequest, Task<PagedResponseOfOrderListItemDto>> _respond;

    public OrdersViewModelTests()
    {
        _respond = request => Task.FromResult(Page(request.Page, total: 45));

        _apiClientMock
            .Setup(x => x.GetOrdersAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(),
                It.IsAny<RiskLevel?>(), It.IsAny<string>(), It.IsAny<OrderSort?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string status, DateTimeOffset? from, DateTimeOffset? to, string state, RiskLevel? risk, string search,
                OrderSort? sort, int? page, int? pageSize, CancellationToken _) =>
            {
                var request = new OrdersRequest(status, from, to, state, risk, search, sort, page ?? 1, pageSize ?? 0);
                _requests.Add(request);
                return _respond(request);
            });

        _viewModel = new OrdersViewModel(_apiClientMock.Object, _navigationMock.Object)
        {
            SearchDelay = TimeSpan.FromMilliseconds(30),
        };
    }

    [Fact]
    public async Task Load_FirstPage_ShowsOrdersAndKnowsThereIsMore()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Orders.Should().HaveCount(OrdersViewModel.PageSize);
        _viewModel.TotalCount.Should().Be(45);
        _viewModel.HasMore.Should().BeTrue();
        _viewModel.IsLoading.Should().BeFalse();
        _viewModel.HasError.Should().BeFalse();
        _requests.Should().ContainSingle().Which.Should().Be(
            new OrdersRequest(null, null, null, null, null, null, OrderSort.DateDesc, 1, OrdersViewModel.PageSize));
    }

    [Fact]
    public async Task Load_SecondTime_DoesNotRequestAgain()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _requests.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadMore_AppendsPagesUntilTheEnd()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        _viewModel.Orders.Should().HaveCount(45);
        _viewModel.Orders.Select(o => o.Id).Should().OnlyHaveUniqueItems();
        _viewModel.HasMore.Should().BeFalse();
        _viewModel.CanLoadMore.Should().BeFalse();
        _requests.Select(r => r.Page).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task LoadMore_Fails_KeepsLoadedOrdersAndAllowsRetry()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _respond = _ => throw new HttpRequestException();

        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        _viewModel.Orders.Should().HaveCount(OrdersViewModel.PageSize);
        _viewModel.HasLoadMoreError.Should().BeTrue();
        _viewModel.HasError.Should().BeFalse();

        _respond = request => Task.FromResult(Page(request.Page, total: 45));
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);

        _viewModel.Orders.Should().HaveCount(40);
        _viewModel.HasLoadMoreError.Should().BeFalse();
    }

    [Fact]
    public async Task Load_ServerUnavailable_ShowsErrorAndRetryRecovers()
    {
        _respond = _ => throw new ApiException("Unavailable", 503, null, null, null);

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Be("Сервер тимчасово недоступний. Спробуйте пізніше.");
        _viewModel.IsListVisible.Should().BeFalse();
        _viewModel.Orders.Should().BeEmpty();

        _respond = request => Task.FromResult(Page(request.Page, total: 3));
        await _viewModel.RetryCommand.ExecuteAsync(null);

        _viewModel.HasError.Should().BeFalse();
        _viewModel.IsListVisible.Should().BeTrue();
        _viewModel.Orders.Should().HaveCount(3);
    }

    [Fact]
    public async Task Load_NoConnection_ShowsNetworkError()
    {
        _respond = _ => throw new HttpRequestException();

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Немає підключення до сервера. Перевірте мережу.");
    }

    [Fact]
    public async Task Filters_AreSentToApi()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.SelectedStatus = _viewModel.StatusOptions.Single(o => o.Value == "delivered");
        _viewModel.SelectedState = _viewModel.StateOptions.Single(o => o.Value == "SP");
        _viewModel.RiskFilter = "high";
        _viewModel.DateFrom = new DateTime(2017, 12, 31);
        _viewModel.DateTo = new DateTime(2017, 1, 1);
        _viewModel.SearchText = "  #e481f51c  ";
        _viewModel.SearchCommand.Execute(null);
        _viewModel.ToggleSortCommand.Execute(null);

        _requests[^1].Should().Be(new OrdersRequest(
            "delivered",
            new DateTimeOffset(2017, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2017, 12, 31, 0, 0, 0, TimeSpan.Zero),
            "SP",
            RiskLevel.High,
            "e481f51c",
            OrderSort.DateAsc,
            1,
            OrdersViewModel.PageSize));
        _viewModel.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public async Task FilterChange_BeforeFirstLoad_DoesNotRequest()
    {
        _viewModel.RiskFilter = "low";
        _viewModel.SelectedStatus = null;

        await Task.Delay(50);

        _requests.Should().BeEmpty();
        _viewModel.SelectedStatus.Should().Be(_viewModel.StatusOptions[0]);
    }

    [Fact]
    public async Task Search_WhileTyping_SendsOneRequestWithLastText()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.SearchText = "a";
        _viewModel.SearchText = "ab";
        _viewModel.SearchText = "abc";
        await Task.Delay(300);

        _requests.Should().HaveCount(2);
        _requests[^1].Search.Should().Be("abc");
    }

    [Fact]
    public async Task ResetFilters_ClearsEverythingWithOneRequest()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.RiskFilter = "medium";
        _viewModel.SelectedState = _viewModel.StateOptions.Single(o => o.Value == "RJ");
        var before = _requests.Count;

        _viewModel.ResetFiltersCommand.Execute(null);

        _requests.Should().HaveCount(before + 1);
        _requests[^1].Risk.Should().BeNull();
        _requests[^1].State.Should().BeNull();
        _viewModel.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public async Task SlowOldResponse_DoesNotOverwriteNewerResult()
    {
        var slow = new TaskCompletionSource<PagedResponseOfOrderListItemDto>();
        _respond = request => request.Risk == RiskLevel.Low
            ? slow.Task
            : Task.FromResult(Page(request.Page, total: 2, prefix: "new"));

        _viewModel.RiskFilter = "low";
        var first = _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.RiskFilter = "high";
        slow.SetResult(Page(1, total: 30, prefix: "old"));
        await first;

        _viewModel.Orders.Select(o => o.Id).Should().Equal("new-1", "new-2");
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_ReplacesListWithoutSkeleton()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreCommand.ExecuteAsync(null);
        _respond = request => Task.FromResult(Page(request.Page, total: 5, prefix: "fresh"));

        _viewModel.IsRefreshing = true;
        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Orders.Should().HaveCount(5);
        _viewModel.Orders[0].Id.Should().Be("fresh-1");
        _viewModel.IsRefreshing.Should().BeFalse();
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task OpenOrder_NavigatesToOrder()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);

        await _viewModel.OpenOrderCommand.ExecuteAsync(_viewModel.Orders[0]);

        _navigationMock.Verify(x => x.OpenOrderAsync("order-1"), Times.Once);
    }

    private static PagedResponseOfOrderListItemDto Page(int page, int total, string prefix = "order")
    {
        var first = ((page - 1) * OrdersViewModel.PageSize) + 1;
        var count = Math.Clamp(total - first + 1, 0, OrdersViewModel.PageSize);

        return new PagedResponseOfOrderListItemDto
        {
            Page = page,
            PageSize = OrdersViewModel.PageSize,
            Total = total,
            Items = Enumerable.Range(first, count)
                .Select(n => new OrderListItemDto
                {
                    Id = $"{prefix}-{n}",
                    PurchasedAt = new DateTimeOffset(2018, 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(-n),
                    Status = "delivered",
                    CustomerState = "SP",
                    ItemsCount = 1,
                    TotalValue = 100,
                })
                .ToList(),
        };
    }

    private sealed record OrdersRequest(
        string? Status,
        DateTimeOffset? From,
        DateTimeOffset? To,
        string? State,
        RiskLevel? Risk,
        string? Search,
        OrderSort? Sort,
        int Page,
        int PageSize);
}
