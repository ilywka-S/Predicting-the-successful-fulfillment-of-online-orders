using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class OrdersViewModel : ObservableObject
{
    public const int PageSize = 20;
    public const string AllRisks = "all";

    private const int SearchMaxLength = 32;

    private static readonly FilterOption[] Statuses =
    [
        new("Усі статуси", null),
        new("Створено", "created"),
        new("Підтверджено", "approved"),
        new("Виставлено рахунок", "invoiced"),
        new("Обробляється", "processing"),
        new("Відправлено", "shipped"),
        new("Доставлено", "delivered"),
        new("Скасовано", "canceled"),
        new("Недоступно", "unavailable"),
    ];

    private static readonly FilterOption[] States =
    [
        new("Усі штати", null),
        .. new[]
        {
            "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA",
            "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
        }.Select(code => new FilterOption(code, code)),
    ];

    private readonly IClient _apiClient;
    private readonly IOrderNavigation _navigation;
    private readonly HashSet<string> _loadedIds = [];

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _searchCts;
    private OrdersQuery? _lastQuery;
    private int _page;
    private bool _started;
    private bool _suspendReload;

    public OrdersViewModel(IClient apiClient, IOrderNavigation navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
    }

    public ObservableCollection<OrderListItemDto> Orders { get; } = [];

    public IReadOnlyList<FilterOption> StatusOptions { get; } = Statuses;

    public IReadOnlyList<FilterOption> StateOptions { get; } = States;

    public TimeSpan SearchDelay { get; set; } = TimeSpan.FromMilliseconds(400);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private FilterOption? _selectedStatus = Statuses[0];

    [ObservableProperty]
    private FilterOption? _selectedState = States[0];

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private string _riskFilter = AllRisks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewestFirst))]
    private OrderSort _sort = OrderSort.DateDesc;

    [ObservableProperty]
    private bool _isFilterPanelOpen;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private bool _isLoadingMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private bool _hasLoadMoreError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsListVisible))]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsListVisible => !IsLoading && !HasError;

    public bool HasMore => Orders.Count < TotalCount;

    public bool CanLoadMore => HasMore && !IsLoadingMore && !HasLoadMoreError;

    public bool IsNewestFirst => Sort == OrderSort.DateDesc;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText)
        || SelectedStatus?.Value is not null
        || SelectedState?.Value is not null
        || DateFrom is not null
        || DateTo is not null
        || RiskFilter != AllRisks;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        _ = ReloadAfterDelayAsync();
    }

    partial void OnSelectedStatusChanged(FilterOption? value)
    {
        if (value is null)
        {
            SelectedStatus = Statuses[0];
            return;
        }

        OnFiltersChanged();
    }

    partial void OnSelectedStateChanged(FilterOption? value)
    {
        if (value is null)
        {
            SelectedState = States[0];
            return;
        }

        OnFiltersChanged();
    }

    partial void OnDateFromChanged(DateTime? value) => OnFiltersChanged();

    partial void OnDateToChanged(DateTime? value) => OnFiltersChanged();

    partial void OnRiskFilterChanged(string value) => OnFiltersChanged();

    partial void OnSortChanged(OrderSort value) => OnFiltersChanged();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_started && !HasError && Orders.Count > 0)
        {
            return;
        }

        _started = true;
        await ReloadAsync(refreshing: false);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RefreshAsync()
    {
        _started = true;
        return ReloadAsync(refreshing: IsRefreshing);
    }

    [RelayCommand]
    private Task RetryAsync() => ReloadAsync(refreshing: false);

    [RelayCommand]
    private void Search()
    {
        _searchCts?.Cancel();
        OnFiltersChanged();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoading || IsRefreshing || IsLoadingMore || HasError || !HasMore || _lastQuery is not { } query || _loadCts is not { } cts)
        {
            return;
        }

        IsLoadingMore = true;
        HasLoadMoreError = false;

        try
        {
            var response = await FetchAsync(query, _page + 1, cts.Token);
            if (cts != _loadCts)
            {
                return;
            }

            _page++;
            AddPage(response);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (cts == _loadCts)
            {
                HasLoadMoreError = true;
            }
        }
        finally
        {
            if (cts == _loadCts)
            {
                IsLoadingMore = false;
            }
        }
    }

    [RelayCommand]
    private void ResetFilters()
    {
        _searchCts?.Cancel();
        _suspendReload = true;

        try
        {
            SearchText = string.Empty;
            SelectedStatus = Statuses[0];
            SelectedState = States[0];
            DateFrom = null;
            DateTo = null;
            RiskFilter = AllRisks;
        }
        finally
        {
            _suspendReload = false;
        }

        OnFiltersChanged();
    }

    [RelayCommand]
    private void ToggleSort() => Sort = IsNewestFirst ? OrderSort.DateAsc : OrderSort.DateDesc;

    [RelayCommand]
    private void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    [RelayCommand]
    private Task OpenOrderAsync(OrderListItemDto? order) =>
        order is null ? Task.CompletedTask : _navigation.OpenOrderAsync(order.Id);

    private void OnFiltersChanged()
    {
        OnPropertyChanged(nameof(HasActiveFilters));

        if (_suspendReload || !_started || (CurrentQuery() == _lastQuery && !HasError))
        {
            return;
        }

        _ = ReloadAsync(refreshing: false);
    }

    private async Task ReloadAfterDelayAsync()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            await Task.Delay(SearchDelay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        OnFiltersChanged();
    }

    private async Task ReloadAsync(bool refreshing)
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        var query = CurrentQuery();
        _lastQuery = query;

        ErrorMessage = string.Empty;
        HasLoadMoreError = false;
        IsLoadingMore = false;
        IsLoading = !refreshing;

        try
        {
            var response = await FetchAsync(query, 1, cts.Token);
            if (cts != _loadCts)
            {
                return;
            }

            Orders.Clear();
            _loadedIds.Clear();
            _page = 1;
            AddPage(response);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (cts == _loadCts)
            {
                Orders.Clear();
                _loadedIds.Clear();
                TotalCount = 0;
                NotifyPaging();
                ErrorMessage = ToMessage(ex);
            }
        }
        finally
        {
            if (cts == _loadCts)
            {
                IsLoading = false;
                IsRefreshing = false;
            }
        }
    }

    private Task<PagedResponseOfOrderListItemDto> FetchAsync(OrdersQuery query, int page, CancellationToken cancellationToken) =>
        _apiClient.GetOrdersAsync(
            query.Status!,
            ToApiDate(query.From),
            ToApiDate(query.To),
            query.State!,
            query.Risk,
            query.Search!,
            query.Sort,
            page,
            PageSize,
            cancellationToken);

    private void AddPage(PagedResponseOfOrderListItemDto response)
    {
        foreach (var order in response.Items)
        {
            if (_loadedIds.Add(order.Id))
            {
                Orders.Add(order);
            }
        }

        TotalCount = response.Items.Count == 0 ? Orders.Count : Math.Max(response.Total, Orders.Count);
        NotifyPaging();
    }

    private void NotifyPaging()
    {
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    private OrdersQuery CurrentQuery()
    {
        var search = SearchText.Trim().TrimStart('#').Trim();
        if (search.Length > SearchMaxLength)
        {
            search = search[..SearchMaxLength];
        }

        var (from, to) = DateFrom > DateTo ? (DateTo, DateFrom) : (DateFrom, DateTo);

        return new OrdersQuery(
            SelectedStatus?.Value,
            from?.Date,
            to?.Date,
            SelectedState?.Value,
            ToRiskLevel(RiskFilter),
            search.Length == 0 ? null : search,
            Sort);
    }

    private static RiskLevel? ToRiskLevel(string risk) => risk switch
    {
        "low" => RiskLevel.Low,
        "medium" => RiskLevel.Medium,
        "high" => RiskLevel.High,
        _ => null,
    };

    private static DateTimeOffset? ToApiDate(DateTime? date) =>
        date is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified), TimeSpan.Zero) : null;

    private static string ToMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 401 or 403 } => "Немає доступу. Увійдіть знову.",
        ApiException { StatusCode: >= 500 } => "Сервер тимчасово недоступний. Спробуйте пізніше.",
        HttpRequestException => "Немає підключення до сервера. Перевірте мережу.",
        OperationCanceledException => "Сервер не відповідає. Спробуйте ще раз.",
        _ => "Не вдалося завантажити замовлення.",
    };

    private sealed record OrdersQuery(
        string? Status,
        DateTime? From,
        DateTime? To,
        string? State,
        RiskLevel? Risk,
        string? Search,
        OrderSort Sort);
}
