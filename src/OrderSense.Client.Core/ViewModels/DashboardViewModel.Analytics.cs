using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class DashboardViewModel
{
    public const int TopCount = 10;

    private readonly IAnalyticsService? _analyticsService;

    private CancellationTokenSource? _analyticsCts;
    private bool _analyticsStarted;

    public DashboardViewModel(ISignalRService signalRService, IAnalyticsService analyticsService)
        : this(signalRService)
    {
        _analyticsService = analyticsService;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPeriod))]
    private DateTime? _periodFrom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPeriod))]
    private DateTime? _periodTo;

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _riskByDay = [];

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _riskByCategory = [];

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _riskByState = [];

    [ObservableProperty]
    private IReadOnlyList<AnalyticsPointDto> _riskBySeller = [];

    [ObservableProperty]
    private int _totalOrders;

    [ObservableProperty]
    private int _predictedOrders;

    [ObservableProperty]
    private int _riskyOrders;

    [ObservableProperty]
    private double? _riskyShare;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalyticsVisible), nameof(IsAnalyticsEmpty))]
    private bool _isAnalyticsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnalyticsError), nameof(IsAnalyticsVisible), nameof(IsAnalyticsEmpty))]
    private string _analyticsError = string.Empty;

    public bool HasPeriod => PeriodFrom is not null || PeriodTo is not null;

    public bool HasAnalyticsError => !string.IsNullOrEmpty(AnalyticsError);

    public bool IsAnalyticsVisible => !IsAnalyticsLoading && !HasAnalyticsError && TotalOrders > 0;

    public bool IsAnalyticsEmpty => !IsAnalyticsLoading && !HasAnalyticsError && TotalOrders == 0;

    partial void OnPeriodFromChanged(DateTime? value) => ReloadForPeriod();

    partial void OnPeriodToChanged(DateTime? value) => ReloadForPeriod();

    partial void OnTotalOrdersChanged(int value)
    {
        OnPropertyChanged(nameof(IsAnalyticsVisible));
        OnPropertyChanged(nameof(IsAnalyticsEmpty));
    }

    [RelayCommand]
    private async Task LoadAnalyticsAsync()
    {
        if (_analyticsService is null)
        {
            return;
        }

        _analyticsStarted = true;
        _analyticsCts?.Cancel();
        var cts = new CancellationTokenSource();
        _analyticsCts = cts;

        IsAnalyticsLoading = true;
        AnalyticsError = string.Empty;

        var (from, to) = PeriodFrom > PeriodTo ? (PeriodTo, PeriodFrom) : (PeriodFrom, PeriodTo);

        try
        {
            var byDay = _analyticsService.GetRiskAsync(AnalyticsDimension.Day, from, to, TopCount, cts.Token);
            var byCategory = _analyticsService.GetRiskAsync(AnalyticsDimension.Category, from, to, TopCount, cts.Token);
            var byState = _analyticsService.GetRiskAsync(AnalyticsDimension.State, from, to, TopCount, cts.Token);
            var bySeller = _analyticsService.GetRiskAsync(AnalyticsDimension.Seller, from, to, TopCount, cts.Token);
            await Task.WhenAll(byDay, byCategory, byState, bySeller);

            if (cts != _analyticsCts)
            {
                return;
            }

            RiskByDay = byDay.Result;
            RiskByCategory = byCategory.Result;
            RiskByState = byState.Result;
            RiskBySeller = bySeller.Result;
            UpdateTotals();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (cts == _analyticsCts)
            {
                AnalyticsError = ApiErrorMessages.For(exception, "Не вдалося завантажити аналітику.");
            }
        }
        finally
        {
            if (cts == _analyticsCts)
            {
                IsAnalyticsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void ResetPeriod()
    {
        _analyticsStarted = false;
        PeriodFrom = null;
        PeriodTo = null;
        _analyticsStarted = true;
        _ = LoadAnalyticsAsync();
    }

    private void ReloadForPeriod()
    {
        if (_analyticsStarted)
        {
            _ = LoadAnalyticsAsync();
        }
    }

    private void UpdateTotals()
    {
        PredictedOrders = RiskByDay.Sum(point => point.Predicted);
        RiskyOrders = RiskByDay.Sum(point => point.Medium + point.High);
        RiskyShare = PredictedOrders > 0 ? (double)RiskyOrders / PredictedOrders : null;
        TotalOrders = RiskByDay.Sum(point => point.Orders);
    }
}
