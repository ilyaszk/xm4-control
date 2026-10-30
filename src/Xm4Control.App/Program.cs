using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Xm4Control.App;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Instance unique : une deuxième instance demande à la première d'afficher ses réglages, puis s'arrête.
        var showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Xm4Control.Show", out var firstInstance);
        if (!firstInstance)
        {
            showRequested.Set();
            showRequested.Dispose();
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App(showRequested, args);
        });
    }
}
