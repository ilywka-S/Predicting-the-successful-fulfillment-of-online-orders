namespace OrderSense.Client.Core.Interfaces;

public interface IWhatIfService
{
    Task<WhatIfResultDto> RunAsync(string orderId, WhatIfRequest request, CancellationToken cancellationToken = default);
}
