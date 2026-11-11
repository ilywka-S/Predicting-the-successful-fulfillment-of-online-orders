namespace OrderSense.Client.Core.Interfaces;

public enum AnalyticsDimension
{
    Day,
    Category,
    State,
    Seller,
}

public interface IAnalyticsService
{
    Task<IReadOnlyList<AnalyticsPointDto>> GetRiskAsync(
        AnalyticsDimension dimension,
        DateTime? fromDate,
        DateTime? toDate,
        int top,
        CancellationToken cancellationToken = default);
}
