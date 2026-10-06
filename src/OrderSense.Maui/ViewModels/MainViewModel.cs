using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OrderSense.Maui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string greeting = "Це перша сторінка.";

    [RelayCommand]
    private async Task GoToDetailsAsync()
    {
        await Shell.Current.GoToAsync("DetailsPage");
    }
}