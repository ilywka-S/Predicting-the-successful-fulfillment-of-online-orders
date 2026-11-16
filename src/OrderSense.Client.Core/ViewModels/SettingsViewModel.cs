using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly IAuthNavigation _navigation;
    private readonly IThemeService _themes;

    public SettingsViewModel(IClient apiClient, ITokenStorage tokenStorage, IAuthNavigation navigation, IThemeService themes)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _navigation = navigation;
        _themes = themes;
        SelectedTheme = themes.Current;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials))]
    [NotifyPropertyChangedFor(nameof(HasUser))]
    [NotifyPropertyChangedFor(nameof(IsProfileSkeletonVisible))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRole))]
    private string _role = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProfileSkeletonVisible))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfileError))]
    private string _profileError = string.Empty;

    [ObservableProperty]
    private string _selectedTheme = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LogoutCommand))]
    private bool _isLoggingOut;

    public bool HasProfileError => ProfileError.Length > 0;

    public bool HasUser => Email.Length > 0;

    public bool HasRole => Role.Length > 0;

    public bool IsProfileSkeletonVisible => IsLoading && !HasUser;

    public string Initials
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(Name) ? Email : Name;
            var words = source.Split([' ', '.', '_', '-', '@'], StringSplitOptions.RemoveEmptyEntries);

            return words.Length switch
            {
                0 => "?",
                1 => words[0][..1].ToUpperInvariant(),
                _ => string.Concat(words[0][0], words[1][0]).ToUpperInvariant(),
            };
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && value != _themes.Current)
        {
            _themes.Apply(value);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ProfileError = string.Empty;

        try
        {
            var user = await _apiClient.MeAsync();

            Name = user.Name ?? string.Empty;
            Email = user.Email ?? string.Empty;
            Role = user.Role ?? string.Empty;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException)
        {
            ProfileError = ApiErrorMessages.For(ex, "Не вдалося завантажити профіль.");
            Role = await _tokenStorage.GetRoleAsync() ?? string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLogout() => !IsLoggingOut;

    [RelayCommand(CanExecute = nameof(CanLogout))]
    private async Task LogoutAsync()
    {
        IsLoggingOut = true;

        try
        {
            await RevokeRefreshTokenAsync();
            await _tokenStorage.ClearTokensAsync();

            Name = string.Empty;
            Email = string.Empty;
            Role = string.Empty;
            ProfileError = string.Empty;
        }
        finally
        {
            IsLoggingOut = false;
        }

        await _navigation.NavigateToLoginAsync();
    }

    private async Task RevokeRefreshTokenAsync()
    {
        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _apiClient.LogoutAsync(new RefreshRequest { RefreshToken = refreshToken });
            }
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException)
        {
            Debug.WriteLine($"Logout request failed: {ex.Message}");
        }
    }
}
