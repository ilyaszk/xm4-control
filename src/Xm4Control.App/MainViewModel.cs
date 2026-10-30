using System.ComponentModel;
using System.Runtime.CompilerServices;
using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App;

/// <summary>
/// État affiché par les fenêtres. Les setters envoient la commande au casque ; l'état réel revient
/// par <see cref="HeadsetController.Changed"/>, qui rafraîchit toutes les liaisons.
/// Les liaisons x:Bind bidirectionnelles réécrivent la valeur affichée à chaque rafraîchissement :
/// chaque setter ignore donc une valeur identique à l'état courant.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public const int LowBatteryThreshold = 20;

    private readonly HeadsetController _headset;
    private readonly AppSettings _settings;
    private bool _lowBatteryNotified;
    private string? _hotkeyError;
    private CancellationTokenSource? _levelDebounce;

    internal MainViewModel(HeadsetController headset, AppSettings settings)
    {
        _headset = headset;
        _settings = settings;
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
    public string Error => _headset.Error ?? "";
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

    /// <summary>Glyphe Segoe Fluent Icons : Battery0 à Battery9 = E850 à E859, Battery10 = E83F.</summary>
    public string BatteryGlyph => State switch
    {
        { Charging: true } => Glyph(0xE83E),
        { Battery: { } b } when (b + 5) / 10 >= 10 => Glyph(0xE83F),
        { Battery: { } b } => Glyph(0xE850 + Math.Max(0, (b + 5) / 10)),
        _ => Glyph(0xE850),
    };

    public string Firmware => State.Firmware ?? "—";
    public string Codec => State.Codec ?? "—";
    public string BatteryDetail => State.Battery is { } b ? $"{b} %{(State.Charging ? " (en charge)" : "")}" : "—";

    // --- Son ambiant ---

    public AmbientMode? Mode => IsConnected ? State.Ambient?.Mode : null;
    public bool IsNoiseCancelling => Mode == AmbientMode.NoiseCancelling;
    public bool IsAmbient => Mode == AmbientMode.Ambient;
    public bool IsWindReduction => Mode == AmbientMode.WindReduction;
    public bool IsOff => Mode == AmbientMode.Off;

    public void SetMode(AmbientMode mode) => _ = _headset.SetAmbientModeAsync(mode);

    public int AmbientLevel => State.Ambient is { Mode: AmbientMode.Ambient, Level: > 0 } a ? a.Level : _headset.AmbientLevel;

    /// <summary>Valeur du curseur ; envoyée au casque 300 ms après le dernier mouvement.</summary>
    public double AmbientLevelValue
    {
        get => AmbientLevel;
        set
        {
            var level = (int)Math.Round(value);
            if (level == AmbientLevel) return;
            _levelDebounce?.Cancel();
            var debounce = _levelDebounce = new CancellationTokenSource();
            _ = SendLevelAsync(level, debounce.Token);
        }
    }

    private async Task SendLevelAsync(int level, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await _headset.SetAmbientLevelAsync(level, FocusOnVoice);
    }

    public bool FocusOnVoice
    {
        get => State.Ambient is { Mode: AmbientMode.Ambient } a ? a.FocusOnVoice : _headset.FocusOnVoice;
        set
        {
            if (value != FocusOnVoice) _ = _headset.SetAmbientLevelAsync(AmbientLevel, value);
        }
    }

    public bool IsOptimizing => State.NcOptimizer is NcOptimizerStatus.CheckingFit
        or NcOptimizerStatus.MeasuringPressure or NcOptimizerStatus.Analyzing;
    public string OptimizerStatus => Labels.Of(State.NcOptimizer);
    public bool HasOptimizerStatus => IsConnected && OptimizerStatus.Length > 0;
    public string OptimizerButtonText => IsOptimizing ? "Arrêter" : "Lancer";

    public Task ToggleOptimizerAsync()
    {
        var start = !IsOptimizing;
        return _headset.RunAsync(c => c.StartNcOptimizerAsync(start));
    }

    // --- Micro ---

    public bool MicAvailable => _headset.MicEnabled.HasValue;

    public bool MicEnabled
    {
        get => _headset.MicEnabled ?? false;
        set
        {
            if (value != MicEnabled) _ = _headset.SetMicAsync(value);
        }
    }

    // --- Égaliseur ---

    public IReadOnlyList<string> PresetLabels { get; } = [.. Enum.GetValues<EqPreset>().Select(Labels.Of)];

    public int PresetIndex
    {
        get => State.EqPreset is { } p ? Array.IndexOf(Enum.GetValues<EqPreset>(), p) : -1;
        set
        {
            var presets = Enum.GetValues<EqPreset>();
            if (value >= 0 && value < presets.Length && value != PresetIndex) _ = _headset.SetEqualizerPresetAsync(presets[value]);
        }
    }

    public string EqualizerLabel => State.EqPreset is { } p ? Labels.Of(p) : "Égaliseur";

    public IReadOnlyList<int> EqualizerBands
    {
        get => State.EqBands ?? new int[Commands.EqBandCount];
        set
        {
            if (!value.SequenceEqual(EqualizerBands)) _ = _headset.SetEqualizerBandsAsync([.. value]);
        }
    }

    // --- Fonctions du casque ---

    public bool HasDsee => IsConnected && State.Dsee.HasValue;
    public bool Dsee
    {
        get => State.Dsee ?? false;
        set
        {
            if (value != Dsee) _ = _headset.RunAsync(c => c.SetDseeAsync(value));
        }
    }

    public bool HasSpeakToChat => IsConnected && State.SpeakToChat.HasValue;
    public bool SpeakToChat
    {
        get => State.SpeakToChat ?? false;
        set
        {
            if (value != SpeakToChat) _ = _headset.RunAsync(c => c.SetSpeakToChatAsync(value));
        }
    }

    /// <summary>Les réglages de Speak-to-Chat ne servent que si la fonction est active.</summary>
    public bool HasSpeakToChatConfig => HasSpeakToChat && SpeakToChat && State.SpeakToChatConfig.HasValue;

    public IReadOnlyList<string> SensitivityLabels { get; } =
        [.. Enum.GetValues<SpeakToChatSensitivity>().Select(Labels.Of)];

    public IReadOnlyList<string> TimeoutLabels { get; } =
        [.. Enum.GetValues<SpeakToChatTimeout>().Select(Labels.Of)];

    public int SensitivityIndex
    {
        get => State.SpeakToChatConfig is { } c ? Array.IndexOf(Enum.GetValues<SpeakToChatSensitivity>(), c.Sensitivity) : -1;
        set
        {
            var values = Enum.GetValues<SpeakToChatSensitivity>();
            if (value >= 0 && value < values.Length && value != SensitivityIndex && State.SpeakToChatConfig is { } c)
                SetSpeakToChatConfig(c with { Sensitivity = values[value] });
        }
    }

    public int TimeoutIndex
    {
        get => State.SpeakToChatConfig is { } c ? Array.IndexOf(Enum.GetValues<SpeakToChatTimeout>(), c.Timeout) : -1;
        set
        {
            var values = Enum.GetValues<SpeakToChatTimeout>();
            if (value >= 0 && value < values.Length && value != TimeoutIndex && State.SpeakToChatConfig is { } c)
                SetSpeakToChatConfig(c with { Timeout = values[value] });
        }
    }

    public bool SpeakToChatVoiceFocus
    {
        get => State.SpeakToChatConfig?.VoiceFocus ?? false;
        set
        {
            if (State.SpeakToChatConfig is { } c && c.VoiceFocus != value) SetSpeakToChatConfig(c with { VoiceFocus = value });
        }
    }

    private void SetSpeakToChatConfig(SpeakToChatConfig config) =>
        _ = _headset.RunAsync(c => c.SetSpeakToChatConfigAsync(config));

    public bool HasPauseWhenTakenOff => IsConnected && State.PauseWhenTakenOff.HasValue;
    public bool PauseWhenTakenOff
    {
        get => State.PauseWhenTakenOff ?? false;
        set
        {
            if (value != PauseWhenTakenOff) _ = _headset.RunAsync(c => c.SetPauseWhenTakenOffAsync(value));
        }
    }

    public bool HasAutoPowerOff => IsConnected && State.AutoPowerOff.HasValue;
    public bool AutoPowerOff
    {
        get => State.AutoPowerOff ?? false;
        set
        {
            if (value != AutoPowerOff) _ = _headset.RunAsync(c => c.SetAutoPowerOffAsync(value));
        }
    }

    public bool HasTouchSensor => IsConnected && State.TouchSensor.HasValue;
    public bool TouchSensor
    {
        get => State.TouchSensor ?? false;
        set
        {
            if (value != TouchSensor) _ = _headset.RunAsync(c => c.SetTouchSensorAsync(value));
        }
    }

    public bool HasVoiceGuidance => IsConnected && State.VoiceGuidance.HasValue;
    public bool VoiceGuidance
    {
        get => State.VoiceGuidance ?? false;
        set
        {
            if (value != VoiceGuidance) _ = _headset.RunAsync(c => c.SetVoiceGuidanceAsync(value));
        }
    }

    public Task PowerOffAsync() => _headset.RunAsync(c => c.PowerOffAsync());

    // --- Application ---

    public bool StartWithWindows
    {
        get => AppSettings.StartWithWindows;
        set
        {
            if (value == StartWithWindows) return;
            AppSettings.StartWithWindows = value;
            OnPropertyChanged();
        }
    }

    public bool HotkeyEnabled
    {
        get => _settings.HotkeyEnabled;
        set
        {
            if (value == _settings.HotkeyEnabled) return;
            _settings.HotkeyEnabled = value;
            _settings.Save();
            HotkeySettingChanged?.Invoke();
            OnPropertyChanged();
        }
    }

    public string HotkeyError
    {
        get => _hotkeyError ?? "";
        set
        {
            _hotkeyError = value.Length == 0 ? null : value;
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
            if (value == _settings.LowBatteryAlert) return;
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

    /// <summary>Caractère d'une police d'icônes à partir de son code (évite les caractères invisibles dans le source).</summary>
    public static string Glyph(int codePoint) => char.ConvertFromUtf32(codePoint);

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
