using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace OrderSense.Client.Core.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly IAuthNavigation _navigation;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;
    public bool IsNotBusy => !IsBusy;

    public LoginViewModel(IClient apiClient, ITokenStorage tokenStorage, IAuthNavigation navigation)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Будь ласка, введіть email та пароль.";
            return;
        }

        IsBusy = true;

        try
        {
            var response = await _apiClient.LoginAsync(new LoginRequest 
            { 
                Email = this.Email, 
                Password = this.Password 
            });

            await _tokenStorage.SaveTokensAsync(response.AccessToken, response.RefreshToken);

            _navigation.NavigateToMain();
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 401)
            {
                ErrorMessage = "Невірний email або пароль.";
            }
            else
            {
                ErrorMessage = "Помилка сервера або обліковий запис заблоковано.";
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Немає підключення до мережі.";
        }
        catch (Exception)
        {
            ErrorMessage = "Сталася непередбачена помилка.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}