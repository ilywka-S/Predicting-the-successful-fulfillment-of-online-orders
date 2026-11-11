using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class OrderDetailsViewModelTests
{
    private const string OrderId = "e481f51cbdc54678b7cc49136f2d6af7";

    private readonly Mock<IClient> _apiClientMock = new();
    private readonly Mock<IOrderNavigation> _navigationMock = new();
    private readonly OrderDetailsViewModel _viewModel;

    public OrderDetailsViewModelTests()
    {
        _viewModel = new OrderDetailsViewModel(_apiClientMock.Object, _navigationMock.Object);
    }

    [Fact]
    public async Task Load_ShowsOrderItemsPaymentsAndPrediction()
    {
        var prediction = Prediction(0.82f, RiskLevel.Low, hoursAgo: 5);
        SetupOrder(Order(prediction));

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.OrderId.Should().Be(OrderId);
        _viewModel.IsContentVisible.Should().BeTrue();
        _viewModel.IsLoading.Should().BeFalse();
        _viewModel.Items.Should().HaveCount(2);
        _viewModel.Payments.Should().ContainSingle();
        _viewModel.ItemsTotal.Should().Be(150);
        _viewModel.FreightTotal.Should().Be(30);
        _viewModel.OrderTotal.Should().Be(180);
        _viewModel.LatestPrediction.Should().BeSameAs(prediction);
        _viewModel.Factors.Should().HaveCount(2);
        _viewModel.History.Select(h => h.CreatedAt).Should().Equal(prediction.CreatedAt);
        _viewModel.PredictCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Load_OrderWithoutPrediction_HasNoFactorsOrHistory()
    {
        SetupOrder(Order(prediction: null));

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.HasPrediction.Should().BeFalse();
        _viewModel.HasFactors.Should().BeFalse();
        _viewModel.HasHistory.Should().BeFalse();
        _viewModel.IsContentVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Load_DeliveredAfterPromisedDate_IsLate()
    {
        var order = Order(prediction: null);
        order.DeliveredCustomerAt = order.EstimatedDeliveryAt.AddDays(3);
        SetupOrder(order);

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.IsDelivered.Should().BeTrue();
        _viewModel.IsDeliveredLate.Should().BeTrue();
    }

    [Fact]
    public async Task Load_NotFound_ShowsErrorAndCannotPredict()
    {
        _apiClientMock
            .Setup(x => x.GetOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Not found", 404, null, null, null));

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Be("Замовлення не знайдено.");
        _viewModel.IsContentVisible.Should().BeFalse();
        _viewModel.PredictCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Retry_AfterNetworkError_LoadsOrder()
    {
        _apiClientMock
            .SetupSequence(x => x.GetOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException())
            .ReturnsAsync(Order(prediction: null));

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);
        _viewModel.ErrorMessage.Should().Be("Немає підключення до сервера. Перевірте мережу.");

        await _viewModel.RetryCommand.ExecuteAsync(null);

        _viewModel.HasError.Should().BeFalse();
        _viewModel.IsContentVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Predict_UpdatesCardFactorsAndHistory()
    {
        var old = Prediction(0.4f, RiskLevel.High, hoursAgo: 48);
        var fresh = Prediction(0.9f, RiskLevel.Low, hoursAgo: 0, factors: 3);
        SetupOrder(Order(old));
        _apiClientMock.Setup(x => x.PredictOrderAsync(OrderId)).ReturnsAsync(fresh);
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.LatestPrediction.Should().BeSameAs(fresh);
        _viewModel.Factors.Should().HaveCount(3);
        _viewModel.History.Select(h => h.CreatedAt).Should().Equal(fresh.CreatedAt, old.CreatedAt);
        _viewModel.IsPredicting.Should().BeFalse();
        _viewModel.HasPredictError.Should().BeFalse();
    }

    [Fact]
    public async Task Predict_WhileRunning_ShowsIndicatorAndBlocksSecondClick()
    {
        var pending = new TaskCompletionSource<PredictionDto>();
        SetupOrder(Order(prediction: null));
        _apiClientMock.Setup(x => x.PredictOrderAsync(OrderId)).Returns(pending.Task);
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        var running = _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.IsPredicting.Should().BeTrue();
        _viewModel.PredictCommand.CanExecute(null).Should().BeFalse();

        pending.SetResult(Prediction(0.7f, RiskLevel.Medium, hoursAgo: 0));
        await running;

        _viewModel.IsPredicting.Should().BeFalse();
        _viewModel.PredictCommand.CanExecute(null).Should().BeTrue();
        _apiClientMock.Verify(x => x.PredictOrderAsync(OrderId), Times.Once);
    }

    [Fact]
    public async Task Predict_ServiceUnavailable_ShowsMessageAndKeepsOldPrediction()
    {
        var old = Prediction(0.4f, RiskLevel.High, hoursAgo: 48);
        SetupOrder(Order(old));
        _apiClientMock
            .Setup(x => x.PredictOrderAsync(OrderId))
            .ThrowsAsync(new ApiException("Unavailable", 503, null, null, null));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.PredictErrorMessage.Should().Be("Сервіс прогнозування тимчасово недоступний. Спробуйте за хвилину.");
        _viewModel.LatestPrediction.Should().BeSameAs(old);
        _viewModel.History.Select(h => h.CreatedAt).Should().Equal(old.CreatedAt);
        _viewModel.IsPredicting.Should().BeFalse();
        _viewModel.PredictCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Predict_NotEnoughData_ShowsMessage()
    {
        SetupOrder(Order(prediction: null));
        _apiClientMock
            .Setup(x => x.PredictOrderAsync(OrderId))
            .ThrowsAsync(new ApiException("Unprocessable", 422, null, null, null));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.PredictErrorMessage.Should().Be("Для цього замовлення не вистачає даних для прогнозу.");
        _viewModel.HasPrediction.Should().BeFalse();
    }

    [Fact]
    public async Task Predict_AfterError_ClearsMessageOnSuccess()
    {
        SetupOrder(Order(prediction: null));
        _apiClientMock
            .SetupSequence(x => x.PredictOrderAsync(OrderId))
            .ThrowsAsync(new ApiException("Unavailable", 503, null, null, null))
            .ReturnsAsync(Prediction(0.6f, RiskLevel.Medium, hoursAgo: 0));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.PredictCommand.ExecuteAsync(null);
        await _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.HasPredictError.Should().BeFalse();
        _viewModel.HasPrediction.Should().BeTrue();
        _viewModel.History.Should().ContainSingle();
    }

    [Fact]
    public async Task Load_AgainAfterPredict_StartsHistoryFromServerPrediction()
    {
        var first = Prediction(0.4f, RiskLevel.High, hoursAgo: 48);
        var fresh = Prediction(0.9f, RiskLevel.Low, hoursAgo: 0);
        _apiClientMock
            .SetupSequence(x => x.GetOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Order(first))
            .ReturnsAsync(Order(fresh));
        _apiClientMock.Setup(x => x.PredictOrderAsync(OrderId)).ReturnsAsync(fresh);
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);
        await _viewModel.PredictCommand.ExecuteAsync(null);

        await _viewModel.RetryCommand.ExecuteAsync(null);

        _viewModel.History.Select(h => h.CreatedAt).Should().Equal(fresh.CreatedAt);
        _viewModel.LatestPrediction.Should().BeSameAs(fresh);
    }

    [Fact]
    public async Task OpenWhatIf_NavigatesWithOrderId()
    {
        SetupOrder(Order(prediction: null));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.OpenWhatIfCommand.ExecuteAsync(null);

        _navigationMock.Verify(x => x.OpenWhatIfAsync(OrderId), Times.Once);
    }

    [Fact]
    public async Task Load_ShowsHistoryFromServer()
    {
        var latest = Prediction(0.8f, RiskLevel.Low, hoursAgo: 1);
        var older = Prediction(0.6f, RiskLevel.Medium, hoursAgo: 24);
        var oldest = Prediction(0.4f, RiskLevel.High, hoursAgo: 72);
        SetupOrder(Order(latest));
        SetupHistory(latest, older, oldest);

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.History.Select(h => h.Probability).Should().Equal(0.8f, 0.6f, 0.4f);
        _viewModel.HasHistory.Should().BeTrue();
    }

    [Fact]
    public async Task Load_HistoryRequestFails_KeepsCardWithLatestPrediction()
    {
        var latest = Prediction(0.8f, RiskLevel.Low, hoursAgo: 1);
        SetupOrder(Order(latest));
        _apiClientMock
            .Setup(x => x.GetOrderPredictionsAsync(OrderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException());

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.IsContentVisible.Should().BeTrue();
        _viewModel.HasError.Should().BeFalse();
        _viewModel.HasHistoryError.Should().BeTrue();
        _viewModel.History.Select(h => h.CreatedAt).Should().Equal(latest.CreatedAt);
    }

    [Fact]
    public async Task Load_RequestsOrderAndHistoryAtTheSameTime()
    {
        var latest = Prediction(0.8f, RiskLevel.Low, hoursAgo: 1);
        var pendingOrder = new TaskCompletionSource<OrderDetailsDto>();
        _apiClientMock
            .Setup(x => x.GetOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .Returns(pendingOrder.Task);
        SetupHistory(latest);

        var loading = _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _apiClientMock.Verify(x => x.GetOrderPredictionsAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _viewModel.IsLoading.Should().BeTrue();

        pendingOrder.SetResult(Order(latest));
        await loading;

        _viewModel.IsContentVisible.Should().BeTrue();
        _viewModel.History.Should().ContainSingle();
    }

    [Fact]
    public async Task Load_LongHistory_ShowsOnlyNewest()
    {
        var predictions = Enumerable.Range(0, 15).Select(i => Prediction(0.5f, RiskLevel.Medium, hoursAgo: i)).ToArray();
        SetupOrder(Order(predictions[0]));
        SetupHistory(predictions);

        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.History.Should().HaveCount(OrderDetailsViewModel.HistoryLimit);
        _viewModel.History[0].CreatedAt.Should().Be(predictions[0].CreatedAt);
        _viewModel.History[^1].CreatedAt.Should().Be(predictions[OrderDetailsViewModel.HistoryLimit - 1].CreatedAt);
    }

    [Fact]
    public async Task Predict_FullHistory_StaysWithinLimit()
    {
        var predictions = Enumerable.Range(1, OrderDetailsViewModel.HistoryLimit).Select(i => Prediction(0.5f, RiskLevel.Medium, hoursAgo: i)).ToArray();
        var fresh = Prediction(0.9f, RiskLevel.Low, hoursAgo: 0);
        SetupOrder(Order(predictions[0]));
        SetupHistory(predictions);
        _apiClientMock.Setup(x => x.PredictOrderAsync(OrderId)).ReturnsAsync(fresh);
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.History.Should().HaveCount(OrderDetailsViewModel.HistoryLimit);
        _viewModel.History[0].CreatedAt.Should().Be(fresh.CreatedAt);
        _viewModel.History.Select(h => h.CreatedAt).Should().NotContain(predictions[^1].CreatedAt);
    }

    [Fact]
    public async Task Predict_PutsNewPredictionOnTopOfServerHistory()
    {
        var latest = Prediction(0.6f, RiskLevel.Medium, hoursAgo: 5);
        var older = Prediction(0.4f, RiskLevel.High, hoursAgo: 48);
        var fresh = Prediction(0.9f, RiskLevel.Low, hoursAgo: 0);
        SetupOrder(Order(latest));
        SetupHistory(latest, older);
        _apiClientMock.Setup(x => x.PredictOrderAsync(OrderId)).ReturnsAsync(fresh);
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        await _viewModel.PredictCommand.ExecuteAsync(null);

        _viewModel.History.Select(h => h.CreatedAt).Should().Equal(fresh.CreatedAt, latest.CreatedAt, older.CreatedAt);
    }

    private void SetupHistory(params PredictionDto[] predictions) =>
        _apiClientMock
            .Setup(x => x.GetOrderPredictionsAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(predictions
                .Select(p => new PredictionHistoryItemDto
                {
                    CreatedAt = p.CreatedAt,
                    ModelVersion = p.ModelVersion,
                    Probability = p.Probability,
                    RiskLevel = p.RiskLevel,
                })
                .ToList());

    private void SetupOrder(OrderDetailsDto order) =>
        _apiClientMock
            .Setup(x => x.GetOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

    private static OrderDetailsDto Order(PredictionDto? prediction) => new()
    {
        Id = OrderId,
        Status = "delivered",
        PurchasedAt = new DateTimeOffset(2017, 10, 2, 10, 56, 0, TimeSpan.Zero),
        EstimatedDeliveryAt = new DateTimeOffset(2017, 10, 18, 0, 0, 0, TimeSpan.Zero),
        Customer = new CustomerDto { City = "sao paulo", State = "SP" },
        Items =
        [
            new OrderItemDto { ItemNo = 1, Category = "housewares", Price = 100, FreightValue = 20, SellerState = "SP" },
            new OrderItemDto { ItemNo = 2, Category = "toys", Price = 50, FreightValue = 10, SellerState = "RJ" },
        ],
        Payments = [new PaymentDto { Type = "credit_card", Installments = 3, Value = 180 }],
        LatestPrediction = prediction!,
    };

    private static PredictionDto Prediction(float probability, RiskLevel risk, int hoursAgo, int factors = 2) => new()
    {
        Probability = probability,
        RiskLevel = risk,
        ModelVersion = "v1",
        CreatedAt = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero).AddHours(-hoursAgo),
        Factors = Enumerable.Range(1, factors)
            .Select(n => new RiskFactorDto
            {
                Feature = n == 1 ? "DistanceKm" : "PromisedDays",
                Label = n == 1 ? "Відстань продавець -> клієнт" : "Обіцяний термін доставки",
                Value = n * 10,
                Impact = n == 1 ? FactorImpact.Increases : FactorImpact.Decreases,
            })
            .ToList(),
    };
}
