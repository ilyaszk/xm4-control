using Microsoft.UI.Xaml.Controls;

namespace Xm4Control.App.Views.Pages;

public sealed partial class EqualizerPage : Page
{
    public EqualizerPage() => InitializeComponent();

    internal MainViewModel ViewModel => App.Current.ViewModel;
}
