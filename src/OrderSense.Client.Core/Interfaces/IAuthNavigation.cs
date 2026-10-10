namespace OrderSense.Client.Core;

public interface IAuthNavigation
{
    void NavigateToLogin();
    void NavigateToMain();
    Task NavigateToLoginAsync();
}