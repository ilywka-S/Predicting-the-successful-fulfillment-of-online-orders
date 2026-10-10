using OrderSense.Client.Core.ViewModels;
namespace OrderSense.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    public AppShell(AppShellViewModel? viewModel = null)
    {
        InitializeComponent();
        if (viewModel != null)
        {
            BindingContext = viewModel;
        }
    }
}