using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Xm4Control.App;

// Sérialisation générée à la compilation : l'exe publié est élagué (trimming), sans réflexion.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJson : JsonSerializerContext;

internal sealed class AppSettings
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "Xm4Control";

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Xm4Control", "settings.json");

    /// <summary>Dernier niveau de son ambiant choisi (le casque renvoie 0 en réduction de bruit).</summary>
    public int AmbientLevel { get; set; } = 10;
    public bool FocusOnVoice { get; set; }

    /// <summary>Choix de l'utilisateur, réappliqué à chaque connexion si Windows a réactivé le profil.</summary>
    public bool MicDisabled { get; set; }

    /// <summary>Raccourci global Ctrl+Alt+N pour passer d'un mode à l'autre.</summary>
    public bool HotkeyEnabled { get; set; } = true;

    /// <summary>Notification quand la batterie du casque passe sous 20 %.</summary>
    public bool LowBatteryAlert { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJson.Default.AppSettings) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SettingsJson.Default.AppSettings));
    }

    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" --tray");
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }
}
