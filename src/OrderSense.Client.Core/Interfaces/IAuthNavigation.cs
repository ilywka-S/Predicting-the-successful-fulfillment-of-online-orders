namespace OrderSense.Client.Core.Interfaces;

public interface IAuthNavigation
{
    Task NavigateToMainAsync();
    Task NavigateToLoginAsync();
}