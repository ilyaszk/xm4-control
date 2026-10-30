using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Xm4Control.App.Views.Pages;

namespace Xm4Control.App.Views;

/// <summary>Popup rapide affiché au-dessus de l'horloge, qui se cache dès qu'on clique ailleurs.</summary>
public partial class FlyoutWindow : FluentWindow
{
    private DateTime _hiddenAt;

    internal FlyoutWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
        Deactivated += (_, _) => HideFlyout();
        SizeChanged += (_, _) => PlaceNearTray();
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            HideFlyout();
            return;
        }
        // Cliquer sur l'icône désactive d'abord le popup, qui se cache : on ne le rouvre pas aussitôt.
        if (DateTime.UtcNow - _hiddenAt < TimeSpan.FromMilliseconds(400)) return;
        PlaceNearTray();
        Show();
        Activate();
    }

    private void HideFlyout()
    {
        if (!IsVisible) return;
        Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    private void PlaceNearTray()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 12;
        Top = area.Bottom - (ActualHeight > 0 ? ActualHeight : 440) - 12;
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        App.Current.ShowSettings();
    }

    private void OnOpenEqualizer(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        App.Current.ShowSettings(typeof(EqualizerPage));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.Current.IsQuitting)
        {
            e.Cancel = true;
            HideFlyout();
        }
        base.OnClosing(e);
    }
}
