using FluentAssertions;
using Moq;
using OrderSense.Client.Core.Interfaces;
using OrderSense.Client.Core.ViewModels;
using OrderSense.Contracts;

namespace OrderSense.Client.Core.Tests.ViewModels;

public sealed class BatchResultsTests : IDisposable
{
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Mock<IClient> _apiClientMock = new();
    private readonly Mock<ISignalRService> _signalRMock = new();
    private readonly Mock<IFileService> _fileServiceMock = new();
    private readonly BatchPredictionViewModel _viewModel;

    public BatchResultsTests()
    {
        _fileServiceMock
            .Setup(x => x.PickCsvAsync())
            .ReturnsAsync(new FileSelectionResult { FileName = "orders.csv", OpenReadAsync = () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])) });
        _apiClientMock
            .Setup(x => x.CreateBatchJobAsync(It.IsAny<FileParameter>()))
            .ReturnsAsync(new BatchJobDto { Id = _jobId, Status = BatchJobStatus.Queued, TotalRows = 45 });

        _viewModel = new BatchPredictionViewModel(_apiClientMock.Object, _signalRMock.Object, _fileServiceMock.Object);
        _viewModel.Subscribe();
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task Upload_InvalidFile_ShowsFileAndRowErrors()
    {
        var errors = new Dictionary<string, ICollection<string>>
        {
            ["row 12"] = ["installments: not a number"],
            ["file"] = ["Missing columns: distanceKm."],
            ["row 3"] = ["customerState: required"],
        };
        _apiClientMock
            .Setup(x => x.CreateBatchJobAsync(It.IsAny<FileParameter>()))
            .ThrowsAsync(new ApiException<ValidationProblemDetails>("Bad Request", 400, null, null, new ValidationProblemDetails { Errors = errors }, null));

        await StartAsync();

        _viewModel.HasRowErrors.Should().BeTrue();
        _viewModel.RowErrors.Should().Equal(
            new BatchRowError(null, "Missing columns: distanceKm."),
            new BatchRowError(3, "customerState: required"),
            new BatchRowError(12, "installments: not a number"));
        _viewModel.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public async Task FinishedJob_LoadsServerMessageAndResultsPageByPage()
    {
        SetupJob(BatchJobStatus.Done, "2 rows were skipped");
        SetupResults(total: 45);
        await StartAsync();

        RaiseProgress("done");
        await WaitUntilAsync(() => _viewModel.HasResults);

        _viewModel.Results.Should().HaveCount(BatchPredictionViewModel.ResultsPageSize);
        _viewModel.ResultsTotal.Should().Be(45);
        _viewModel.HasMoreResults.Should().BeTrue();
        _viewModel.JobMessage.Should().Be("2 rows were skipped");

        await _viewModel.LoadMoreResultsCommand.ExecuteAsync(null);
        await _viewModel.LoadMoreResultsCommand.ExecuteAsync(null);

        _viewModel.Results.Should().HaveCount(45);
        _viewModel.Results.Select(r => r.RowNo).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        _viewModel.HasMoreResults.Should().BeFalse();
        _viewModel.LoadMoreResultsCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task FailedJob_ShowsServerMessageWithoutResults()
    {
        SetupJob(BatchJobStatus.Failed, "No rows could be predicted");
        await StartAsync();

        RaiseProgress("failed");
        await WaitUntilAsync(() => _viewModel.HasJobMessage);

        _viewModel.JobMessage.Should().Be("No rows could be predicted");
        _viewModel.HasResults.Should().BeFalse();
        _apiClientMock.Verify(x => x.GetBatchJobResultsAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task NewUpload_ClearsPreviousResultsAndErrors()
    {
        SetupJob(BatchJobStatus.Done, "2 rows were skipped");
        SetupResults(total: 5);
        await StartAsync();
        RaiseProgress("done");
        await WaitUntilAsync(() => _viewModel.HasResults);

        _apiClientMock
            .Setup(x => x.CreateBatchJobAsync(It.IsAny<FileParameter>()))
            .ReturnsAsync(new BatchJobDto { Id = Guid.NewGuid(), Status = BatchJobStatus.Queued });
        await _viewModel.StartBatchJobCommand.ExecuteAsync(null);

        _viewModel.HasResults.Should().BeFalse();
        _viewModel.HasJobMessage.Should().BeFalse();
        _viewModel.ResultsTotal.Should().Be(0);
    }

    [Fact]
    public async Task DownloadTemplate_SavesCsv()
    {
        _apiClientMock
            .Setup(x => x.GetBatchJobTemplateAsync())
            .ReturnsAsync(new FileResponse(200, new Dictionary<string, IEnumerable<string>>(), new MemoryStream([1]), null, null));
        _fileServiceMock
            .Setup(x => x.SaveFileAsync("ordersense-template.csv", It.IsAny<Stream>()))
            .ReturnsAsync(true);

        await _viewModel.DownloadTemplateCommand.ExecuteAsync(null);

        _fileServiceMock.Verify(x => x.SaveFileAsync("ordersense-template.csv", It.IsAny<Stream>()), Times.Once);
        _viewModel.StatusMessage.Should().Be("Шаблон збережено.");
    }

    private async Task StartAsync()
    {
        await _viewModel.SelectCsvCommand.ExecuteAsync(null);
        await _viewModel.StartBatchJobCommand.ExecuteAsync(null);
    }

    private void RaiseProgress(string status) =>
        _signalRMock.Raise(x => x.OnBatchJobProgress += null, new BatchJobProgress(_jobId, 45, 45, status));

    private void SetupJob(BatchJobStatus status, string? error) =>
        _apiClientMock
            .Setup(x => x.GetBatchJobAsync(_jobId))
            .ReturnsAsync(new BatchJobDto { Id = _jobId, Status = status, TotalRows = 45, ProcessedRows = 45, Error = error! });

    private void SetupResults(int total) =>
        _apiClientMock
            .Setup(x => x.GetBatchJobResultsAsync(_jobId, It.IsAny<int?>(), BatchPredictionViewModel.ResultsPageSize))
            .Returns((Guid _, int? page, int? size) =>
            {
                var first = ((page!.Value - 1) * size!.Value) + 1;
                var count = Math.Clamp(total - first + 1, 0, size.Value);
                return Task.FromResult(new PagedResponseOfBatchResultDto
                {
                    Page = page.Value,
                    PageSize = size.Value,
                    Total = total,
                    Items = Enumerable.Range(first, count)
                        .Select(n => new BatchResultDto { RowNo = n, Probability = 0.8f, RiskLevel = new RiskLevel2 { Value = RiskLevel.Low } })
                        .ToList(),
                });
            });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }

        condition().Should().BeTrue();
    }
}
