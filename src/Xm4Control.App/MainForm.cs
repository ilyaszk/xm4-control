using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App;

internal sealed class MainForm : Form
{
    private static readonly Color Accent = Color.FromArgb(0, 103, 192);
    private const int LeftWidth = 520;

    private readonly HeadsetController _headset;
    private readonly Label _status = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 0) };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Color.IndianRed, MaximumSize = new Size(LeftWidth * 2, 0) };
    private readonly Dictionary<AmbientMode, CheckBox> _modes = [];
    private readonly TrackBar _level = new()
    {
        Minimum = Commands.MinAmbientLevel,
        Maximum = Commands.MaxAmbientLevel,
        TickStyle = TickStyle.None,
        AutoSize = false,
        Size = new Size(320, 30),
    };
    private readonly Label _levelValue = new() { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
    private readonly CheckBox _focusVoice = Option("Focus sur la voix");
    private readonly CheckBox _mic = Option("Micro du casque (profil mains libres de Windows)");
    private readonly ComboBox _eqPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, FormattingEnabled = true };
    private readonly TrackBar[] _bands = new TrackBar[Commands.EqBandCount];
    private readonly Label[] _bandValues = new Label[Commands.EqBandCount];
    private readonly CheckBox _dsee = Option("DSEE Extreme (améliore la musique compressée)");
    private readonly CheckBox _speakToChat = Option("Speak-to-Chat (met en pause quand vous parlez)");
    private readonly CheckBox _pause = Option("Pause quand on retire le casque");
    private readonly CheckBox _touch = Option("Pavé tactile");
    private readonly CheckBox _voice = Option("Guide vocal");
    private readonly CheckBox _autostart = Option("Lancer au démarrage de Windows");
    private readonly Button _powerOff = new() { Text = "Éteindre le casque", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
    private readonly System.Windows.Forms.Timer _levelDebounce = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _eqDebounce = new() { Interval = 300 };
    private readonly List<Control> _needsConnection = [];
    private bool _updating;

    public MainForm(HeadsetController headset)
    {
        _headset = headset;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "XM4 Control";
        Icon = AppIcons.Window();
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.Manual;

        var left = Column(
            Header("Contrôle du son ambiant"),
            BuildModes(),
            Row(new Label { Text = "Niveau", AutoSize = true, Margin = new Padding(3, 6, 6, 0) }, _level, _levelValue),
            _focusVoice,
            Header("Micro"),
            _mic,
            Hint("Décoché, le casque reste en stéréo haute qualité : Windows ne le bascule plus en mode " +
                 "« téléphone » quand une appli ouvre le micro (Teams, Discord…), mais son micro n'est plus utilisable."),
            Header("Options du casque"),
            _dsee, _speakToChat, _pause, _touch, _voice);

        var right = Column(
            Header("Égaliseur"),
            _eqPreset,
            BuildEqualizer(),
            Header("Application"),
            _autostart,
            _powerOff);
        right.Margin = new Padding(24, 0, 0, 0);

        var title = new Label
        {
            Text = "WH-1000XM4",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15F),
        };
        var header = Column(title, _status, _error);
        var root = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(16, 10, 16, 16) };
        root.Controls.Add(header, 0, 0);
        root.SetColumnSpan(header, 2);
        root.Controls.Add(left, 0, 1);
        root.Controls.Add(right, 1, 1);
        Controls.Add(root);
        ResumeLayout(false);
        PerformLayout();

        _needsConnection.AddRange([.. _modes.Values, _eqPreset, .. _bands, _dsee, _speakToChat, _pause, _touch, _voice, _powerOff]);
        WireEvents();
        _headset.Changed += UpdateView;
        UpdateView();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 12, Math.Max(area.Top, area.Bottom - Height - 12));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // La fenêtre se cache : l'appli reste dans la zone de notification.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    private FlowLayoutPanel BuildModes()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        foreach (var mode in Labels.ModeOrder)
        {
            // Pas de RadioButton : WinForms en "clique" un quand il prend le focus, ce qui changerait
            // le mode à l'ouverture de la fenêtre. Ces boutons ne reflètent que l'état renvoyé par le casque.
            var button = new CheckBox
            {
                Text = Labels.Of(mode),
                Appearance = Appearance.Button,
                AutoCheck = false,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(124, 36),
                Margin = new Padding(3, 0, 3, 0),
            };
            button.FlatAppearance.CheckedBackColor = Accent;
            _modes[mode] = button;
            panel.Controls.Add(button);
        }
        return panel;
    }

    private TableLayoutPanel BuildEqualizer()
    {
        var grid = new TableLayoutPanel { ColumnCount = Commands.EqBandCount, RowCount = 3, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        for (var i = 0; i < Commands.EqBandCount; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            _bandValues[i] = new Label { AutoSize = true, Anchor = AnchorStyles.None };
            _bands[i] = new TrackBar
            {
                Orientation = Orientation.Vertical,
                Minimum = Commands.EqBandMin,
                Maximum = Commands.EqBandMax,
                TickFrequency = 5,
                TickStyle = TickStyle.Both,
                LargeChange = 2,
                AutoSize = false,
                Size = new Size(45, 150),
                Anchor = AnchorStyles.None,
            };
            grid.Controls.Add(_bandValues[i], i, 0);
            grid.Controls.Add(_bands[i], i, 1);
            grid.Controls.Add(new Label { Text = Labels.EqBands[i], AutoSize = true, Anchor = AnchorStyles.None }, i, 2);
        }

        _eqPreset.Items.AddRange([.. Enum.GetValues<EqPreset>().Cast<object>()]);
        _eqPreset.Format += (_, e) => e.Value = e.ListItem is EqPreset p ? Labels.Of(p) : e.Value;
        return grid;
    }

    private void WireEvents()
    {
        foreach (var (mode, button) in _modes)
        {
            button.CheckedChanged += (_, _) => button.ForeColor = button.Checked ? Color.White : Color.Empty;
            button.Click += (_, _) =>
            {
                if (!button.Checked) _ = _headset.SetAmbientModeAsync(mode);
            };
        }

        _level.ValueChanged += (_, _) =>
        {
            _levelValue.Text = _level.Value.ToString();
            if (!_updating) Restart(_levelDebounce);
        };
        _levelDebounce.Tick += (_, _) =>
        {
            _levelDebounce.Stop();
            _ = _headset.SetAmbientLevelAsync(_level.Value, _focusVoice.Checked);
        };
        _focusVoice.CheckedChanged += (_, _) =>
        {
            if (!_updating) _ = _headset.SetAmbientLevelAsync(_level.Value, _focusVoice.Checked);
        };

        _mic.CheckedChanged += (_, _) =>
        {
            if (!_updating) _ = _headset.SetMicAsync(_mic.Checked);
        };

        _eqPreset.SelectedIndexChanged += (_, _) =>
        {
            if (!_updating && _eqPreset.SelectedItem is EqPreset preset) _ = _headset.SetEqualizerPresetAsync(preset);
        };
        foreach (var band in _bands)
        {
            band.ValueChanged += (_, _) =>
            {
                UpdateBandLabels();
                if (!_updating) Restart(_eqDebounce);
            };
        }
        _eqDebounce.Tick += (_, _) =>
        {
            _eqDebounce.Stop();
            _ = _headset.SetEqualizerBandsAsync([.. _bands.Select(b => b.Value)]);
        };

        Toggle(_dsee, (c, on) => c.SetDseeAsync(on));
        Toggle(_speakToChat, (c, on) => c.SetSpeakToChatAsync(on));
        Toggle(_pause, (c, on) => c.SetPauseWhenTakenOffAsync(on));
        Toggle(_touch, (c, on) => c.SetTouchSensorAsync(on));
        Toggle(_voice, (c, on) => c.SetVoiceGuidanceAsync(on));

        _autostart.Checked = AppSettings.StartWithWindows;
        _autostart.CheckedChanged += (_, _) => AppSettings.StartWithWindows = _autostart.Checked;

        _powerOff.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Éteindre le casque ?", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                _ = _headset.RunAsync(c => c.PowerOffAsync());
        };
    }

    private void Toggle(CheckBox box, Func<Xm4Client, bool, Task> action)
    {
        box.CheckedChanged += (_, _) =>
        {
            if (_updating) return;
            var on = box.Checked;
            _ = _headset.RunAsync(c => action(c, on));
        };
    }

    private void UpdateView()
    {
        _updating = true;
        try
        {
            var state = _headset.State;
            var connected = _headset.Status == ConnectionStatus.Connected;

            _status.Text = _headset.StatusText;
            _error.Text = _headset.Error ?? "";
            _error.Visible = _headset.Error is not null;
            foreach (var control in _needsConnection) control.Enabled = connected;

            var ambient = state.Ambient;
            foreach (var (mode, button) in _modes) button.Checked = connected && ambient?.Mode == mode;
            var ambientOn = connected && ambient?.Mode == AmbientMode.Ambient;
            _level.Enabled = _focusVoice.Enabled = ambientOn;
            if (!_levelDebounce.Enabled)
                _level.Value = ambient is { Mode: AmbientMode.Ambient, Level: > 0 } a ? a.Level : _headset.AmbientLevel;
            _focusVoice.Checked = ambient is { Mode: AmbientMode.Ambient } current ? current.FocusOnVoice : _headset.FocusOnVoice;
            _levelValue.Text = _level.Value.ToString();

            _mic.Enabled = _headset.MicEnabled.HasValue;
            _mic.Checked = _headset.MicEnabled ?? false;

            _eqPreset.SelectedIndex = -1;
            if (state.EqPreset is { } preset) _eqPreset.SelectedItem = preset;
            if (state.EqBands is { } bands && !_eqDebounce.Enabled)
            {
                for (var i = 0; i < _bands.Length; i++)
                    _bands[i].Value = Math.Clamp(bands[i], Commands.EqBandMin, Commands.EqBandMax);
            }
            UpdateBandLabels();

            SetOption(_dsee, state.Dsee, connected);
            SetOption(_speakToChat, state.SpeakToChat, connected);
            SetOption(_pause, state.PauseWhenTakenOff, connected);
            SetOption(_touch, state.TouchSensor, connected);
            SetOption(_voice, state.VoiceGuidance, connected);
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateBandLabels()
    {
        for (var i = 0; i < _bands.Length; i++)
            _bandValues[i].Text = _bands[i].Value > 0 ? $"+{_bands[i].Value}" : _bands[i].Value.ToString();
    }

    private static void SetOption(CheckBox box, bool? value, bool connected)
    {
        box.Checked = value ?? false;
        box.Enabled = connected && value.HasValue;
    }

    private static void Restart(System.Windows.Forms.Timer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private static CheckBox Option(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };

    private static Label Header(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10.5F),
        Margin = new Padding(0, 14, 0, 6),
    };

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(LeftWidth, 0),
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(20, 0, 3, 0),
    };

    private static FlowLayoutPanel Column(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty };
        panel.Controls.AddRange(controls);
        return panel;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { WrapContents = false, AutoSize = true, Margin = Padding.Empty };
        panel.Controls.AddRange(controls);
        return panel;
    }
}
