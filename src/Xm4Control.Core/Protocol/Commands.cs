namespace Xm4Control.Core.Protocol;

public enum AmbientMode
{
    Off,
    NoiseCancelling,
    WindReduction,
    Ambient,
}

public enum EqPreset : byte
{
    Off = 0x00,
    Bright = 0x10,
    Excited = 0x11,
    Mellow = 0x12,
    Relaxed = 0x13,
    Vocal = 0x14,
    TrebleBoost = 0x15,
    BassBoost = 0x16,
    Speech = 0x17,
    Manual = 0xA0,
    Custom1 = 0xA1,
    Custom2 = 0xA2,
}

/// <param name="Level">Niveau du son ambiant, de 1 à 20.</param>
public readonly record struct AmbientSettings(AmbientMode Mode, int Level, bool FocusOnVoice);

public sealed record Request(MessageType Type, byte[] Payload)
{
    public override string ToString() => $"{Type} [{Convert.ToHexString(Payload)}]";
}

/// <summary>
/// Commandes du protocole v1 utilisé par le WH-1000XM4.
/// Le premier octet du payload est le code de commande ; chaque fonctionnalité a
/// un quadruplet GET (n), RET (n+1), SET (n+2), NOTIFY (n+3).
/// Source : rétro-ingénierie de Gadgetbridge et de SonyHeadphonesClient.
/// </summary>
public static class Commands
{
    public const int MinAmbientLevel = 1;
    public const int MaxAmbientLevel = 20;
    public const int EqBandCount = 6; // Clear Bass + 400 Hz, 1 kHz, 2,5 kHz, 6,3 kHz, 16 kHz
    public const int EqBandMin = -10;
    public const int EqBandMax = 10;

    public static Request Init() => C1(0x00, 0x00);
    public static Request GetFirmware() => C1(0x04, 0x02);
    public static Request GetBattery() => C1(0x10, 0x00);
    public static Request GetCodec() => C1(0x18, 0x00);
    public static Request PowerOff() => C1(0x22, 0x00, 0x01);

    public static Request GetAmbient() => C1(0x66, 0x02);

    public static Request SetAmbient(AmbientSettings s)
    {
        var enabled = s.Mode != AmbientMode.Off;
        byte nc = s.Mode switch
        {
            AmbientMode.NoiseCancelling => 0x02,
            AmbientMode.WindReduction => 0x01,
            _ => 0x00,
        };
        var level = s.Mode is AmbientMode.Off or AmbientMode.Ambient
            ? Math.Clamp(s.Level, MinAmbientLevel, MaxAmbientLevel)
            : 0;
        var focus = s.Mode == AmbientMode.Ambient && s.FocusOnVoice;

        // 0x11 = "réglage terminé" ; 0x10 ferait un réglage intermédiaire.
        return C1(0x68, 0x02, (byte)(enabled ? 0x11 : 0x00), 0x02, nc, 0x01, B(focus), (byte)level);
    }

    public static Request GetEqualizer() => C1(0x56, 0x01);
    public static Request SetEqualizerPreset(EqPreset preset) => C1(0x58, 0x01, (byte)preset, 0x00);

    /// <param name="bands">Clear Bass puis les 5 bandes, de -10 à +10.</param>
    public static Request SetEqualizerBands(IReadOnlyList<int> bands)
    {
        if (bands.Count != EqBandCount) throw new ArgumentException($"{EqBandCount} bandes attendues", nameof(bands));
        var payload = new byte[4 + EqBandCount];
        payload[0] = 0x58;
        payload[1] = 0x01;
        payload[2] = 0xFF; // conserve le préréglage courant
        payload[3] = EqBandCount;
        for (var i = 0; i < EqBandCount; i++)
            payload[4 + i] = (byte)(Math.Clamp(bands[i], EqBandMin, EqBandMax) + 10);
        return new Request(MessageType.Command1, payload);
    }

    public static Request GetDsee() => C1(0xE6, 0x02);
    public static Request SetDsee(bool on) => C1(0xE8, 0x02, 0x00, B(on));

    public static Request GetTouchSensor() => C1(0xD6, 0xD2);
    public static Request SetTouchSensor(bool on) => C1(0xD8, 0xD2, 0x01, B(on));

    public static Request GetPauseWhenTakenOff() => C1(0xF6, 0x03);
    public static Request SetPauseWhenTakenOff(bool on) => C1(0xF8, 0x03, 0x00, B(on));

    public static Request GetSpeakToChat() => C1(0xF6, 0x05);
    public static Request SetSpeakToChat(bool on) => C1(0xF8, 0x05, 0x01, B(on));

    public static Request GetVoiceGuidance() => C2(0x46, 0x01, 0x01);
    public static Request SetVoiceGuidance(bool on) => C2(0x48, 0x01, 0x01, B(on));

    /// <summary>Requêtes envoyées après l'init pour connaître l'état complet du casque.</summary>
    public static IEnumerable<Request> InitialQueries() =>
    [
        GetFirmware(),
        GetBattery(),
        GetCodec(),
        GetAmbient(),
        GetEqualizer(),
        GetDsee(),
        GetSpeakToChat(),
        GetPauseWhenTakenOff(),
        GetTouchSensor(),
        GetVoiceGuidance(),
    ];

    private static byte B(bool value) => (byte)(value ? 0x01 : 0x00);
    private static Request C1(params byte[] payload) => new(MessageType.Command1, payload);
    private static Request C2(params byte[] payload) => new(MessageType.Command2, payload);
}
