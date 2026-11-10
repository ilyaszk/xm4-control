using Xm4Control.Core.Protocol;
using static Xm4Control.Core.Lang;

namespace Xm4Control.Core;

public static class Labels
{
    /// <summary>Ordre d'affichage des modes, comme dans l'appli Sony.</summary>
    public static readonly AmbientMode[] ModeOrder =
        [AmbientMode.NoiseCancelling, AmbientMode.Ambient, AmbientMode.WindReduction, AmbientMode.Off];

    public static string Of(AmbientMode mode) => mode switch
    {
        AmbientMode.NoiseCancelling => T("Réduction de bruit", "Noise cancelling"),
        AmbientMode.WindReduction => T("Réduction du vent", "Wind noise reduction"),
        AmbientMode.Ambient => T("Son ambiant", "Ambient sound"),
        _ => T("Désactivé", "Off"),
    };

    public static string Of(EqPreset preset) => preset switch
    {
        EqPreset.Off => T("Désactivé", "Off"),
        EqPreset.Bright => "Bright",
        EqPreset.Excited => "Excited",
        EqPreset.Mellow => "Mellow",
        EqPreset.Relaxed => "Relaxed",
        EqPreset.Vocal => "Vocal",
        EqPreset.TrebleBoost => "Treble Boost",
        EqPreset.BassBoost => "Bass Boost",
        EqPreset.Speech => "Speech",
        EqPreset.Manual => T("Manuel", "Manual"),
        EqPreset.Custom1 => T("Personnalisé 1", "Custom 1"),
        EqPreset.Custom2 => T("Personnalisé 2", "Custom 2"),
        _ => $"0x{(byte)preset:X2}",
    };

    public static IReadOnlyList<string> EqBands =>
        ["Clear Bass", "400 Hz", "1 kHz", T("2,5 kHz", "2.5 kHz"), T("6,3 kHz", "6.3 kHz"), "16 kHz"];

    public static string Of(SpeakToChatSensitivity sensitivity) => sensitivity switch
    {
        SpeakToChatSensitivity.High => T("Élevée", "High"),
        SpeakToChatSensitivity.Low => T("Faible", "Low"),
        _ => T("Automatique", "Auto"),
    };

    public static string Of(SpeakToChatTimeout timeout) => timeout switch
    {
        SpeakToChatTimeout.Short => T("Court (environ 15 s)", "Short (about 15 s)"),
        SpeakToChatTimeout.Long => T("Long (environ 1 min)", "Long (about 1 min)"),
        SpeakToChatTimeout.Off => T("Jamais automatiquement", "Never automatically"),
        _ => T("Standard (environ 30 s)", "Standard (about 30 s)"),
    };

    public static string Of(NcOptimizerStatus status) => status switch
    {
        NcOptimizerStatus.CheckingFit => T("Vérification du port du casque…", "Checking how the headphones fit…"),
        NcOptimizerStatus.MeasuringPressure => T("Mesure de la pression atmosphérique…", "Measuring atmospheric pressure…"),
        NcOptimizerStatus.Analyzing => T("Analyse…", "Analyzing…"),
        NcOptimizerStatus.Finished => T("Optimisation terminée.", "Optimization complete."),
        _ => "",
    };
}
