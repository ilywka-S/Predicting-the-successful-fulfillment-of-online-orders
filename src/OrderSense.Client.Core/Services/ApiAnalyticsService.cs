using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Services;

public class ApiAnalyticsService : IAnalyticsService
{
    private readonly IClient _apiClient;

    public ApiAnalyticsService(IClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<AnalyticsPointDto>> GetRiskAsync(
        AnalyticsDimension dimension,
        DateTime? fromDate,
        DateTime? toDate,
        int top,
        CancellationToken cancellationToken = default)
    {
        var from = ToApiDate(fromDate);
        var to = ToApiDate(toDate);

        var points = dimension switch
        {
            AnalyticsDimension.Day => await _apiClient.GetRiskByDayAsync(from, to, null, cancellationToken),
            AnalyticsDimension.Category => await _apiClient.GetRiskByCategoryAsync(from, to, top, cancellationToken),
            AnalyticsDimension.State => await _apiClient.GetRiskByStateAsync(from, to, top, cancellationToken),
            _ => await _apiClient.GetRiskBySellerAsync(from, to, top, cancellationToken),
        };

        return points.ToList();
    }

    private static DateTimeOffset? ToApiDate(DateTime? date) =>
        date is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified), TimeSpan.Zero) : null;
}
