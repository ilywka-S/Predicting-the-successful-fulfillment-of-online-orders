using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IClient _apiClient; 
    private readonly ITokenStorage _tokenStorage;
    private readonly IAuthNavigation _navigation;

    public MainViewModel(IClient apiClient, ITokenStorage tokenStorage, IAuthNavigation navigation)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try 
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();

            var request = new RefreshRequest 
            { 
                RefreshToken = refreshToken 
            };

            await _apiClient.LogoutAsync(request); 
        }
        catch 
        {
        }

        await _tokenStorage.ClearTokensAsync();
        await _navigation.NavigateToLoginAsync();
    }
}