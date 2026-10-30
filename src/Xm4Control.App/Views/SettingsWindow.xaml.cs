using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Xm4Control.App.Views.Pages;

namespace Xm4Control.App.Views;

/// <summary>Fenêtre de réglages ; détruite à la fermeture, l'appli la recrée à la demande.</summary>
public sealed partial class SettingsWindow : Window
{
    private static readonly Type[] Pages = [typeof(SoundPage), typeof(EqualizerPage), typeof(HeadsetPage), typeof(AppPage)];

    internal SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Ui.IconPath);
        // Appli non empaquetée : chemin absolu plutôt qu'une URI ms-appx.
        AppTitleBar.IconSource = new ImageIconSource { ImageSource = new BitmapImage(new Uri(Ui.IconPath)) };
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.PreferredMinimumWidth = 760;

        var scale = Ui.Scale(this);
        var size = new SizeInt32((int)(900 * scale), (int)(700 * scale));
        var area = DisplayArea.Primary.WorkArea;
        AppWindow.MoveAndResize(new RectInt32(
            area.X + (area.Width - size.Width) / 2,
            area.Y + (area.Height - size.Height) / 2,
            size.Width,
            size.Height));
    }

    /// <summary>Affiche la fenêtre, sur la page demandée ou la première.</summary>
    public void ShowPage(Type? page)
    {
        var index = page is null ? -1 : Array.IndexOf(Pages, page);
        if (index >= 0 || Navigation.SelectedItem is null)
            Navigation.SelectedItem = Navigation.MenuItems[Math.Max(index, 0)];
        Activate();
        Ui.BringToFront(this);
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var index = sender.MenuItems.IndexOf(args.SelectedItem);
        if (index < 0 || ContentFrame.CurrentSourcePageType == Pages[index]) return;
        ContentFrame.Navigate(Pages[index], null, new EntranceNavigationTransitionInfo());
    }
}
