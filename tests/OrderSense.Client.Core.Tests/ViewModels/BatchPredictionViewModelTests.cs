using System;
using Moq;
using Xunit;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Contracts;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Client.Core;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class BatchPredictionViewModelTests
{
    [Fact]
    public void HandleProgress_ShouldIgnoreEvent_WhenJobIdMismatches()
    {
        // Arrange
        var apiClientMock = new Mock<IClient>();
        var signalRServiceMock = new Mock<ISignalRService>();
        
        var fileServiceMock = new Mock<IFileService>();

        var viewModel = new BatchPredictionViewModel(
            apiClientMock.Object, 
            signalRServiceMock.Object, 
            fileServiceMock.Object);

        viewModel.Subscribe();

        var foreignJobId = Guid.NewGuid();
        
        var incomingProgress = new BatchJobProgress(
            JobId: foreignJobId,
            ProcessedRows: 50,
            TotalRows: 100,
            Status: "running" 
        );

        // Act
        signalRServiceMock.Raise(s => s.OnBatchJobProgress += null, incomingProgress);

        // Assert
        Assert.Equal(0.0, viewModel.Progress); 
        Assert.False(viewModel.IsFinished);
    }
}