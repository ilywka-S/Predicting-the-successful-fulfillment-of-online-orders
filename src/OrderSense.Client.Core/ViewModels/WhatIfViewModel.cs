using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class WhatIfViewModel : ObservableObject
{
    public const int MaxPromisedDays = 180;
    public const int MaxInstallments = 24;

    private static readonly FilterOption[] Payments =
    [
        new("Кредитна картка", "credit_card"),
        new("Дебетова картка", "debit_card"),
        new("Boleto (банківський рахунок)", "boleto"),
        new("Ваучер", "voucher"),
    ];

    private readonly IClient _apiClient;
    private readonly IWhatIfService _whatIfService;

    public WhatIfViewModel(IClient apiClient, IWhatIfService whatIfService)
    {
        _apiClient = apiClient;
        _whatIfService = whatIfService;
    }

    public IReadOnlyList<FilterOption> PaymentOptions { get; } = Payments;

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsContentVisible))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    private bool _isOrderLoaded;

    [ObservableProperty]
    private int _currentPromisedDays;

    [ObservableProperty]
    private FilterOption? _currentPayment;

    [ObservableProperty]
    private int _currentInstallments;

    [ObservableProperty]
    private string _currentSellerId = string.Empty;

    [ObservableProperty]
    private string _promisedDaysText = string.Empty;

    [ObservableProperty]
    private FilterOption? _selectedPayment;

    [ObservableProperty]
    private string _installmentsText = string.Empty;

    [ObservableProperty]
    private string _sellerId = string.Empty;

    [ObservableProperty]
    private string _promisedDaysError = string.Empty;

    [ObservableProperty]
    private string _installmentsError = string.Empty;

    [ObservableProperty]
    private string _sellerIdError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunError))]
    private string _runError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(DeltaPoints), nameof(IsImproved), nameof(IsWorse))]
    private WhatIfResultDto? _result;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasRunError => !string.IsNullOrEmpty(RunError);

    public bool IsContentVisible => IsOrderLoaded && !IsLoading && !HasError;

    public bool HasResult => Result is not null;

    public int DeltaPoints => Result is { } result
        ? (int)Math.Round((result.After.Probability - result.Before.Probability) * 100)
        : 0;

    public bool IsImproved => HasResult && DeltaPoints > 0;

    public bool IsWorse => HasResult && DeltaPoints < 0;

    public bool IsValid =>
        string.IsNullOrEmpty(PromisedDaysError)
        && string.IsNullOrEmpty(InstallmentsError)
        && string.IsNullOrEmpty(SellerIdError);

    public bool HasChanges => BuildRequest() is { } request
        && (request.PromisedDays is not null || request.PaymentType is not null
            || request.Installments is not null || request.SellerId is not null);

    partial void OnPromisedDaysTextChanged(string value) => Validate();

    partial void OnInstallmentsTextChanged(string value) => Validate();

    partial void OnSellerIdChanged(string value) => Validate();

    partial void OnSelectedPaymentChanged(FilterOption? value) => Validate();

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

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var order = await _apiClient.GetOrderAsync(OrderId);
            ApplyOrder(order);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception is ApiException { StatusCode: 404 }
                ? "Замовлення не знайдено."
                : ApiErrorMessages.For(exception, "Не вдалося завантажити замовлення.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (BuildRequest() is not { } request)
        {
            return;
        }

        IsRunning = true;
        RunError = string.Empty;

        try
        {
            Result = await _whatIfService.RunAsync(OrderId, request);
        }
        catch (Exception exception)
        {
            RunError = exception switch
            {
                ApiException { StatusCode: 501 } => "Сервер поки не підтримує сценарії «що якщо».",
                ApiException { StatusCode: 400 } => "Сервер не прийняв ці значення. Перевірте поля.",
                ApiException { StatusCode: 404 } => "Замовлення не знайдено.",
                _ => ApiErrorMessages.For(exception, "Не вдалося порахувати сценарій."),
            };
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        PromisedDaysText = CurrentPromisedDays.ToString(CultureInfo.InvariantCulture);
        SelectedPayment = CurrentPayment;
        InstallmentsText = CurrentInstallments.ToString(CultureInfo.InvariantCulture);
        SellerId = CurrentSellerId;
        Result = null;
        RunError = string.Empty;
    }

    private bool CanRun() => IsOrderLoaded && !IsRunning && IsValid && HasChanges;

    private void ApplyOrder(OrderDetailsDto order)
    {
        var payment = order.Payments.OrderByDescending(p => p.Value).FirstOrDefault();
        var item = order.Items.OrderByDescending(i => i.Price).ThenBy(i => i.ItemNo).FirstOrDefault();

        CurrentPromisedDays = Math.Max(0, (order.EstimatedDeliveryAt.Date - order.PurchasedAt.Date).Days);
        CurrentPayment = Payments.FirstOrDefault(option => option.Value == payment?.Type);
        CurrentInstallments = Math.Max(1, payment?.Installments ?? 1);
        CurrentSellerId = item?.SellerId ?? string.Empty;

        IsOrderLoaded = true;
        Reset();
    }

    private void Validate()
    {
        PromisedDaysError = ParseNumber(PromisedDaysText, 1, MaxPromisedDays) is null
            ? $"Вкажіть від 1 до {MaxPromisedDays} днів."
            : string.Empty;

        InstallmentsError = ParseNumber(InstallmentsText, 1, MaxInstallments) is null
            ? $"Вкажіть від 1 до {MaxInstallments} платежів."
            : string.Empty;

        var seller = SellerId.Trim();
        SellerIdError = seller.Length == 0 || SellerIdPattern().IsMatch(seller)
            ? string.Empty
            : "Id продавця має 32 символи: цифри 0–9 і літери a–f.";

        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(HasChanges));
        RunCommand.NotifyCanExecuteChanged();
    }

    private WhatIfRequest? BuildRequest()
    {
        if (!IsOrderLoaded || !IsValid)
        {
            return null;
        }

        var days = ParseNumber(PromisedDaysText, 1, MaxPromisedDays);
        var installments = ParseNumber(InstallmentsText, 1, MaxInstallments);
        var payment = SelectedPayment?.Value;
        var seller = SellerId.Trim();

        return new WhatIfRequest
        {
            PromisedDays = days != CurrentPromisedDays ? days : null,
            PaymentType = payment != CurrentPayment?.Value ? payment! : null!,
            Installments = installments != CurrentInstallments ? installments : null,
            SellerId = seller.Length > 0 && !string.Equals(seller, CurrentSellerId, StringComparison.OrdinalIgnoreCase) ? seller : null!,
        };
    }

    private static int? ParseNumber(string text, int min, int max) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max
            ? number
            : null;

    [GeneratedRegex("^[0-9a-fA-F]{32}$")]
    private static partial Regex SellerIdPattern();
}
