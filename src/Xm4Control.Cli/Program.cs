using Xm4Control.Core;
using Xm4Control.Core.Bluetooth;
using Xm4Control.Core.Protocol;
using static Xm4Control.Core.Lang;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var verbose = args.Any(a => a is "-v" or "--verbose");
args = args.Where(a => a is not ("-v" or "--verbose")).Select(a => a.ToLowerInvariant()).ToArray();

if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

try
{
    if (args[0] == "list")
    {
        foreach (var d in PairedDevices.List()) Console.WriteLine(d);
        return 0;
    }

    var device = PairedDevices.FindXm4()
        ?? throw new InvalidOperationException(T("Aucun WH-1000XM4 appairé.", "No WH-1000XM4 paired."));

    if (args[0] == "mic") return Mic(device, args.ElementAtOrDefault(1));

    if (!device.Connected)
        throw new InvalidOperationException(T($"{device.Name} est appairé mais pas connecté.", $"{device.Name} is paired but not connected."));

    using var client = new Xm4Client(device);
    if (verbose) client.Trace += line => Console.WriteLine($"  {line}");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    await client.ConnectAsync(cts.Token);

    var on = args.ElementAtOrDefault(1) switch
    {
        "on" or "1" or "oui" or "yes" => true,
        "off" or "0" or "non" or "no" => false,
        _ => (bool?)null,
    };

    switch (args[0])
    {
        case "status":
            break;
        case "nc":
            await client.SetAmbientAsync(new AmbientSettings(AmbientMode.NoiseCancelling, 0, false));
            break;
        case "wind":
            await client.SetAmbientAsync(new AmbientSettings(AmbientMode.WindReduction, 0, false));
            break;
        case "off":
            await client.SetAmbientAsync(new AmbientSettings(AmbientMode.Off, CurrentLevel(client.State), false));
            break;
        case "ambient":
            var level = int.TryParse(args.ElementAtOrDefault(1), out var l) ? l : CurrentLevel(client.State);
            var focusOnVoice = args.Skip(1).Any(a => a is "voix" or "voice");
            await client.SetAmbientAsync(new AmbientSettings(AmbientMode.Ambient, level, focusOnVoice));
            break;
        case "eq":
            if (!Enum.TryParse<EqPreset>(args.ElementAtOrDefault(1), true, out var preset))
            {
                var names = string.Join(", ", Enum.GetNames<EqPreset>());
                throw new ArgumentException(T($"Préréglage inconnu. Valeurs : {names}", $"Unknown preset. Values: {names}"));
            }
            await client.SetEqualizerPresetAsync(preset);
            break;
        case "dsee":
            await client.SetDseeAsync(Require(on));
            break;
        case "s2c":
            await client.SetSpeakToChatAsync(Require(on));
            break;
        case "pause":
            await client.SetPauseWhenTakenOffAsync(Require(on));
            break;
        case "autooff":
            await client.SetAutoPowerOffAsync(Require(on));
            break;
        case "touch":
            await client.SetTouchSensorAsync(Require(on));
            break;
        case "voice":
            await client.SetVoiceGuidanceAsync(Require(on));
            break;
        case "raw" or "raw2":
            // Debug : envoie un payload brut en hexadécimal, ex. "xm4 raw 6602 -v".
            var type = args[0] == "raw" ? MessageType.Command1 : MessageType.Command2;
            await client.SendAsync(new Request(type, Convert.FromHexString(args.ElementAtOrDefault(1) ?? "")));
            break;
        case "poweroff":
            await client.PowerOffAsync();
            Console.WriteLine(T("Casque éteint.", "Headphones turned off."));
            return 0;
        default:
            PrintUsage();
            return 1;
    }

    // Laisse le temps aux dernières notifications d'arriver.
    await Task.Delay(args[0] is "raw" or "raw2" ? 1500 : 300);
    PrintState(device, client.State);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(T($"Erreur : {ex.Message}", $"Error: {ex.Message}"));
    if (verbose) Console.Error.WriteLine(ex);
    return 1;
}

static int Mic(PairedDevice device, string? action)
{
    if (action is "on" or "off")
    {
        try
        {
            HandsFreeProfile.SetEnabled(device.Address, action == "on");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(T(
                $"{ex.Message} Relance la commande dans un terminal administrateur.",
                $"{ex.Message} Run the command again in an administrator terminal."));
            return 2;
        }
    }
    var enabled = HandsFreeProfile.IsEnabled(device.Address);
    Console.WriteLine(T(
        $"Micro (profil mains libres) : {(enabled ? "activé" : "désactivé")}",
        $"Mic (hands-free profile): {(enabled ? "on" : "off")}"));
    return 0;
}

static int CurrentLevel(Xm4State state) =>
    state.Ambient is { Level: > 0 } a ? a.Level : 10;

