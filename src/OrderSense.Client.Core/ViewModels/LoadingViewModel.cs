using CommunityToolkit.Mvvm.ComponentModel;

namespace OrderSense.Client.Core.ViewModels;

public partial class LoadingViewModel : ObservableObject
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IAuthNavigation _navigation;

    public LoadingViewModel(ITokenStorage tokenStorage, IAuthNavigation navigation)
    {
        _tokenStorage = tokenStorage;
        _navigation = navigation;
    }

    public async Task InitializeAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            _navigation.NavigateToMain();
        }
        else
        {
            _navigation.NavigateToLogin();
        }
    }
}