using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Xm4Control.App.Views;

namespace Xm4Control.App;

/// <summary>Application résidente : icône de notification, popup rapide et fenêtre de réglages.</summary>
public partial class App : Application
{
    private EventWaitHandle? _showRequested;
    private RegisteredWaitHandle? _showRegistration;
    private TextWriter? _log;
    private HeadsetController? _headset;
    private TaskbarIcon? _tray;
    private GlobalHotkey? _hotkey;
    private readonly MemoryTrimmer _trimmer = new();
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private bool? _iconConnected;

    internal static new App Current => (App)Application.Current;

    internal MainViewModel ViewModel { get; private set; } = null!;

    /// <summary>Vrai pendant la fermeture : le popup se ferme au lieu de se cacher.</summary>
    internal bool IsQuitting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Xm4Control.Show", out var firstInstance);
        if (!firstInstance)
        {
            // Une instance tourne déjà : on lui demande d'afficher ses réglages.
            _showRequested.Set();
            IsQuitting = true;
            Shutdown();
            return;
        }

        // Garde l'entrée de démarrage alignée sur l'exe lancé (s'il a été mis à jour ou déplacé).
        if (AppSettings.StartWithWindows) AppSettings.StartWithWindows = true;
        ApplicationThemeManager.ApplySystemTheme();
        ApplyAccent(ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += (theme, _) => ApplyAccent(theme);
        if (SynchronizationContext.Current is null)
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));

        // --log : trace des trames échangées avec le casque, pour le débogage.
        _log = e.Args.Contains("--log") ? OpenLog() : null;
        var settings = AppSettings.Load();
        _headset = new HeadsetController(settings, _log);
        ViewModel = new MainViewModel(_headset, settings);
        ViewModel.PropertyChanged += (_, _) => UpdateTray();
        ViewModel.HotkeySettingChanged += ApplyHotkeySetting;
        ViewModel.LowBattery += level => _tray?.ShowNotification(
            title: "Batterie du casque faible",
            message: $"{ViewModel.DeviceName} : {level} % restants.",
            icon: NotificationIcon.Warning);

        var menu = (ContextMenu)FindResource("TrayMenu");
        menu.DataContext = ViewModel;
        _tray = new TaskbarIcon
        {
            NoLeftClickDelay = true,
            MenuActivation = PopupActivationMode.RightClick,
            ContextMenu = menu,
        };
        _tray.TrayLeftMouseUp += (_, _) => ToggleFlyout();
        UpdateTray();
        _tray.ForceCreate(enablesEfficiencyMode: false);

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += () => _ = ViewModel.CycleModeAsync();
        ApplyHotkeySetting();

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showRequested, (_, _) => Dispatcher.BeginInvoke(() => ShowSettings()), null, Timeout.Infinite, executeOnlyOnce: false);

        // --tray : lancement au démarrage de Windows, sans ouvrir de fenêtre.
        if (e.Args.Contains("--tray")) _trimmer.Schedule();
        else ShowSettings();
    }

    // Les fenêtres ne sont créées qu'à leur première ouverture : en arrière-plan, l'appli n'a que l'icône.
    private void ToggleFlyout()
    {
        if (_flyout is null)
        {
            _flyout = new FlyoutWindow(ViewModel);
            _flyout.IsVisibleChanged += (_, _) =>
            {
                if (!_flyout.IsVisible) _trimmer.Schedule();
            };
        }
        _flyout.Toggle();
    }

    // La fenêtre Réglages est détruite à sa fermeture, et recréée à la demande.
    internal void ShowSettings(Type? page = null)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(ViewModel);
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                _trimmer.Schedule();
            };
        }
        _settingsWindow.ShowPage(page);
    }

    // Accent fixe, le bleu de l'icône : l'accent Windows peut être gris, et les éléments actifs
    // (mode sélectionné, interrupteurs) auraient l'air désactivés. Les nuances suivent le thème clair/sombre.
    private static void ApplyAccent(ApplicationTheme theme) =>
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x00, 0x78, 0xD4), theme, false, false);

    internal void Quit()
    {
        IsQuitting = true;
        Shutdown();
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e) => ShowSettings();

    private void OnQuit(object sender, RoutedEventArgs e) => Quit();

    private void ApplyHotkeySetting()
    {
        if (_hotkey is null) return;
        if (ViewModel.HotkeyEnabled)
        {
            ViewModel.HotkeyError = _hotkey.Register() ? null : $"{GlobalHotkey.Label} est déjà utilisé par une autre application.";
        }
        else
        {
            _hotkey.Unregister();
            ViewModel.HotkeyError = null;
        }
    }

    private void UpdateTray()
    {
        if (_tray is null) return;
        _tray.ToolTipText = ViewModel.Summary;
        if (ViewModel.IsConnected == _iconConnected) return;
        _iconConnected = ViewModel.IsConnected;
        var previous = _tray.Icon;
        _tray.Icon = TrayIcons.Create(ViewModel.IsConnected);
        previous?.Dispose();
    }

    // Thème clair/sombre de la barre des tâches changé : on redessine l'icône.
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        Dispatcher.BeginInvoke(() =>
        {
            _iconConnected = null;
            UpdateTray();
        });
    }

    private static TextWriter OpenLog()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xm4Control", "trace.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return TextWriter.Synchronized(new StreamWriter(path, append: false) { AutoFlush = true });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trimmer.Stop();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _showRegistration?.Unregister(null);
        _hotkey?.Dispose();
        _tray?.Dispose();
        _headset?.Dispose();
        _log?.Dispose();
        _showRequested?.Dispose();
        base.OnExit(e);
    }
}