static bool Require(bool? on) => on ?? throw new ArgumentException(T("Préciser on ou off.", "Specify on or off."));

static void PrintState(PairedDevice device, Xm4State s)
{
    static string YesNo(bool? b) => b switch { true => T("oui", "yes"), false => T("non", "no"), null => "?" };

    // Valeurs alignées en colonne, quelle que soit la longueur des libellés dans chaque langue.
    static void Row(string label, string value) => Console.WriteLine($"  {(label + T(" :", ":")).PadRight(22)}{value}");

    Console.WriteLine($"{device.Name} ({device.AddressText})");
    Row("Firmware", s.Firmware ?? "?");
    Row(T("Batterie", "Battery"), (s.Battery is { } b ? Percent(b) : "?") + (s.Charging ? T(" (en charge)", " (charging)") : ""));
    Row("Codec", s.Codec ?? "?");
    if (s.Ambient is { } a)
    {
        var voice = a.FocusOnVoice ? T(", voix", ", voice") : "";
        var detail = a.Mode == AmbientMode.Ambient ? T($" (niveau {a.Level}{voice})", $" (level {a.Level}{voice})") : "";
        Row(T("Son ambiant", "Ambient sound"), Labels.Of(a.Mode) + detail);
    }
    if (s.EqPreset is { } eq)
        Row(T("Égaliseur", "Equalizer"), Labels.Of(eq) + (s.EqBands is { } bands ? $" [{string.Join(" ", bands)}]" : ""));
    Row("DSEE Extreme", YesNo(s.Dsee));
    Row("Speak-to-Chat", YesNo(s.SpeakToChat));
    if (s.SpeakToChatConfig is { } c)
    {
        var sensitivity = Labels.Of(c.Sensitivity).ToLowerInvariant();
        var timeout = Labels.Of(c.Timeout).ToLowerInvariant();
        Console.WriteLine(T(
            $"    sensibilité {sensitivity}, fin {timeout}{(c.VoiceFocus ? ", focus voix" : "")}",
            $"    sensitivity {sensitivity}, timeout {timeout}{(c.VoiceFocus ? ", voice focus" : "")}"));
    }
    Row(T("Pause au retrait", "Pause when taken off"), YesNo(s.PauseWhenTakenOff));
    Row(T("Extinction auto", "Auto power off"), YesNo(s.AutoPowerOff));
    Row(T("Pavé tactile", "Touch panel"), YesNo(s.TouchSensor));
    Row(T("Guide vocal", "Voice guidance"), YesNo(s.VoiceGuidance));
}

static void PrintUsage() => Console.WriteLine(T("""
    xm4 - contrôle du Sony WH-1000XM4

      xm4 status                 état du casque
      xm4 nc                     réduction de bruit
      xm4 ambient [1-20] [voix]  son ambiant (niveau, focus sur la voix)
      xm4 wind                   réduction du bruit du vent
      xm4 off                    ni réduction de bruit ni son ambiant
      xm4 eq <préréglage>        Off, Bright, Excited, Mellow, Relaxed, Vocal,
                                 TrebleBoost, BassBoost, Speech, Manual, Custom1, Custom2
      xm4 dsee|s2c|pause|autooff|touch|voice on|off
                                 DSEE Extreme, Speak-to-Chat, pause au retrait,
                                 extinction quand on ne le porte plus,
                                 pavé tactile, guide vocal
      xm4 mic [on|off]           profil mains libres Windows (off = stéréo sans micro)
      xm4 poweroff               éteint le casque
      xm4 list                   appareils Bluetooth appairés
      xm4 raw|raw2 <hex>         debug : envoie un payload brut (Command1 / Command2)

      -v                         affiche les trames échangées
    """, """
    xm4 - Sony WH-1000XM4 control

      xm4 status                 headphones status
      xm4 nc                     noise cancelling
      xm4 ambient [1-20] [voice] ambient sound (level, focus on voice)
      xm4 wind                   wind noise reduction
      xm4 off                    neither noise cancelling nor ambient sound
      xm4 eq <preset>            Off, Bright, Excited, Mellow, Relaxed, Vocal,
                                 TrebleBoost, BassBoost, Speech, Manual, Custom1, Custom2
      xm4 dsee|s2c|pause|autooff|touch|voice on|off
                                 DSEE Extreme, Speak-to-Chat, pause when taken off,
                                 power off when not worn,
                                 touch panel, voice guidance
      xm4 mic [on|off]           Windows hands-free profile (off = stereo without mic)
      xm4 poweroff               turns the headphones off
      xm4 list                   paired Bluetooth devices
      xm4 raw|raw2 <hex>         debug: sends a raw payload (Command1 / Command2)

      -v                         prints the exchanged frames
    """));
