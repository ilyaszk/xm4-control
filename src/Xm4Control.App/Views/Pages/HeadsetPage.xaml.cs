using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Xm4Control.App.Views.Pages;

public sealed partial class HeadsetPage : Page
{
    public HeadsetPage() => InitializeComponent();

    internal MainViewModel ViewModel => App.Current.ViewModel;

    private async void OnPowerOff(object sender, RoutedEventArgs e)
    {
        if (await Ui.ConfirmAsync(XamlRoot, "Éteindre le casque", "Le casque va s'éteindre. Il faudra le rallumer avec son bouton.", "Éteindre"))
            await ViewModel.PowerOffAsync();
    }
}
