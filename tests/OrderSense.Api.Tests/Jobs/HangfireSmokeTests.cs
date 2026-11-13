using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using OrderSense.Api.Tests.Infrastructure;
namespace OrderSense.Api.Tests.Jobs;
public static class SmokeJob
{
    public static void Run()
    {
    }
}
[Collection(ApiCollection.Name)]
public class HangfireSmokeTests(ApiFactory factory)
{
    [Fact]
    public async Task EnqueuedJob_IsProcessedByServerUsingPostgresStorage()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IBackgroundJobClient client = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        string jobId = client.Enqueue(() => SmokeJob.Run());
        bool succeeded = false;
        for (int attempt = 0; attempt < 40 && !succeeded; attempt++)
        {
            JobDetailsDto? details = JobStorage.Current.GetMonitoringApi().JobDetails(jobId);
            if (details != null)
            {
                foreach (StateHistoryDto state in details.History)
                {
                    if (state.StateName == "Succeeded")
                    {
                        succeeded = true;
                    }
                }
            }
            if (!succeeded)
            {
                await Task.Delay(500);
            }
        }
        Assert.True(succeeded);
    }
}