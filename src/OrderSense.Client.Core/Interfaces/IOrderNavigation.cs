namespace OrderSense.Client.Core.Interfaces;

public interface IOrderNavigation
{
    Task OpenOrderAsync(string orderId);
}
