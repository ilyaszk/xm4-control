using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Xm4Control.App.Views.Pages;

namespace Xm4Control.App.Views;

public partial class SettingsWindow : FluentWindow
{
    private Type _pendingPage = typeof(SoundPage);
    private bool _navigated;

    internal SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: false);
            Navigate(_pendingPage);
        };
    }

    /// <summary>Affiche la fenêtre, sur la page demandée ou la dernière consultée.</summary>
    public void ShowPage(Type? page)
    {
        if (page is not null) _pendingPage = page;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        // Avant le premier Loaded, le modèle de NavigationView n'est pas encore appliqué.
        if (IsLoaded && (page is not null || !_navigated)) Navigate(_pendingPage);
    }

    private void Navigate(Type page) => _navigated = Navigation.Navigate(page) || _navigated;

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fermer la fenêtre la cache : l'appli reste dans la zone de notification.
        if (!App.Current.IsQuitting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
