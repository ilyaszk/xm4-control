using System.IO;
using Xm4Control.Core;
using Xm4Control.Core.Bluetooth;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App;

public enum ConnectionStatus
{
    Searching,
    NoDevice,
    NotConnected,
    Connecting,
    Connected,
}

/// <summary>
/// Surveille le casque, s'y connecte dès que Windows le voit connecté et relaie les commandes.
/// Tout se passe sur le thread UI : <see cref="Changed"/> peut mettre à jour les contrôles directement.
/// </summary>
internal sealed class HeadsetController : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BatteryInterval = TimeSpan.FromMinutes(5);
    private static readonly Xm4State EmptyState = new();

    private readonly SynchronizationContext _ui;
    private readonly AppSettings _settings;
    private readonly TextWriter? _log;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(3));
    private Xm4Client? _client;
    private bool _polling;
    private DateTime _nextConnectAttempt;
    private DateTime _nextBatteryRefresh;

    public HeadsetController(AppSettings settings, TextWriter? log = null)
    {
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Doit être créé sur le thread UI.");
        _settings = settings;
        _log = log;
        _ = PollLoopAsync();
    }

    // Les continuations reviennent sur le contexte UI capturé : pas besoin de minuterie propre au framework.
    private async Task PollLoopAsync()
    {
        await PollAsync();
        while (await _timer.WaitForNextTickAsync()) await PollAsync();
    }

    public event Action? Changed;

    public ConnectionStatus Status { get; private set; }
    public PairedDevice? Device { get; private set; }
    public Xm4State State => _client?.State ?? EmptyState;
    public bool? MicEnabled { get; private set; }
    public string? Error { get; private set; }
    public int AmbientLevel => _settings.AmbientLevel;
    public bool FocusOnVoice => _settings.FocusOnVoice;

    public string StatusText => Status switch
    {
        ConnectionStatus.Searching => "Recherche du casque…",
        ConnectionStatus.NoDevice => "Aucun WH-1000XM4 appairé à ce PC",
        ConnectionStatus.NotConnected => "Casque non connecté",
        ConnectionStatus.Connecting => "Connexion…",
        _ => string.Join(" · ", new[]
        {
            "Connecté",
            State.Battery is { } b ? $"batterie {b} %{(State.Charging ? " (en charge)" : "")}" : null,
            State.Codec,
            State.Firmware is { } fw ? $"firmware {fw}" : null,
        }.Where(s => s is not null)),
    };

    /// <summary>Résumé court pour l'infobulle de l'icône.</summary>
    public string Summary => Status == ConnectionStatus.Connected
        ? string.Join(" · ", new[]
        {
            Device?.Name,
            State.Ambient is { } a ? Labels.Of(a.Mode) : null,
            State.Battery is { } b ? $"{b} %" : null,
        }.Where(s => s is not null))
        : $"WH-1000XM4 : {StatusText.ToLowerInvariant()}";

    public Task SetAmbientModeAsync(AmbientMode mode) =>
        RunAsync(c => c.SetAmbientAsync(new AmbientSettings(mode, _settings.AmbientLevel, _settings.FocusOnVoice)));

    public Task SetAmbientLevelAsync(int level, bool focusOnVoice)
    {
        _settings.AmbientLevel = level;
        _settings.FocusOnVoice = focusOnVoice;
        _settings.Save();
        return RunAsync(c => c.SetAmbientAsync(new AmbientSettings(AmbientMode.Ambient, level, focusOnVoice)));
    }

    public Task SetEqualizerPresetAsync(EqPreset preset) => RunAsync(c => c.SetEqualizerPresetAsync(preset));

    /// <summary>Comme l'appli Sony : toucher aux bandes bascule sur le préréglage manuel.</summary>
    public Task SetEqualizerBandsAsync(int[] bands) => RunAsync(async c =>
    {
        if (c.State.EqPreset is not (EqPreset.Manual or EqPreset.Custom1 or EqPreset.Custom2))
            await c.SetEqualizerPresetAsync(EqPreset.Manual);
        await c.SetEqualizerBandsAsync(bands);
    });

    public async Task RunAsync(Func<Xm4Client, Task> action)
    {
        if (_client is not { } client) return;
        try
        {
            await action(client);
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        Raise();
    }

    public async Task SetMicAsync(bool enabled)
    {
        if (Device is not { } device) return;
        _settings.MicDisabled = !enabled;
        _settings.Save();
        try
        {
            await Task.Run(() => HandsFreeProfile.SetEnabled(device.Address, enabled));
            Error = null;
        }
        catch (Exception ex)
        {
            Error = $"Micro : {ex.Message}";
        }
        await RefreshMicAsync();
    }

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            if (_client is not null)
            {
                if (DateTime.UtcNow >= _nextBatteryRefresh)
                {
                    _nextBatteryRefresh = DateTime.UtcNow + BatteryInterval;
                    await RunAsync(c => c.RefreshBatteryAsync());
                }
                return;
            }

            var device = await Task.Run(PairedDevices.FindXm4);
            var isNewDevice = device?.Address != Device?.Address;
            Device = device;
            if (device is null)
            {
                SetStatus(ConnectionStatus.NoDevice);
                return;
            }
            if (isNewDevice) await RefreshMicAsync();

            if (!device.Connected)
            {
                SetStatus(ConnectionStatus.NotConnected);
                return;
            }
            if (DateTime.UtcNow >= _nextConnectAttempt) await ConnectAsync(device);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Raise();
        }
        finally
        {
            _polling = false;
        }
    }

    private async Task ConnectAsync(PairedDevice device)
    {
        SetStatus(ConnectionStatus.Connecting);
        var client = new Xm4Client(device);
        if (_log is { } log) client.Trace += line => log.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
        client.StateChanged += _ => _ui.Post(_ => OnStateChanged(client), null);
        client.Disconnected += () => _ui.Post(_ => OnDisconnected(client), null);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.ConnectAsync(cts.Token);
        }
        catch (Exception ex)
        {
            client.Dispose();
            _nextConnectAttempt = DateTime.UtcNow + RetryDelay;
            Error = $"Connexion impossible : {ex.Message}";
            SetStatus(ConnectionStatus.NotConnected);
            return;
        }

        _client = client;
        _nextBatteryRefresh = DateTime.UtcNow + BatteryInterval;
        Error = null;
        RememberAmbientSettings();
        SetStatus(ConnectionStatus.Connected);

        await RefreshMicAsync();
        if (_settings.MicDisabled && MicEnabled == true) await SetMicAsync(false);
    }

    private void OnStateChanged(Xm4Client client)
    {
        if (client != _client) return;
        RememberAmbientSettings();
        Raise();
    }

    private void OnDisconnected(Xm4Client client)
    {
        if (client != _client) return;
        _client = null;
        client.Dispose();
        _nextConnectAttempt = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        SetStatus(ConnectionStatus.NotConnected);
    }

    // Le casque renvoie un niveau 0 en réduction de bruit : on garde le dernier niveau ambiant connu.
    private void RememberAmbientSettings()
    {
        if (State.Ambient is not { Mode: AmbientMode.Ambient, Level: > 0 } ambient) return;
        if (ambient.Level == _settings.AmbientLevel && ambient.FocusOnVoice == _settings.FocusOnVoice) return;
        _settings.AmbientLevel = ambient.Level;
        _settings.FocusOnVoice = ambient.FocusOnVoice;
        _settings.Save();
    }

    private async Task RefreshMicAsync()
    {
        if (Device is not { } device) return;
        try
        {
            MicEnabled = await Task.Run(() => HandsFreeProfile.IsEnabled(device.Address));
        }
        catch (Exception ex)
        {
            MicEnabled = null;
            Error = $"Micro : {ex.Message}";
        }
        Raise();
    }

    private void SetStatus(ConnectionStatus status)
    {
        Status = status;
        Raise();
    }

    private void Raise() => Changed?.Invoke();

    public void Dispose()
    {
        _timer.Dispose();
        _client?.Dispose();
        _client = null;
    }
}
