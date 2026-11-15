namespace OrderSense.Client.Core.Interfaces;

public interface IBatchNavigation
{
    Task OpenSummaryAsync(Guid jobId);
}
