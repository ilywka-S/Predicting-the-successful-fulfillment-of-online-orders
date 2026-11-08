using CommunityToolkit.Mvvm.ComponentModel;

namespace OrderSense.Maui.ViewModels;

public partial class DetailsViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    private string info = "Це друга сторінка.";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id))
        {
            Info = $"Замовлення #{id}";
        }
    }
}