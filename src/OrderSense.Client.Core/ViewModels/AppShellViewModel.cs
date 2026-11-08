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

    [ObservableProperty]
    private bool isBatchPredictionVisible;

    public AppShellViewModel(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public async Task UpdateVisibilityAsync()
    {
        var role = await _tokenStorage.GetRoleAsync();
        
        IsManagerVisible = role == "manager"; 
        IsAnalystVisible = role == "analyst";
        IsAdminVisible = role == "admin";

        IsBatchPredictionVisible = role == "admin" || role == "analyst";
    }

}