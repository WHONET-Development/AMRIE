using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using AMRIE.WinUI.ViewModels;

namespace AMRIE.WinUI.Pages;

public sealed partial class ResourceExplorerPage : Page
{
    public ResourceExplorerPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ResourceExplorerViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
