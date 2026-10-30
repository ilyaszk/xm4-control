using Xm4Control.Core.Protocol;

namespace Xm4Control.Core;

public static class Labels
{
    /// <summary>Ordre d'affichage des modes, comme dans l'appli Sony.</summary>
    public static readonly AmbientMode[] ModeOrder =
        [AmbientMode.NoiseCancelling, AmbientMode.Ambient, AmbientMode.WindReduction, AmbientMode.Off];

    public static string Of(AmbientMode mode) => mode switch
    {
        AmbientMode.NoiseCancelling => "Réduction de bruit",
        AmbientMode.WindReduction => "Réduction du vent",
        AmbientMode.Ambient => "Son ambiant",
        _ => "Désactivé",
    };

    public static string Of(EqPreset preset) => preset switch
    {
        EqPreset.Off => "Désactivé",
        EqPreset.Bright => "Bright",
        EqPreset.Excited => "Excited",
        EqPreset.Mellow => "Mellow",
        EqPreset.Relaxed => "Relaxed",
        EqPreset.Vocal => "Vocal",
        EqPreset.TrebleBoost => "Treble Boost",
        EqPreset.BassBoost => "Bass Boost",
        EqPreset.Speech => "Speech",
        EqPreset.Manual => "Manuel",
        EqPreset.Custom1 => "Personnalisé 1",
        EqPreset.Custom2 => "Personnalisé 2",
        _ => $"0x{(byte)preset:X2}",
    };

    public static readonly string[] EqBands = ["Clear Bass", "400 Hz", "1 kHz", "2,5 kHz", "6,3 kHz", "16 kHz"];

    public static string Of(SpeakToChatSensitivity sensitivity) => sensitivity switch
    {
        SpeakToChatSensitivity.High => "Élevée",
        SpeakToChatSensitivity.Low => "Faible",
        _ => "Automatique",
    };

    public static string Of(SpeakToChatTimeout timeout) => timeout switch
    {
        SpeakToChatTimeout.Short => "Court (environ 15 s)",
        SpeakToChatTimeout.Long => "Long (environ 1 min)",
        SpeakToChatTimeout.Off => "Jamais automatiquement",
        _ => "Standard (environ 30 s)",
    };

    public static string Of(NcOptimizerStatus status) => status switch
    {
        NcOptimizerStatus.CheckingFit => "Vérification du port du casque…",
        NcOptimizerStatus.MeasuringPressure => "Mesure de la pression atmosphérique…",
        NcOptimizerStatus.Analyzing => "Analyse…",
        NcOptimizerStatus.Finished => "Optimisation terminée.",
        _ => "",
    };
}
