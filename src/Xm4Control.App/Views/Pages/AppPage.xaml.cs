using System.Windows;
using System.Windows.Controls;

namespace Xm4Control.App.Views.Pages;

public partial class AppPage : Page
{
    public AppPage()
    {
        InitializeComponent();
        DataContext = App.Current.ViewModel;
    }

    private void OnQuit(object sender, RoutedEventArgs e) => App.Current.Quit();
}
