using System.Windows.Controls;

namespace Xm4Control.App.Views.Pages;

public partial class EqualizerPage : Page
{
    public EqualizerPage()
    {
        InitializeComponent();
        DataContext = App.Current.ViewModel;
    }
}
