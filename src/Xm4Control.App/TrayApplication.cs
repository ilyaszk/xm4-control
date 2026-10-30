using Microsoft.Win32;
using Xm4Control.Core;

namespace Xm4Control.App;

/// <summary>Application résidente : icône dans la zone de notification, menu rapide et fenêtre de réglages.</summary>
internal sealed class TrayApplication : ApplicationContext
{
    private readonly SynchronizationContext _ui;
    private readonly HeadsetController _headset;
    private readonly MainForm _form;
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly RegisteredWaitHandle _showRequests;
    private bool? _iconConnected;

    public TrayApplication(EventWaitHandle showRequested, bool startHidden, TextWriter? log)
    {
        _ui = SynchronizationContext.Current!;
        _headset = new HeadsetController(AppSettings.Load(), log);
        _form = new MainForm(_headset);

        _tray.ContextMenuStrip = _menu;
        _menu.Opening += (_, e) =>
        {
            BuildMenu();
            e.Cancel = false;
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowForm();
        };
        _headset.Changed += UpdateTray;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        UpdateTray();
        _tray.Visible = true;

        // Une deuxième instance lancée signale l'événement : on affiche la fenêtre existante.
        _showRequests = ThreadPool.RegisterWaitForSingleObject(
            showRequested, (_, _) => _ui.Post(_ => ShowForm(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        if (!startHidden) ShowForm();
    }

    private void ShowForm()
    {
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        _menu.Items.Add(new ToolStripMenuItem(_headset.Summary) { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());

        var connected = _headset.Status == ConnectionStatus.Connected;
        var current = _headset.State.Ambient?.Mode;
        foreach (var mode in Labels.ModeOrder)
        {
            _menu.Items.Add(new ToolStripMenuItem(Labels.Of(mode), null, (_, _) => _ = _headset.SetAmbientModeAsync(mode))
            {
                Checked = connected && current == mode,
                Enabled = connected,
            });
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Micro du casque", null, (_, _) => _ = _headset.SetMicAsync(_headset.MicEnabled != true))
        {
            Checked = _headset.MicEnabled == true,
            Enabled = _headset.MicEnabled.HasValue,
        });

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Ouvrir", null, (_, _) => ShowForm());
        _menu.Items.Add("Quitter", null, (_, _) => ExitThread());
    }

    private void UpdateTray()
    {
        var summary = _headset.Summary;
        _tray.Text = summary.Length > 127 ? summary[..127] : summary;

        var connected = _headset.Status == ConnectionStatus.Connected;
        if (connected != _iconConnected) SetTrayIcon(connected);
    }

    private void SetTrayIcon(bool connected)
    {
        var previous = _tray.Icon;
        _tray.Icon = AppIcons.Tray(connected);
        previous?.Dispose();
        _iconConnected = connected;
    }

    // Changement de thème clair/sombre de la barre des tâches.
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General) _ui.Post(_ => SetTrayIcon(_iconConnected ?? false), null);
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _showRequests.Unregister(null);
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _headset.Dispose();
        _form.Dispose();
        base.ExitThreadCore();
    }
}
