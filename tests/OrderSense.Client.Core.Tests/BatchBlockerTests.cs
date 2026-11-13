using System.Net.Http;
using Moq;
using Xunit;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;

namespace OrderSense.Client.Core.Tests.ViewModels;

public class BatchBlockerTests
{
    private static readonly Guid JobId = Guid.NewGuid();

    private static (BatchPredictionViewModel Vm, Mock<IClient> Api, Mock<ISignalRService> SignalR) Create()
    {
        var api = new Mock<IClient>();
        var signalR = new Mock<ISignalRService>();
        var files = new Mock<IFileService>();

        api.Setup(x => x.CreateBatchJobAsync(It.IsAny<FileParameter>()))
            .ReturnsAsync(new BatchJobDto { Id = JobId });
        files.Setup(x => x.PickCsvAsync()).ReturnsAsync(new FileSelectionResult
        {
            FileName = "a.csv",
            OpenReadAsync = () => Task.FromResult<Stream>(new MemoryStream())
        });

        return (new BatchPredictionViewModel(api.Object, signalR.Object, files.Object), api, signalR);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 130 && !condition(); i++)
        {
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task HttpTimeout_DoesNotStopPolling()
    {
        var (vm, api, _) = Create();
        api.SetupSequence(x => x.GetBatchJobAsync(JobId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout"))
            .ReturnsAsync(new BatchJobDto { Id = JobId, Status = BatchJobStatus.Done, TotalRows = 1, ProcessedRows = 1 });

        await vm.SelectCsvAsync();
        await vm.StartBatchJobAsync();
        await WaitUntil(() => vm.IsFinished);

        Assert.True(vm.IsFinished);
        Assert.True(vm.IsSuccess);
        Assert.False(vm.IsProcessing);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    public async Task PermanentApiError_FinishesJobWithError(int statusCode)
    {
        var (vm, api, _) = Create();
        api.Setup(x => x.GetBatchJobAsync(JobId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("error", statusCode, "", new Dictionary<string, IEnumerable<string>>(), null!));

        await vm.SelectCsvAsync();
        await vm.StartBatchJobAsync();
        await WaitUntil(() => vm.IsFinished);

        Assert.True(vm.IsFinished);
        Assert.False(vm.IsSuccess);
        Assert.False(vm.IsProcessing);
        vm.Unsubscribe();
    }

    [Fact]
    public async Task TemporaryError_KeepsPolling()
    {
        var (vm, api, _) = Create();
        api.SetupSequence(x => x.GetBatchJobAsync(JobId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network"))
            .ReturnsAsync(new BatchJobDto { Id = JobId, Status = BatchJobStatus.Done, TotalRows = 1, ProcessedRows = 1 });

        await vm.SelectCsvAsync();
        await vm.StartBatchJobAsync();
        await WaitUntil(() => vm.IsFinished);

        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public void Subscribe_StartsSignalR()
    {
        var (vm, _, signalR) = Create();

        vm.Subscribe();

        signalR.Verify(x => x.StartConnectionAsync(), Times.Once);
    }
}
