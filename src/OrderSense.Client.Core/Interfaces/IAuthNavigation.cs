namespace OrderSense.Client.Core.Interfaces;

public interface IAuthNavigation
{
    void NavigateToLogin();
    void NavigateToMain();
    Task NavigateToLoginAsync();
}