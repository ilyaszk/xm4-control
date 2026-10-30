namespace Xm4Control.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Xm4Control.Show", out var firstInstance);
        if (!firstInstance)
        {
            showRequested.Set();
            return;
        }

        // Garde l'entrée de démarrage alignée sur l'exe lancé (s'il a été republié ou déplacé).
        if (AppSettings.StartWithWindows) AppSettings.StartWithWindows = true;

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        // --log : trace des trames échangées avec le casque, pour le débogage.
        using var log = args.Contains("--log") ? OpenLog() : null;

        // --tray : lancement au démarrage de Windows, sans ouvrir la fenêtre.
        Application.Run(new TrayApplication(showRequested, startHidden: args.Contains("--tray"), log));
    }

    private static TextWriter OpenLog()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xm4Control", "trace.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return TextWriter.Synchronized(new StreamWriter(path, append: false) { AutoFlush = true });
    }
}
