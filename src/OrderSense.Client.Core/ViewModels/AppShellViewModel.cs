using CommunityToolkit.Mvvm.ComponentModel;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.ViewModels;

public partial class AppShellViewModel : ObservableObject
{
    private readonly ITokenStorage _tokenStorage;

    [ObservableProperty]
    private bool isManagerVisible;

    [ObservableProperty]
    private bool isAnalystVisible;

    [ObservableProperty]
    private bool isAdminVisible;

    public AppShellViewModel(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public async Task UpdateVisibilityAsync()
    {
        var role = await _tokenStorage.GetRoleAsync();
        
        // Звіряємо з константами, які ти показав
        IsManagerVisible = role == "manager"; 
        IsAnalystVisible = role == "analyst";
        IsAdminVisible = role == "admin";
    }
}