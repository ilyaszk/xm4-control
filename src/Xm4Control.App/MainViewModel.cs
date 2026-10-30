using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App;

internal sealed record PresetItem(EqPreset Preset, string Label);

/// <summary>
/// État affiché par les fenêtres. Les setters envoient la commande au casque ; l'état réel revient
/// par <see cref="HeadsetController.Changed"/>, qui rafraîchit toutes les liaisons.
/// </summary>
internal sealed class MainViewModel : INotifyPropertyChanged
{
    public const int LowBatteryThreshold = 20;

    private readonly HeadsetController _headset;
    private readonly AppSettings _settings;
    private bool _lowBatteryNotified;
    private string? _hotkeyError;

    public MainViewModel(HeadsetController headset, AppSettings settings)
    {
        _headset = headset;
        _settings = settings;
        Presets = [.. Enum.GetValues<EqPreset>().Select(p => new PresetItem(p, Labels.Of(p)))];
        SetModeCommand = new RelayCommand(p => _ = _headset.SetAmbientModeAsync((AmbientMode)p!));
        _headset.Changed += OnHeadsetChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>La batterie vient de passer sous le seuil (une fois par décharge).</summary>
    public event Action<int>? LowBattery;

    /// <summary>Le raccourci global a été activé ou désactivé dans les réglages.</summary>
    public event Action? HotkeySettingChanged;

    private Xm4State State => _headset.State;

    public bool IsConnected => _headset.Status == ConnectionStatus.Connected;
    public string DeviceName => _headset.Device?.Name ?? "WH-1000XM4";
    public string Summary => _headset.Summary;
    public string StatusText => IsConnected
        ? string.Join(" · ", new[] { "Connecté", State.Codec }.Where(s => s is not null))
        : _headset.StatusText;
    public string? Error => _headset.Error;
    public bool HasError => _headset.Error is not null;
    public bool ShowDisconnected => !IsConnected;
    public string DisconnectedHint => _headset.Status switch
    {
        ConnectionStatus.NoDevice => "Appairez-le dans Paramètres > Bluetooth et appareils.",
        ConnectionStatus.Connecting or ConnectionStatus.Searching => "",
        _ => "Allumez-le ou connectez-le à ce PC : l'appli s'y connecte toute seule.",
    };

    public bool HasBattery => IsConnected && State.Battery.HasValue;
    public string BatteryText => State.Battery is { } b ? $"{b} %" : "";
    public SymbolRegular BatterySymbol => State switch
    {
        { Charging: true } => SymbolRegular.BatteryCharge24,
        { Battery: { } b } when b <= LowBatteryThreshold => SymbolRegular.BatteryWarning24,
        { Battery: { } b } when Enum.TryParse<SymbolRegular>($"Battery{Math.Clamp((b + 5) / 10, 0, 10)}24", out var symbol) => symbol,
        _ => SymbolRegular.Battery024,
    };
    public string Firmware => State.Firmware ?? "—";
    public string Codec => State.Codec ?? "—";
    public string BatteryDetail => State.Battery is { } b ? $"{b} %{(State.Charging ? " (en charge)" : "")}" : "—";

    public ICommand SetModeCommand { get; }
    public AmbientMode? Mode => IsConnected ? State.Ambient?.Mode : null;
    public bool IsNoiseCancelling => Mode == AmbientMode.NoiseCancelling;
    public bool IsAmbient => Mode == AmbientMode.Ambient;
    public bool IsWindReduction => Mode == AmbientMode.WindReduction;
    public bool IsOff => Mode == AmbientMode.Off;

    public int AmbientLevel
    {
        get => State.Ambient is { Mode: AmbientMode.Ambient, Level: > 0 } a ? a.Level : _headset.AmbientLevel;
        set => _ = _headset.SetAmbientLevelAsync(value, FocusOnVoice);
    }

    public bool FocusOnVoice
    {
        get => State.Ambient is { Mode: AmbientMode.Ambient } a ? a.FocusOnVoice : _headset.FocusOnVoice;
        set => _ = _headset.SetAmbientLevelAsync(AmbientLevel, value);
    }

    public bool MicAvailable => _headset.MicEnabled.HasValue;

    public bool MicEnabled
    {
        get => _headset.MicEnabled ?? false;
        set => _ = _headset.SetMicAsync(value);
    }

    public IReadOnlyList<PresetItem> Presets { get; }

    public PresetItem? SelectedPreset
    {
        get => Presets.FirstOrDefault(p => p.Preset == State.EqPreset);
        set
        {
            if (value is not null && value.Preset != State.EqPreset) _ = _headset.SetEqualizerPresetAsync(value.Preset);
        }
    }

    public string EqualizerLabel => State.EqPreset is { } p ? Labels.Of(p) : "Égaliseur";

    public IReadOnlyList<int> EqualizerBands
    {
        get => State.EqBands ?? new int[Commands.EqBandCount];
        set => _ = _headset.SetEqualizerBandsAsync([.. value]);
    }

    public bool HasDsee => IsConnected && State.Dsee.HasValue;
    public bool Dsee
    {
        get => State.Dsee ?? false;
        set => _ = _headset.RunAsync(c => c.SetDseeAsync(value));
    }

    public bool HasSpeakToChat => IsConnected && State.SpeakToChat.HasValue;
    public bool SpeakToChat
    {
        get => State.SpeakToChat ?? false;
        set => _ = _headset.RunAsync(c => c.SetSpeakToChatAsync(value));
    }

    public bool HasPauseWhenTakenOff => IsConnected && State.PauseWhenTakenOff.HasValue;
    public bool PauseWhenTakenOff
    {
        get => State.PauseWhenTakenOff ?? false;
        set => _ = _headset.RunAsync(c => c.SetPauseWhenTakenOffAsync(value));
    }

    public bool HasTouchSensor => IsConnected && State.TouchSensor.HasValue;
    public bool TouchSensor
    {
        get => State.TouchSensor ?? false;
        set => _ = _headset.RunAsync(c => c.SetTouchSensorAsync(value));
    }

    public bool HasVoiceGuidance => IsConnected && State.VoiceGuidance.HasValue;
    public bool VoiceGuidance
    {
        get => State.VoiceGuidance ?? false;
        set => _ = _headset.RunAsync(c => c.SetVoiceGuidanceAsync(value));
    }

    public bool StartWithWindows
    {
        get => AppSettings.StartWithWindows;
        set
        {
            AppSettings.StartWithWindows = value;
            OnPropertyChanged();
        }
    }

    public bool HotkeyEnabled
    {
        get => _settings.HotkeyEnabled;
        set
        {
            _settings.HotkeyEnabled = value;
            _settings.Save();
            HotkeySettingChanged?.Invoke();
            OnPropertyChanged();
        }
    }

    public string? HotkeyError
    {
        get => _hotkeyError;
        set
        {
            _hotkeyError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasHotkeyError));
        }
    }

    public bool HasHotkeyError => _hotkeyError is not null;

    public bool LowBatteryAlert
    {
        get => _settings.LowBatteryAlert;
        set
        {
            _settings.LowBatteryAlert = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string Version => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "";

    /// <summary>Même cycle que le bouton NC/AMB du casque.</summary>
    public Task CycleModeAsync() => _headset.SetAmbientModeAsync(Mode switch
    {
        AmbientMode.NoiseCancelling => AmbientMode.Ambient,
        AmbientMode.Ambient => AmbientMode.Off,
        _ => AmbientMode.NoiseCancelling,
    });

    public Task PowerOffAsync() => _headset.RunAsync(c => c.PowerOffAsync());

    private void OnHeadsetChanged()
    {
        CheckBattery();
        OnPropertyChanged(string.Empty);
    }

    private void CheckBattery()
    {
        if (!IsConnected || State.Battery is not { } level) return;
        if (State.Charging || level > LowBatteryThreshold)
        {
            _lowBatteryNotified = false;
            return;
        }
        if (_lowBatteryNotified || !_settings.LowBatteryAlert) return;
        _lowBatteryNotified = true;
        LowBattery?.Invoke(level);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}
