using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App.Views.Pages;

public sealed partial class SoundPage : Page
{
    public SoundPage() => InitializeComponent();

    internal MainViewModel ViewModel => App.Current.ViewModel;

    private async void OnToggleOptimizer(object sender, RoutedEventArgs e) => await ViewModel.ToggleOptimizerAsync();

    private void OnNoiseCancelling(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.NoiseCancelling);

    private void OnAmbient(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.Ambient);

    private void OnWindReduction(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.WindReduction);

    private void OnOff(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.Off);
}
