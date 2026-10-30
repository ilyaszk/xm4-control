using System.Net.Sockets;
using Xm4Control.Core.Bluetooth;
using Xm4Control.Core.Protocol;

namespace Xm4Control.Core;

/// <summary>
/// Connexion RFCOMM au service Sony du casque.
/// Chaque commande attend l'ACK du casque avant la suivante ; le numéro de séquence
/// de la commande suivante est celui de l'ACK reçu. Les messages envoyés par le casque
/// (réponses et notifications) sont acquittés avec la séquence 1 - n.
/// </summary>
public sealed class Xm4Client(PairedDevice device) : IDisposable
{
    public static readonly Guid SonyService = new("96CC203E-5068-46AD-B32D-E316F5E069BA");

    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(4);

    private readonly Socket _socket = new(BluetoothEndPoint.Family, SocketType.Stream, BluetoothEndPoint.Rfcomm);
    private readonly FrameReader _reader = new();
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly Lock _writeLock = new();
    private readonly TaskCompletionSource _initReply = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource? _pendingAck;
    private byte _sequence;
    private int _disposed;

    /// <summary>Déclenché sur le thread de lecture : l'UI doit repasser sur son propre thread.</summary>
    public event Action<Xm4State>? StateChanged;

    /// <summary>Connexion perdue sans appel à <see cref="Dispose"/>.</summary>
    public event Action? Disconnected;

    /// <summary>Trace des trames échangées.</summary>
    public event Action<string>? Trace;

    public PairedDevice Device { get; } = device;
    public Xm4State State { get; private set; } = new();

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await using (ct.Register(Dispose))
        {
            try
            {
                await Task.Run(() => _socket.Connect(new BluetoothEndPoint(Device.Address, SonyService)));
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
        }

        new Thread(ReadLoop) { IsBackground = true, Name = "XM4 RFCOMM" }.Start();

        // Juste après la connexion, le casque ignore souvent la première trame : on réessaie vite.
        await SendAsync(Commands.Init(), TimeSpan.FromMilliseconds(300), 10, ct);
        await _initReply.Task.WaitAsync(InitTimeout, ct);
        if (State.ProtocolVersion != 1)
            throw new NotSupportedException("Ce casque utilise le protocole Sony v2, non pris en charge.");

        foreach (var query in Commands.InitialQueries())
            await SendAsync(query, ct);
    }

    public Task SendAsync(Request request, CancellationToken ct = default) => SendAsync(request, AckTimeout, 2, ct);

    private async Task SendAsync(Request request, TimeSpan ackTimeout, int attempts, CancellationToken ct)
    {
        await _requestLock.WaitAsync(ct);
        try
        {
            // Les réémissions gardent la même séquence : le casque les traite comme des répétitions.
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var ack = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _pendingAck, ack);
                Write(new Frame(request.Type, Volatile.Read(ref _sequence), request.Payload));
                try
                {
                    await ack.Task.WaitAsync(ackTimeout, ct);
                    return;
                }
                catch (TimeoutException)
                {
                    Trace?.Invoke($"Pas d'ACK pour {request}");
                }
            }
            throw new TimeoutException("Le casque n'a pas accusé réception de la commande.");
        }
        finally
        {
            Volatile.Write(ref _pendingAck, null);
            _requestLock.Release();
        }
    }

    public Task SetAmbientAsync(AmbientSettings settings) => SetAndRefresh(Commands.SetAmbient(settings), Commands.GetAmbient());
    public Task SetEqualizerPresetAsync(EqPreset preset) => SetAndRefresh(Commands.SetEqualizerPreset(preset), Commands.GetEqualizer());
    public Task SetEqualizerBandsAsync(IReadOnlyList<int> bands) => SetAndRefresh(Commands.SetEqualizerBands(bands), Commands.GetEqualizer());
    public Task SetDseeAsync(bool on) => SetAndRefresh(Commands.SetDsee(on), Commands.GetDsee());
    public Task SetSpeakToChatAsync(bool on) => SetAndRefresh(Commands.SetSpeakToChat(on), Commands.GetSpeakToChat());
    public Task SetPauseWhenTakenOffAsync(bool on) => SetAndRefresh(Commands.SetPauseWhenTakenOff(on), Commands.GetPauseWhenTakenOff());
    public Task SetTouchSensorAsync(bool on) => SetAndRefresh(Commands.SetTouchSensor(on), Commands.GetTouchSensor());
    public Task SetVoiceGuidanceAsync(bool on) => SetAndRefresh(Commands.SetVoiceGuidance(on), Commands.GetVoiceGuidance());
    public Task RefreshBatteryAsync() => SendAsync(Commands.GetBattery());
    public Task PowerOffAsync() => SendAsync(Commands.PowerOff());

    // Le casque ne notifie pas toujours après un SET : on relit la valeur pour rester synchronisé.
    private async Task SetAndRefresh(Request set, Request get)
    {
        await SendAsync(set);
        await SendAsync(get);
    }

    private void Write(Frame frame)
    {
        var bytes = frame.Encode();
        lock (_writeLock) _socket.Send(bytes);
        Trace?.Invoke($"-> {frame}");
    }

    private void ReadLoop()
    {
        var buffer = new byte[1024];
        try
        {
            int read;
            while ((read = _socket.Receive(buffer)) > 0)
            {
                foreach (var frame in _reader.Feed(buffer.AsSpan(0, read)))
                    Handle(frame);
            }
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }

        var lost = new IOException("Connexion au casque perdue.");
        _initReply.TrySetException(lost);
        Interlocked.Exchange(ref _pendingAck, null)?.TrySetException(lost);
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _socket.Dispose();
            Disconnected?.Invoke();
        }
    }

    private void Handle(Frame frame)
    {
        Trace?.Invoke($"<- {frame}");

        if (frame.Type == MessageType.Ack)
        {
            Volatile.Write(ref _sequence, frame.Sequence);
            Interlocked.Exchange(ref _pendingAck, null)?.TrySetResult();
            return;
        }

        if (frame.Payload.Length == 0) return;

        try
        {
            Write(new Frame(MessageType.Ack, (byte)(1 - frame.Sequence), []));
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            return;
        }

        var state = ResponseParser.Apply(State, frame.Type, frame.Payload);
        if (state != State)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        if (frame.Type == MessageType.Command1 && frame.Payload[0] == 0x01)
            _initReply.TrySetResult();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        _socket.Dispose();
    }
}
