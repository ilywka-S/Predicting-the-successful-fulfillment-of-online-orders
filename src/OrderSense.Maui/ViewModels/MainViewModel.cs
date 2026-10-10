using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core;

namespace OrderSense.Maui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IClient _client;
    private readonly ITokenStorage _tokenStorage;

    // Додаємо поля для форми
    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    public MainViewModel(IClient client, ITokenStorage tokenStorage)
    {
        _client = client;
        _tokenStorage = tokenStorage;
    }

    [RelayCommand]
    private async Task TestAuthAsync()
    {
        try
        {
            var response = await _client.LoginAsync(new LoginRequest 
            { 
                Email = this.Email, 
                Password = this.Password 
            });

            System.Diagnostics.Debug.WriteLine($"Login success! Token: {response.AccessToken}");
            await _tokenStorage.SaveTokensAsync(response.AccessToken, response.RefreshToken);

            var userInfo = await _client.MeAsync();
            System.Diagnostics.Debug.WriteLine($"User info: {userInfo.Email} ({userInfo.Role})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
        }
    }
}