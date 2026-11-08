using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class WhatIfViewModelTests
{
    private const string OrderId = "e481f51cbdc54678b7cc49136f2d6af7";
    private const string SellerId = "3504c0cb71d7fa48d967e0e4c94d59d9";
    private const string OtherSellerId = "4a3ca9315b744ce9f8e9374361493884";

    private readonly Mock<IClient> _apiClientMock = new();
    private readonly Mock<IWhatIfService> _whatIfServiceMock = new();
    private readonly WhatIfViewModel _viewModel;

    public WhatIfViewModelTests()
    {
        _apiClientMock.Setup(x => x.GetOrderAsync(OrderId)).ReturnsAsync(Order());
        _viewModel = new WhatIfViewModel(_apiClientMock.Object, _whatIfServiceMock.Object);
    }

    [Fact]
    public async Task Load_FillsFormWithCurrentOrderValues()
    {
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.IsContentVisible.Should().BeTrue();
        _viewModel.CurrentPromisedDays.Should().Be(16);
        _viewModel.PromisedDaysText.Should().Be("16");
        _viewModel.SelectedPayment!.Value.Should().Be("credit_card");
        _viewModel.InstallmentsText.Should().Be("3");
        _viewModel.SellerId.Should().Be(SellerId);
        _viewModel.HasChanges.Should().BeFalse();
        _viewModel.RunCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("181")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task PromisedDays_OutOfRange_ShowsErrorAndBlocksRun(string days)
    {
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.PromisedDaysText = days;

        _viewModel.PromisedDaysError.Should().NotBeEmpty();
        _viewModel.IsValid.Should().BeFalse();
        _viewModel.RunCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Installments_OutOfRange_ShowsError()
    {
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.InstallmentsText = "25";

        _viewModel.InstallmentsError.Should().Be("Вкажіть від 1 до 24 платежів.");
    }

    [Fact]
    public async Task SellerId_WrongFormat_ShowsError()
    {
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.SellerId = "seller-1";

        _viewModel.SellerIdError.Should().NotBeEmpty();
        _viewModel.RunCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Run_SendsOnlyChangedValues()
    {
        WhatIfRequest? sent = null;
        _whatIfServiceMock
            .Setup(x => x.RunAsync(OrderId, It.IsAny<WhatIfRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, WhatIfRequest, CancellationToken>((_, request, _) => sent = request)
            .ReturnsAsync(Result(0.7f, 0.82f));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.PromisedDaysText = "30";
        _viewModel.SellerId = OtherSellerId;
        await _viewModel.RunCommand.ExecuteAsync(null);

        sent!.PromisedDays.Should().Be(30);
        sent.SellerId.Should().Be(OtherSellerId);
        sent.PaymentType.Should().BeNull();
        sent.Installments.Should().BeNull();
    }

    [Fact]
    public async Task Run_ShowsBeforeAfterAndImprovement()
    {
        _whatIfServiceMock
            .Setup(x => x.RunAsync(OrderId, It.IsAny<WhatIfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(0.7f, 0.82f));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.SelectedPayment = _viewModel.PaymentOptions.Single(o => o.Value == "boleto");
        await _viewModel.RunCommand.ExecuteAsync(null);

        _viewModel.HasResult.Should().BeTrue();
        _viewModel.DeltaPoints.Should().Be(12);
        _viewModel.IsImproved.Should().BeTrue();
        _viewModel.IsWorse.Should().BeFalse();
        _viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WorseScenario_IsMarkedWorse()
    {
        _whatIfServiceMock
            .Setup(x => x.RunAsync(OrderId, It.IsAny<WhatIfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(0.7f, 0.55f));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.PromisedDaysText = "5";
        await _viewModel.RunCommand.ExecuteAsync(null);

        _viewModel.DeltaPoints.Should().Be(-15);
        _viewModel.IsWorse.Should().BeTrue();
    }

    [Theory]
    [InlineData(501, "Сервер поки не підтримує сценарії «що якщо».")]
    [InlineData(503, "Сервіс прогнозування тимчасово недоступний. Спробуйте за хвилину.")]
    [InlineData(400, "Сервер не прийняв ці значення. Перевірте поля.")]
    public async Task Run_ApiError_ShowsMessage(int status, string message)
    {
        _whatIfServiceMock
            .Setup(x => x.RunAsync(OrderId, It.IsAny<WhatIfRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Error", status, null, null, null));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);

        _viewModel.InstallmentsText = "6";
        await _viewModel.RunCommand.ExecuteAsync(null);

        _viewModel.RunError.Should().Be(message);
        _viewModel.HasResult.Should().BeFalse();
        _viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Reset_RestoresCurrentValuesAndClearsResult()
    {
        _whatIfServiceMock
            .Setup(x => x.RunAsync(OrderId, It.IsAny<WhatIfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(0.7f, 0.8f));
        await _viewModel.LoadCommand.ExecuteAsync(OrderId);
        _viewModel.PromisedDaysText = "40";
        await _viewModel.RunCommand.ExecuteAsync(null);

        _viewModel.ResetCommand.Execute(null);

        _viewModel.PromisedDaysText.Should().Be("16");
        _viewModel.HasResult.Should().BeFalse();
        _viewModel.HasChanges.Should().BeFalse();
    }

    [Fact]
    public async Task Load_OrderNotFound_ShowsError()
    {
        _apiClientMock
            .Setup(x => x.GetOrderAsync("missing"))
            .ThrowsAsync(new ApiException("Not found", 404, null, null, null));

        await _viewModel.LoadCommand.ExecuteAsync("missing");

        _viewModel.ErrorMessage.Should().Be("Замовлення не знайдено.");
        _viewModel.IsContentVisible.Should().BeFalse();
    }

    private static OrderDetailsDto Order() => new()
    {
        Id = OrderId,
        Status = "delivered",
        PurchasedAt = new DateTimeOffset(2017, 10, 2, 10, 56, 0, TimeSpan.Zero),
        EstimatedDeliveryAt = new DateTimeOffset(2017, 10, 18, 0, 0, 0, TimeSpan.Zero),
        Customer = new CustomerDto { City = "sao paulo", State = "SP" },
        Items =
        [
            new OrderItemDto { ItemNo = 1, Price = 20, SellerId = OtherSellerId },
            new OrderItemDto { ItemNo = 2, Price = 100, SellerId = SellerId },
        ],
        Payments =
        [
            new PaymentDto { Type = "voucher", Installments = 1, Value = 10 },
            new PaymentDto { Type = "credit_card", Installments = 3, Value = 110 },
        ],
    };

    private static WhatIfResultDto Result(float before, float after) => new()
    {
        Before = new PredictionDto { Probability = before, RiskLevel = RiskLevel.Medium, ModelVersion = "v1" },
        After = new PredictionDto { Probability = after, RiskLevel = RiskLevel.Low, ModelVersion = "v1" },
    };
}
