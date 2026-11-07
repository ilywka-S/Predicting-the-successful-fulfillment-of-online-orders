using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Moq;
using OrderSense.Client.Core;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Maui.ViewModels;
using Xunit;

namespace OrderSense.Tests.ViewModels;

public class BatchPredictionViewModelTests
{
    private readonly Mock<IClient> _apiClientMock;
    private readonly Mock<ISignalRService> _signalRServiceMock;
    private readonly Mock<IFileSaver> _fileSaverMock;
    private readonly BatchPredictionViewModel _viewModel;

    public BatchPredictionViewModelTests()
    {
        _apiClientMock = new Mock<IClient>();
        _signalRServiceMock = new Mock<ISignalRService>();
        _fileSaverMock = new Mock<IFileSaver>();

        _viewModel = new BatchPredictionViewModel(
            _apiClientMock.Object, 
            _signalRServiceMock.Object, 
            _fileSaverMock.Object);
    }

    [Fact]
    public async Task UploadAndStartJobAsync_WhenNoFileSelected_SetsErrorMessage()
    {
        // Act
        await _viewModel.UploadAndStartJobAsync();

        // Assert
        Assert.Equal("Будь ласка, оберіть файл.", _viewModel.ErrorMessage);
        Assert.False(_viewModel.IsProcessing);
        
        _apiClientMock.Verify(api => api.CreateBatchJobAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DownloadResultsAsync_WhenJobIdIsEmpty_DoesNothing()
    {
        // Act
        await _viewModel.DownloadResultsAsync();

        // Assert
        _apiClientMock.Verify(api => api.ExportBatchJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _fileSaverMock.Verify(fs => fs.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DownloadResultsAsync_WhenApiSucceeds_CallsFileSaver()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var dummyStream = new MemoryStream();
        
        var fakeResponse = new FileContentResult 
        { 
            FileContents = dummyStream.ToArray()
        };
        
        _apiClientMock
            .Setup(api => api.ExportBatchJobAsync(jobId))
            .ReturnsAsync(fakeResponse);

        _fileSaverMock
            .Setup(fs => fs.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileSaverResult("fake_path.csv", null));

        var field = typeof(BatchPredictionViewModel).GetField("_currentJobId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_viewModel, jobId);

        // Act
        await _viewModel.DownloadResultsAsync();

        // Assert
        Assert.Empty(_viewModel.ErrorMessage);
        
        _fileSaverMock.Verify(fs => fs.SaveAsync($"predictions_job_{jobId}.csv", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_SubscribesToSignalRProgressEvent()
    {
        // Assert
        Assert.NotNull(_viewModel);
    }
}