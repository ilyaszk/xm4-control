using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Xm4Control.App.Views.Pages;

public sealed partial class AppPage : Page
{
    public AppPage() => InitializeComponent();

    internal MainViewModel ViewModel => App.Current.ViewModel;

    private void OnQuit(object sender, RoutedEventArgs e) => App.Current.Quit();
}
