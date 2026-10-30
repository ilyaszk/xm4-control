using System.IO;
using System.Windows.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Xm4Control.App.Views;
using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App;

/// <summary>Application résidente : icône de notification, popup rapide et fenêtre de réglages.</summary>
public partial class App : Application
{
    private readonly EventWaitHandle _showRequested;
    private readonly string[] _args;
    private readonly MemoryTrimmer _trimmer;
    private readonly UISettings _uiSettings = new();
    private DispatcherQueue _dispatcher = null!;
    private RegisteredWaitHandle? _showRegistration;
    private TextWriter? _log;
    private HeadsetController? _headset;
    private TaskbarIcon? _tray;
    private TrayMenu? _menu;
    private GlobalHotkey? _hotkey;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private bool? _iconConnected;

    internal App(EventWaitHandle showRequested, string[] args)
    {
        _showRequested = showRequested;
        _args = args;
        _trimmer = new MemoryTrimmer(() => _flyout?.IsShown == true || _settingsWindow is not null);
        InitializeComponent();
        // L'appli vit dans la zone de notification : fermer la dernière fenêtre ne doit pas la quitter.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    internal static new App Current => (App)Application.Current;

    internal MainViewModel ViewModel { get; private set; } = null!;

    /// <summary>Vrai pendant la fermeture : le popup se ferme au lieu de se cacher.</summary>
    internal bool IsQuitting { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Garde l'entrée de démarrage alignée sur l'exe lancé (s'il a été mis à jour ou déplacé).
        if (AppSettings.StartWithWindows) AppSettings.StartWithWindows = true;

        // --log : trace des trames échangées avec le casque, pour le débogage.
        _log = _args.Contains("--log") ? OpenLog() : null;
        var settings = AppSettings.Load();
        _headset = new HeadsetController(settings, _log);
        ViewModel = new MainViewModel(_headset, settings);
        ViewModel.PropertyChanged += (_, _) => UpdateTray();
        ViewModel.HotkeySettingChanged += ApplyHotkeySetting;
        ViewModel.LowBattery += level => _tray?.ShowNotification(
            title: "Batterie du casque faible",
            message: $"{ViewModel.DeviceName} : {level} % restants.",
            icon: NotificationIcon.Warning);

        Ui.FollowSystemThemeInMenus();
        CreateTray();

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += () => _ = ViewModel.CycleModeAsync();
        ApplyHotkeySetting();

        // Thème clair/sombre de la barre des tâches changé : on redessine l'icône (événement hors thread UI).
        _uiSettings.ColorValuesChanged += (_, _) => _dispatcher.TryEnqueue(() =>
        {
            _iconConnected = null;
            UpdateTray();
        });

        _showRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showRequested, (_, _) => _dispatcher.TryEnqueue(() => ShowSettings()), null, Timeout.Infinite, executeOnlyOnce: false);

        // --tray : lancement au démarrage de Windows, sans ouvrir de fenêtre.
        if (_args.Contains("--tray")) _trimmer.Schedule();
        else ShowSettings();
    }

    // Les fenêtres ne sont créées qu'à leur première ouverture : en arrière-plan, l'appli n'a que l'icône.
    private void ToggleFlyout()
    {
        if (_flyout is null)
        {
            _flyout = new FlyoutWindow(ViewModel);
            _flyout.Hidden += _trimmer.Schedule;
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

    internal void Quit()
    {
        IsQuitting = true;
        _trimmer.Stop();
        _showRegistration?.Unregister(null);
        _hotkey?.Dispose();
        _tray?.Dispose();
        _headset?.Dispose();
        _log?.Dispose();
        _showRequested.Dispose();
        _settingsWindow?.Close();
        _flyout?.Close();
        Exit();
    }

    private void CreateTray()
    {
        _menu = new TrayMenu(this);
        _tray = new TaskbarIcon
        {
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = _menu.Flyout,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
            LeftClickCommand = new ActionCommand(ToggleFlyout),
        };
        UpdateTray();
        _tray.ForceCreate(enablesEfficiencyMode: false);
    }

    private void UpdateTray()
    {
        if (_tray is null) return;
        _tray.ToolTipText = ViewModel.Summary;
        _menu?.Update(ViewModel);
        if (ViewModel.IsConnected == _iconConnected) return;
        _iconConnected = ViewModel.IsConnected;
        var previous = _tray.Icon;
        _tray.Icon = TrayIcons.Create(ViewModel.IsConnected);
        previous?.Dispose();
    }

    private void ApplyHotkeySetting()
    {
        if (_hotkey is null) return;
        if (ViewModel.HotkeyEnabled)
        {
            ViewModel.HotkeyError = _hotkey.Register() ? "" : $"{GlobalHotkey.Label} est déjà utilisé par une autre application.";
        }
        else
        {
            _hotkey.Unregister();
            ViewModel.HotkeyError = "";
        }
    }

    private static TextWriter OpenLog()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xm4Control", "trace.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return TextWriter.Synchronized(new StreamWriter(path, append: false) { AutoFlush = true });
    }

    /// <summary>Menu du clic droit sur l'icône, reconstruit à partir de l'état à chaque changement.</summary>
    private sealed class TrayMenu
    {
        private readonly MenuFlyoutItem _header = new() { IsEnabled = false };
        private readonly Dictionary<AmbientMode, ToggleMenuFlyoutItem> _modes = [];
        private readonly ToggleMenuFlyoutItem _mic = new() { Text = "Micro du casque" };

        public TrayMenu(App app)
        {
            Flyout.Items.Add(_header);
            Flyout.Items.Add(new MenuFlyoutSeparator());
            foreach (var mode in Labels.ModeOrder)
            {
                var item = new ToggleMenuFlyoutItem { Text = Labels.Of(mode), Command = new ActionCommand(() => app.ViewModel.SetMode(mode)) };
                _modes[mode] = item;
                Flyout.Items.Add(item);
            }
            Flyout.Items.Add(new MenuFlyoutSeparator());
            _mic.Command = new ActionCommand(() => app.ViewModel.MicEnabled = !app.ViewModel.MicEnabled);
            Flyout.Items.Add(_mic);
            Flyout.Items.Add(new MenuFlyoutSeparator());
            Flyout.Items.Add(new MenuFlyoutItem { Text = "Réglages…", Command = new ActionCommand(() => app.ShowSettings()) });
            Flyout.Items.Add(new MenuFlyoutItem { Text = "Quitter", Command = new ActionCommand(app.Quit) });
        }

        public MenuFlyout Flyout { get; } = new();

        public void Update(MainViewModel vm)
        {
            _header.Text = vm.Summary;
            foreach (var (mode, item) in _modes)
            {
                item.IsChecked = vm.Mode == mode;
                item.IsEnabled = vm.IsConnected;
            }
            _mic.IsChecked = vm.MicEnabled;
            _mic.IsEnabled = vm.MicAvailable;
        }
    }
}

internal sealed class ActionCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
