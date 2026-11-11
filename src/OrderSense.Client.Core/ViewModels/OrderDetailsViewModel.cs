using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class OrderDetailsViewModel : ObservableObject
{
    public const int HistoryLimit = 10;

    private readonly IClient _apiClient;
    private readonly IOrderNavigation _navigation;

    private CancellationTokenSource? _loadCts;

    public OrderDetailsViewModel(IClient apiClient, IOrderNavigation navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
    }

    public ObservableCollection<OrderItemDto> Items { get; } = [];

    public ObservableCollection<PaymentDto> Payments { get; } = [];

    public ObservableCollection<RiskFactorDto> Factors { get; } = [];

    public ObservableCollection<PredictionHistoryItemDto> History { get; } = [];

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrder), nameof(IsContentVisible), nameof(IsDelivered), nameof(IsDeliveredLate))]
    [NotifyCanExecuteChangedFor(nameof(PredictCommand), nameof(OpenWhatIfCommand))]
    private OrderDetailsDto? _order;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrediction))]
    private PredictionDto? _latestPrediction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyCanExecuteChangedFor(nameof(PredictCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsContentVisible))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PredictCommand))]
    private bool _isPredicting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPredictError))]
    private string _predictErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistoryError))]
    private string _historyErrorMessage = string.Empty;

    public bool HasOrder => Order is not null;

    public bool HasPrediction => LatestPrediction is not null;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasPredictError => !string.IsNullOrEmpty(PredictErrorMessage);

    public bool HasHistoryError => !string.IsNullOrEmpty(HistoryErrorMessage);

    public bool IsContentVisible => HasOrder && !IsLoading && !HasError;

    public bool HasFactors => Factors.Count > 0;

    public bool HasHistory => History.Count > 0;

    public double ItemsTotal => Items.Sum(item => item.Price);

    public double FreightTotal => Items.Sum(item => item.FreightValue);

    public double OrderTotal => ItemsTotal + FreightTotal;

    public bool IsDelivered => Order?.DeliveredCustomerAt is not null;

    public bool IsDeliveredLate =>
        Order is { DeliveredCustomerAt: { } delivered } order && delivered.Date > order.EstimatedDeliveryAt.Date;

    [RelayCommand]
    private async Task LoadAsync(string? orderId)
    {
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            OrderId = orderId;
        }

        if (string.IsNullOrWhiteSpace(OrderId))
        {
            return;
        }

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        IsLoading = true;
        ErrorMessage = string.Empty;
        PredictErrorMessage = string.Empty;
        HistoryErrorMessage = string.Empty;

        try
        {
            var orderTask = _apiClient.GetOrderAsync(OrderId, cts.Token);
            var historyTask = LoadHistoryAsync(OrderId, cts.Token);
            await Task.WhenAll(orderTask, historyTask);

            if (cts == _loadCts)
            {
                ApplyOrder(await orderTask);
                ApplyHistory(await historyTask);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (cts == _loadCts)
            {
                ErrorMessage = ToLoadMessage(exception);
            }
        }
        finally
        {
            if (cts == _loadCts)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync(null);

    [RelayCommand(CanExecute = nameof(CanPredict))]
    private async Task PredictAsync()
    {
        IsPredicting = true;
        PredictErrorMessage = string.Empty;

        try
        {
            var prediction = await _apiClient.PredictOrderAsync(OrderId);
            ApplyPrediction(prediction);
        }
        catch (Exception exception)
        {
            PredictErrorMessage = ToPredictMessage(exception);
        }
        finally
        {
            IsPredicting = false;
        }
    }

    private bool CanPredict() => HasOrder && !IsLoading && !IsPredicting;

    [RelayCommand(CanExecute = nameof(HasOrder))]
    private Task OpenWhatIfAsync() => _navigation.OpenWhatIfAsync(OrderId);

    private void ApplyOrder(OrderDetailsDto order)
    {
        Items.Clear();
        foreach (var item in order.Items)
        {
            Items.Add(item);
        }

        Payments.Clear();
        foreach (var payment in order.Payments)
        {
            Payments.Add(payment);
        }

        History.Clear();
        Factors.Clear();
        LatestPrediction = null;

        Order = order;

        if (order.LatestPrediction is { } prediction)
        {
            ApplyPrediction(prediction);
        }

        OnPropertyChanged(nameof(ItemsTotal));
        OnPropertyChanged(nameof(FreightTotal));
        OnPropertyChanged(nameof(OrderTotal));
        NotifyPrediction();
    }

    private async Task<IReadOnlyList<PredictionHistoryItemDto>?> LoadHistoryAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            var history = await _apiClient.GetOrderPredictionsAsync(orderId, cancellationToken);
            return history?.ToList() ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"[OrderDetails] Не вдалося завантажити історію прогнозів: {exception.Message}");
            return null;
        }
    }

    private void ApplyHistory(IReadOnlyList<PredictionHistoryItemDto>? items)
    {
        if (items is null)
        {
            HistoryErrorMessage = "Не вдалося завантажити історію прогнозів, показано лише останній.";
            return;
        }

        History.Clear();
        foreach (var item in items)
        {
            History.Add(item);
        }

        if (LatestPrediction is { } latest)
        {
            AddToHistory(latest);
        }

        TrimHistory();
        NotifyPrediction();
    }

    private void ApplyPrediction(PredictionDto prediction)
    {
        LatestPrediction = prediction;

        Factors.Clear();
        foreach (var factor in prediction.Factors)
        {
            Factors.Add(factor);
        }

        AddToHistory(prediction);
        NotifyPrediction();
    }

    private void AddToHistory(PredictionDto prediction)
    {
        if (!History.Any(item => IsSamePrediction(item, prediction)))
        {
            History.Insert(0, ToHistoryItem(prediction));
        }

        TrimHistory();
    }

    private void TrimHistory()
    {
        while (History.Count > HistoryLimit)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    private static bool IsSamePrediction(PredictionHistoryItemDto item, PredictionDto prediction) =>
        item.CreatedAt == prediction.CreatedAt && item.ModelVersion == prediction.ModelVersion;

    private static PredictionHistoryItemDto ToHistoryItem(PredictionDto prediction) => new()
    {
        CreatedAt = prediction.CreatedAt,
        ModelVersion = prediction.ModelVersion,
        Probability = prediction.Probability,
        RiskLevel = prediction.RiskLevel,
    };

    private void NotifyPrediction()
    {
        OnPropertyChanged(nameof(HasFactors));
        OnPropertyChanged(nameof(HasHistory));
    }

    private static string ToLoadMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 404 } => "Замовлення не знайдено.",
        ApiException { StatusCode: 401 or 403 } => "Немає доступу. Увійдіть знову.",
        ApiException { StatusCode: >= 500 } => "Сервер тимчасово недоступний. Спробуйте пізніше.",
        HttpRequestException => "Немає підключення до сервера. Перевірте мережу.",
        OperationCanceledException => "Сервер не відповідає. Спробуйте ще раз.",
        _ => "Не вдалося завантажити замовлення.",
    };

    private static string ToPredictMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 503 } => "Сервіс прогнозування тимчасово недоступний. Спробуйте за хвилину.",
        ApiException { StatusCode: 422 } => "Для цього замовлення не вистачає даних для прогнозу.",
        ApiException { StatusCode: 404 } => "Замовлення не знайдено.",
        ApiException { StatusCode: 401 or 403 } => "Немає доступу. Увійдіть знову.",
        ApiException { StatusCode: >= 500 } => "Сервер тимчасово недоступний. Спробуйте пізніше.",
        HttpRequestException => "Немає підключення до сервера. Перевірте мережу.",
        OperationCanceledException => "Сервер не відповідає. Спробуйте ще раз.",
        _ => "Не вдалося отримати прогноз.",
    };
}
