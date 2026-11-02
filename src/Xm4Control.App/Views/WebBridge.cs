using System.Text.Json;
using System.Text.Json.Serialization;
using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App.Views;

/// <summary>
/// État envoyé à la page web de la fenêtre principale, à chaque changement. Un booléen nullable vaut null quand
/// la fonction n'est pas disponible (casque déconnecté ou réglage pas encore lu).
/// </summary>
internal sealed record WebState(
    bool Connected,
    string DeviceName,
    string StatusText,
    string DisconnectedHint,
    string Error,
    int? Battery,
    bool Charging,
    string Codec,
    string Firmware,
    string? Mode,
    int AmbientLevel,
    bool FocusOnVoice,
    bool Optimizing,
    string OptimizerStatus,
    bool? Mic,
    IReadOnlyList<string> Presets,
    int PresetIndex,
    IReadOnlyList<int> Bands,
    IReadOnlyList<string> BandLabels,
    bool? Dsee,
    bool? SpeakToChat,
    bool SpeakToChatConfig,
    IReadOnlyList<string> Sensitivities,
    int SensitivityIndex,
    IReadOnlyList<string> Timeouts,
    int TimeoutIndex,
    bool SpeakToChatVoiceFocus,
    bool? PauseWhenTakenOff,
    bool? AutoPowerOff,
    bool? TouchSensor,
    bool? VoiceGuidance,
    bool StartWithWindows,
    bool HotkeyEnabled,
    string HotkeyError,
    bool LowBatteryAlert,
    string Version,
    string? Page);

// Générateur de sources : l'appli est élaguée, pas de sérialisation par réflexion.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WebState))]
internal sealed partial class WebJson : JsonSerializerContext;

/// <summary>Traduit l'état du <see cref="MainViewModel"/> pour la page web, et ses messages en commandes.</summary>
internal static class WebBridge
{
    public static string Serialize(MainViewModel vm, string? page) => JsonSerializer.Serialize(new WebState(
        vm.IsConnected,
        vm.DeviceName,
        vm.StatusText,
        vm.DisconnectedHint,
        vm.Error,
        vm.HasBattery ? (int)vm.BatteryLevel : null,
        vm.IsCharging,
        vm.Codec,
        vm.Firmware,
        vm.Mode switch
        {
            AmbientMode.NoiseCancelling => "nc",
            AmbientMode.Ambient => "ambient",
            AmbientMode.WindReduction => "wind",
            AmbientMode.Off => "off",
            _ => null,
        },
        vm.AmbientLevel,
        vm.FocusOnVoice,
        vm.IsOptimizing,
        vm.HasOptimizerStatus ? vm.OptimizerStatus : "",
        vm.MicAvailable ? vm.MicEnabled : null,
        vm.PresetLabels,
        vm.PresetIndex,
        vm.EqualizerBands,
        Labels.EqBands,
        vm.HasDsee ? vm.Dsee : null,
        vm.HasSpeakToChat ? vm.SpeakToChat : null,
        vm.HasSpeakToChatConfig,
        vm.SensitivityLabels,
        vm.SensitivityIndex,
        vm.TimeoutLabels,
        vm.TimeoutIndex,
        vm.SpeakToChatVoiceFocus,
        vm.HasPauseWhenTakenOff ? vm.PauseWhenTakenOff : null,
        vm.HasAutoPowerOff ? vm.AutoPowerOff : null,
        vm.HasTouchSensor ? vm.TouchSensor : null,
        vm.HasVoiceGuidance ? vm.VoiceGuidance : null,
        vm.StartWithWindows,
        vm.HotkeyEnabled,
        vm.HotkeyError,
        vm.LowBatteryAlert,
        vm.Version,
        page), WebJson.Default.WebState);

    /// <summary>
    /// Applique un message de la page, de la forme <c>{"cmd":"dsee","value":true}</c>. Renvoie le nom d'une action
    /// que la fenêtre doit traiter elle-même (confirmation, quitter), sinon null.
    /// </summary>
    public static string? Apply(MainViewModel vm, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cmd = root.GetProperty("cmd").GetString();
        var value = root.TryGetProperty("value", out var v) ? v : default;
        switch (cmd)
        {
            case "mode":
                vm.SetMode(value.GetString() switch
                {
                    "ambient" => AmbientMode.Ambient,
                    "wind" => AmbientMode.WindReduction,
                    "off" => AmbientMode.Off,
                    _ => AmbientMode.NoiseCancelling,
                });
                break;
            case "ambientLevel": vm.AmbientLevelValue = value.GetInt32(); break;
            case "focusOnVoice": vm.FocusOnVoice = value.GetBoolean(); break;
            case "optimizer": _ = vm.ToggleOptimizerAsync(); break;
            case "mic": vm.MicEnabled = value.GetBoolean(); break;
            case "preset": vm.PresetIndex = value.GetInt32(); break;
            case "bands": vm.EqualizerBands = [.. value.EnumerateArray().Select(b => b.GetInt32())]; break;
            case "dsee": vm.Dsee = value.GetBoolean(); break;
            case "speakToChat": vm.SpeakToChat = value.GetBoolean(); break;
            case "sensitivity": vm.SensitivityIndex = value.GetInt32(); break;
            case "timeout": vm.TimeoutIndex = value.GetInt32(); break;
            case "speakToChatVoiceFocus": vm.SpeakToChatVoiceFocus = value.GetBoolean(); break;
            case "pauseWhenTakenOff": vm.PauseWhenTakenOff = value.GetBoolean(); break;
            case "autoPowerOff": vm.AutoPowerOff = value.GetBoolean(); break;
            case "touchSensor": vm.TouchSensor = value.GetBoolean(); break;
            case "voiceGuidance": vm.VoiceGuidance = value.GetBoolean(); break;
            case "startWithWindows": vm.StartWithWindows = value.GetBoolean(); break;
            case "hotkey": vm.HotkeyEnabled = value.GetBoolean(); break;
            case "lowBatteryAlert": vm.LowBatteryAlert = value.GetBoolean(); break;
            default: return cmd;
        }
        return null;
    }
}
