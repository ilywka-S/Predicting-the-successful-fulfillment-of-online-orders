using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Contracts;
using Xunit;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<ISignalRService> _signalRServiceMock;
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _signalRServiceMock = new Mock<ISignalRService>();
        _viewModel = new DashboardViewModel(_signalRServiceMock.Object);
    }

    [Fact]
    public void OnOrderCreated_AddsNewOrderToCollection()
    {
        // Arrange
        var testOrder = new OrderFeatures();

        // Act — симулюємо виклик події SignalR на бекенді
        _signalRServiceMock.Raise(x => x.OnOrderCreated += null, testOrder);

        // Assert
        Assert.Single(_viewModel.Orders);
        Assert.Same(testOrder, _viewModel.Orders[0]);
    }

    [Fact]
    public void OnPredictionUpdated_AddsPredictionToCollection()
    {
        // Arrange
        var testPrediction = new OrderPrediction 
        { 
            Probability = 0.85f, 
            RiskLevel = "low", 
            ModelVersion = "v1.0" 
        };

        // Act
        _signalRServiceMock.Raise(x => x.OnPredictionUpdated += null, testPrediction);

        // Assert
        Assert.Single(_viewModel.Predictions);
        Assert.Equal(0.85f, _viewModel.Predictions[0].Probability);
        Assert.Equal("low", _viewModel.Predictions[0].RiskLevel);
    }

    [Fact]
    public void OnBatchJobProgress_UpdatesProgressProperty()
    {
        // Arrange
        var incomingProgress = new BatchJobProgress(
            JobId: Guid.NewGuid(),
            ProcessedRows: 75,
            TotalRows: 100,
            Status: "running" 
        );

        // Act
        _signalRServiceMock.Raise(x => x.OnBatchJobProgress += null, incomingProgress);

        // Assert
        Assert.Equal(75, _viewModel.BatchJobProgress);
    }
}