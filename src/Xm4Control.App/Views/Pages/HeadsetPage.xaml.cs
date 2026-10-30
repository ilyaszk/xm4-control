using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Xm4Control.App.Views.Pages;

public partial class HeadsetPage : Page
{
    public HeadsetPage()
    {
        InitializeComponent();
        DataContext = App.Current.ViewModel;
    }

    private async void OnPowerOff(object sender, RoutedEventArgs e)
    {
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Éteindre le casque",
            Content = "Le casque va s'éteindre. Il faudra le rallumer avec son bouton.",
            PrimaryButtonText = "Éteindre",
            CloseButtonText = "Annuler",
        };
        if (await confirm.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
            await App.Current.ViewModel.PowerOffAsync();
    }
}
