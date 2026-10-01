using CommunityToolkit.Mvvm.ComponentModel;

namespace OrderSense.Maui.ViewModels;

public partial class DetailsViewModel : ObservableObject
{
    [ObservableProperty]
    private string info = "Це друга сторінка.";
}