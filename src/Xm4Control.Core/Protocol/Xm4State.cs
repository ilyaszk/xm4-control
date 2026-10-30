namespace Xm4Control.Core.Protocol;

/// <summary>État connu du casque. Immuable : chaque réponse produit un nouvel état.</summary>
public sealed record Xm4State
{
    /// <summary>1 ou 2 une fois la réponse d'init reçue, 0 avant.</summary>
    public int ProtocolVersion { get; init; }
    public string? Firmware { get; init; }
    public int? Battery { get; init; }
    public bool Charging { get; init; }
    public string? Codec { get; init; }
    public AmbientSettings? Ambient { get; init; }
    public EqPreset? EqPreset { get; init; }
    /// <summary>Clear Bass puis les 5 bandes, de -10 à +10.</summary>
    public int[]? EqBands { get; init; }
    public bool? Dsee { get; init; }
    public bool? SpeakToChat { get; init; }
    public bool? PauseWhenTakenOff { get; init; }
    public bool? TouchSensor { get; init; }
    public bool? VoiceGuidance { get; init; }
}

/// <summary>Décode les payloads RET/NOTIFY envoyés par le casque.</summary>
public static class ResponseParser
{
    /// <returns>Le nouvel état, ou <paramref name="state"/> si le payload est inconnu ou invalide.</returns>
    public static Xm4State Apply(Xm4State state, MessageType type, byte[] p)
    {
        if (p.Length == 0) return state;

        if (type == MessageType.Command2)
        {
            return p[0] is 0x47 or 0x49 && p.Length >= 4 && p[1] == 0x01 && TryBool(p[3], out var voice)
                ? state with { VoiceGuidance = voice }
                : state;
        }

        if (type != MessageType.Command1) return state;

        switch (p[0])
        {
            case 0x01: // INIT_REPLY : 4 octets en v1, 8 en v2
                return state with { ProtocolVersion = p.Length == 8 ? 2 : 1 };

            case 0x05 when p.Length >= 3 && p[1] == 0x02 && p.Length >= 3 + p[2]:
                return state with { Firmware = System.Text.Encoding.ASCII.GetString(p, 3, p[2]) };

            case 0x11 or 0x13 when p.Length >= 4 && p[1] == 0x00:
                return state with { Battery = p[2], Charging = p[3] == 0x01 };

            case 0x19 or 0x1B when p.Length >= 3 && p[1] == 0x00:
                return state with { Codec = CodecName(p[2]) };

            case 0x57 or 0x59 when p.Length >= 3:
                var bands = p.Length >= 10
                    ? p.AsSpan(4, Commands.EqBandCount).ToArray().Select(b => b - 10).ToArray()
                    : state.EqBands;
                return state with { EqPreset = (EqPreset)p[2], EqBands = bands };

            case 0x67 or 0x69 when p.Length >= 8 && p[1] == 0x02:
                var ambient = ParseAmbient(p);
                return ambient is null ? state : state with { Ambient = ambient };

            case 0xD7 or 0xD9 when p.Length >= 4 && p[1] == 0xD2 && TryBool(p[3], out var touch):
                return state with { TouchSensor = touch };

            case 0xE7 or 0xE9 when p.Length >= 4 && p[1] == 0x02 && TryBool(p[3], out var dsee):
                return state with { Dsee = dsee };

            case 0xF7 or 0xF9 when p.Length >= 4 && TryBool(p[3], out var flag):
                return p[1] switch
                {
                    0x03 => state with { PauseWhenTakenOff = flag },
                    // RET : F7 05 00 xx, notification après SET : F9 05 01 xx.
                    // F9 05 02 .. est un autre message (pas l'activation de la fonction).
                    0x05 when p[2] is 0x00 or 0x01 => state with { SpeakToChat = flag },
                    _ => state,
                };

            default:
                return state;
        }
    }

    private static AmbientSettings? ParseAmbient(byte[] p)
    {
        var focus = p[6] == 0x01;
        int level = p[7];
        if (p[2] == 0x00) return new AmbientSettings(AmbientMode.Off, level, focus);
        if (p[2] != 0x01) return null;

        AmbientMode? mode = (p[3], p[4]) switch
        {
            (0x02, 0x02) or (0x00, 0x01) => AmbientMode.NoiseCancelling,
            (0x02, 0x01) => AmbientMode.WindReduction,
            (0x02, 0x00) or (0x00, 0x00) => AmbientMode.Ambient,
            _ => null,
        };
        return mode is null ? null : new AmbientSettings(mode.Value, level, focus);
    }

    private static bool TryBool(byte b, out bool value)
    {
        value = b == 0x01;
        return b is 0x00 or 0x01;
    }

    private static string CodecName(byte code) => code switch
    {
        0x01 => "SBC",
        0x02 => "AAC",
        0x10 => "LDAC",
        0x20 => "aptX",
        0x21 => "aptX HD",
        _ => $"0x{code:X2}",
    };
}
