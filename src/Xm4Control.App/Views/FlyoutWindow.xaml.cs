using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.UI;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App.Views;

/// <summary>Popup rapide affiché au-dessus de l'horloge, qui se cache dès qu'on clique ailleurs.</summary>
public sealed partial class FlyoutWindow : Window
{
    private const int ContentWidth = 380;
    private const int ScreenMargin = 12;

    private DateTime _hiddenAt;

    internal FlyoutWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // Rendu Blender partagé avec la fenêtre principale ; lu depuis un flux (une URI de fichier vers un PNG
        // reste vide dans cette appli non empaquetée), décodé à la taille affichée pour la mémoire au repos.
        var image = new BitmapImage { DecodePixelWidth = 160 };
        using (var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Web", "headset.png")))
            image.SetSource(file.AsRandomAccessStream());
        Headset.Source = image;

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

    internal void HideFlyout()
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

    // --- Fonctions des liaisons x:Bind (paramètres booléens : réévaluées à chaque changement d'état) ---

    private static readonly Color Noise = Color.FromArgb(255, 0x8E, 0xC5, 0xFF);
    private static readonly Color Ambient = Color.FromArgb(255, 0xF2, 0xC2, 0x7B);
    private static readonly Color Wind = Color.FromArgb(255, 0x7F, 0xD8, 0xC8);
    private static readonly Color Neutral = Color.FromArgb(255, 0xA3, 0xAC, 0xB8);

    /// <summary>
    /// Halo derrière le casque, à la couleur du mode. Un nouveau pinceau à chaque fois : modifier les arrêts d'un
    /// RadialGradientBrush déjà affiché ne le redessine pas.
    /// </summary>
    private Brush ModeGlow(bool connected, bool noiseCancelling, bool ambient, bool wind)
    {
        var c = !connected ? Neutral : noiseCancelling ? Noise : ambient ? Ambient : wind ? Wind : Neutral;
        return new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(110, c.R, c.G, c.B), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(0, c.R, c.G, c.B), Offset = 1 },
            },
        };
    }

    private string ModeTitle(bool connected, bool noiseCancelling, bool ambient, bool wind, bool off) =>
        !connected ? "Déconnecté"
        : noiseCancelling ? "Réduction de bruit"
        : ambient ? "Son ambiant"
        : wind ? "Réduction du vent"
        : off ? "Désactivé"
        : "…";

    private string ModeHint(bool connected, bool noiseCancelling, bool ambient, bool wind, bool off) =>
        !connected ? ViewModel.DisconnectedHint
        : noiseCancelling ? "Coupe les bruits autour de vous."
        : ambient ? "Laisse entrer les sons extérieurs."
        : wind ? "Coupe le souffle du vent, dehors."
        : off ? "Son naturel, sans traitement."
        : "";

    private string DeviceCaps(string name) => name.ToUpperInvariant();

    private Brush StatusDot(bool connected) => new SolidColorBrush(connected
        ? Color.FromArgb(255, 0x3D, 0xCC, 0x8A)
        : Neutral);

    private string BatteryNumber(double level) => ((int)level).ToString();

    private string AmbientText(int level) => $"{level} / 20";

    private string AmbientFoot(bool ambient) => ambient ? "Voix en avant" : "Réglable en mode Son ambiant";

    private string MicHint(bool enabled) => enabled ? "Mains libres actif" : "Coupé : stéréo HD";

    // Petit interrupteur dessiné dans les bascules rapides.
    private Brush TrackBrush(bool on) => on
        ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
        : (Brush)Application.Current.Resources["ControlStrongFillColorDisabledBrush"];

    private Brush KnobBrush(bool on) => on
        ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
        : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

    private HorizontalAlignment KnobSide(bool on) => on ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    private void OnToggleMic(object sender, RoutedEventArgs e) => ViewModel.MicEnabled = !ViewModel.MicEnabled;

    private void OnToggleSpeakToChat(object sender, RoutedEventArgs e) => ViewModel.SpeakToChat = !ViewModel.SpeakToChat;

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
        App.Current.ShowSettings("eq");
    }
}
