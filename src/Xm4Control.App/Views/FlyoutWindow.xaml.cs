using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Xm4Control.App.Views.Pages;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App.Views;

/// <summary>Popup rapide affiché au-dessus de l'horloge, qui se cache dès qu'on clique ailleurs.</summary>
public sealed partial class FlyoutWindow : Window
{
    private const int ContentWidth = 360;
    private const int ScreenMargin = 12;

    private DateTime _hiddenAt;

    internal FlyoutWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // Fenêtre sans barre de titre, au premier plan, absente de la barre des tâches et d'Alt+Tab.
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(true, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetIcon(Ui.IconPath);

        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated) HideFlyout();
        };
        AppWindow.Closing += (_, e) =>
        {
            if (App.Current.IsQuitting) return;
            e.Cancel = true;
            HideFlyout();
        };
        // La hauteur suit le contenu (message d'erreur, etc.). Redimensionner pendant la mise en page
        // serait ignoré : on le fait juste après.
        Root.SizeChanged += (_, _) => DispatcherQueue.TryEnqueue(Place);
    }

    internal MainViewModel ViewModel { get; }

    public bool IsShown => AppWindow.IsVisible;

    /// <summary>Le popup vient d'être caché.</summary>
    public event Action? Hidden;

    public void Toggle()
    {
        if (AppWindow.IsVisible)
        {
            HideFlyout();
            return;
        }
        // Cliquer sur l'icône désactive d'abord le popup, qui se cache : on ne le rouvre pas aussitôt.
        if (DateTime.UtcNow - _hiddenAt < TimeSpan.FromMilliseconds(400)) return;
        Place();
        AppWindow.Show(true);
        Activate();
        Ui.BringToFront(this);
    }

    private void HideFlyout()
    {
        if (!AppWindow.IsVisible) return;
        AppWindow.Hide();
        _hiddenAt = DateTime.UtcNow;
        Hidden?.Invoke();
    }

    /// <summary>Dimensionne la fenêtre sur son contenu et la place en bas à droite de l'écran principal.</summary>
    private void Place()
    {
        var scale = Ui.Scale(this);
        var height = Root.ActualHeight > 0 ? Root.ActualHeight : 440;
        // Taille extérieure = contenu + bordures (écart actuel entre taille de la fenêtre et zone cliente).
        var width = (int)Math.Ceiling(ContentWidth * scale) + AppWindow.Size.Width - AppWindow.ClientSize.Width;
        var outerHeight = (int)Math.Ceiling(height * scale) + AppWindow.Size.Height - AppWindow.ClientSize.Height;

        var area = DisplayArea.Primary.WorkArea;
        var margin = (int)(ScreenMargin * scale);
        AppWindow.MoveAndResize(new RectInt32(
            area.X + area.Width - width - margin,
            area.Y + area.Height - outerHeight - margin,
            width,
            outerHeight));
    }

    private void OnNoiseCancelling(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.NoiseCancelling);

    private void OnAmbient(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.Ambient);

    private void OnWindReduction(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.WindReduction);

    private void OnOff(object sender, RoutedEventArgs e) => ViewModel.SetMode(AmbientMode.Off);

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
}
