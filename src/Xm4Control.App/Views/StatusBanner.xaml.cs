using Microsoft.UI.Xaml.Controls;

namespace Xm4Control.App.Views;

public sealed partial class StatusBanner : UserControl
{
    public StatusBanner() => InitializeComponent();

    internal MainViewModel ViewModel => App.Current.ViewModel;
}
