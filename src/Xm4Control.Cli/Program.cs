using Xm4Control.Core;
using Xm4Control.Core.Bluetooth;
using Xm4Control.Core.Protocol;

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

    var device = PairedDevices.FindXm4() ?? throw new InvalidOperationException("Aucun WH-1000XM4 appairé.");

    if (args[0] == "mic") return Mic(device, args.ElementAtOrDefault(1));

    if (!device.Connected) throw new InvalidOperationException($"{device.Name} est appairé mais pas connecté.");

    using var client = new Xm4Client(device);
    if (verbose) client.Trace += line => Console.WriteLine($"  {line}");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    await client.ConnectAsync(cts.Token);

    var on = args.ElementAtOrDefault(1) switch
    {
        "on" or "1" or "oui" => true,
        "off" or "0" or "non" => false,
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
            await client.SetAmbientAsync(new AmbientSettings(AmbientMode.Ambient, level, args.Contains("voix")));
            break;
        case "eq":
            if (!Enum.TryParse<EqPreset>(args.ElementAtOrDefault(1), true, out var preset))
                throw new ArgumentException($"Préréglage inconnu. Valeurs : {string.Join(", ", Enum.GetNames<EqPreset>())}");
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
            Console.WriteLine("Casque éteint.");
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
    Console.Error.WriteLine($"Erreur : {ex.Message}");
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
            Console.Error.WriteLine($"{ex.Message} Relance la commande dans un terminal administrateur.");
            return 2;
        }
    }
    Console.WriteLine($"Micro (profil mains libres) : {(HandsFreeProfile.IsEnabled(device.Address) ? "activé" : "désactivé")}");
    return 0;
}

static int CurrentLevel(Xm4State state) =>
    state.Ambient is { Level: > 0 } a ? a.Level : 10;

static bool Require(bool? on) => on ?? throw new ArgumentException("Préciser on ou off.");

static void PrintState(PairedDevice device, Xm4State s)
{
    static string YesNo(bool? b) => b switch { true => "oui", false => "non", null => "?" };

    Console.WriteLine($"{device.Name} ({device.AddressText})");
    Console.WriteLine($"  Firmware       : {s.Firmware ?? "?"}");
    Console.WriteLine($"  Batterie       : {(s.Battery is { } b ? $"{b} %" : "?")}{(s.Charging ? " (en charge)" : "")}");
    Console.WriteLine($"  Codec          : {s.Codec ?? "?"}");
    if (s.Ambient is { } a)
    {
        var detail = a.Mode == AmbientMode.Ambient ? $" (niveau {a.Level}{(a.FocusOnVoice ? ", voix" : "")})" : "";
        Console.WriteLine($"  Son ambiant    : {Labels.Of(a.Mode)}{detail}");
    }
    if (s.EqPreset is { } eq)
        Console.WriteLine($"  Égaliseur      : {Labels.Of(eq)}{(s.EqBands is { } bands ? $" [{string.Join(" ", bands)}]" : "")}");
    Console.WriteLine($"  DSEE Extreme   : {YesNo(s.Dsee)}");
    Console.WriteLine($"  Speak-to-Chat  : {YesNo(s.SpeakToChat)}");
    if (s.SpeakToChatConfig is { } c)
        Console.WriteLine($"    sensibilité {Labels.Of(c.Sensitivity).ToLowerInvariant()}, fin {Labels.Of(c.Timeout).ToLowerInvariant()}{(c.VoiceFocus ? ", focus voix" : "")}");
    Console.WriteLine($"  Pause au retrait : {YesNo(s.PauseWhenTakenOff)}");
    Console.WriteLine($"  Extinction auto : {YesNo(s.AutoPowerOff)}");
    Console.WriteLine($"  Pavé tactile   : {YesNo(s.TouchSensor)}");
    Console.WriteLine($"  Guide vocal    : {YesNo(s.VoiceGuidance)}");
}

static void PrintUsage() => Console.WriteLine("""
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
    """);
