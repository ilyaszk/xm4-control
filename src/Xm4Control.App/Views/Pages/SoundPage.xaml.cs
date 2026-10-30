using System.Windows.Controls;

namespace Xm4Control.App.Views.Pages;

public partial class SoundPage : Page
{
    public SoundPage()
    {
        InitializeComponent();
        DataContext = App.Current.ViewModel;
    }
}
