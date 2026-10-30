using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Xm4Control.App;

/// <summary>
/// Rend la mémoire inutilisée à Windows quand l'appli passe en arrière-plan : collecte complète
/// qui restitue les segments libres, puis vidage de la plage de travail. Les pages encore utiles
/// reviennent à la demande.
/// </summary>
internal sealed class MemoryTrimmer
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };

    public MemoryTrimmer() => _timer.Tick += (_, _) =>
    {
        _timer.Stop();
        Trim();
    };

    /// <summary>Planifie un nettoyage dans 10 s ; chaque appel repousse l'échéance.</summary>
    public void Schedule()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private static void Trim()
    {
        // Pas pendant qu'une fenêtre est affichée : on la ralentirait pour rien.
        if (Application.Current.Windows.Cast<Window>().Any(w => w.IsVisible)) return;

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
