using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Services;

public class ApiWhatIfService : IWhatIfService
{
    private readonly IClient _apiClient;

    public ApiWhatIfService(IClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<WhatIfResultDto> RunAsync(string orderId, WhatIfRequest request, CancellationToken cancellationToken = default) =>
        _apiClient.WhatIfAsync(orderId, request, cancellationToken);
}
