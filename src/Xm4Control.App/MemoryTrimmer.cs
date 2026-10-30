using System.Runtime;
using System.Runtime.InteropServices;

namespace Xm4Control.App;

/// <summary>
/// Rend la mémoire inutilisée à Windows quand l'appli passe en arrière-plan : collecte complète
/// qui restitue les segments libres, puis vidage de la plage de travail. Les pages encore utiles
/// reviennent à la demande.
/// </summary>
internal sealed class MemoryTrimmer(Func<bool> isAnyWindowVisible)
{
    // Deuxième passage : après la fermeture d'une fenêtre, WinUI libère ses ressources en différé et
    // la mémoire remonte d'une vingtaine de Mo dans les secondes qui suivent le premier nettoyage.
    private static readonly TimeSpan[] Delays = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(50)];
    private CancellationTokenSource? _pending;

    /// <summary>Planifie un nettoyage dans 10 s puis un autre une minute après ; chaque appel repousse l'échéance.</summary>
    public void Schedule()
    {
        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        _ = TrimLaterAsync(pending.Token);
    }

    public void Stop() => _pending?.Cancel();

    private async Task TrimLaterAsync(CancellationToken ct)
    {
        foreach (var delay in Delays)
        {
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Pas pendant qu'une fenêtre est affichée : on la ralentirait pour rien.
            if (isAnyWindowVisible()) return;
            Trim();
        }
    }

    private static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        EmptyWorkingSet(GetCurrentProcess());
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);
}
