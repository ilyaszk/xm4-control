using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Xm4Control.App.Views;

/// <summary>Petites aides communes aux fenêtres et aux liaisons x:Bind.</summary>
public static class Ui
{
    public static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

    /// <summary>Opacité d'un bloc indisponible : les TextBlock, eux, ne grisent pas d'eux-mêmes.</summary>
    public static double Dim(bool enabled) => enabled ? 1.0 : 0.5;

    /// <summary>Conversion explicite : la conversion implicite bool → Visibility de x:Bind ne s'applique pas ici.</summary>
    public static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    public static IntPtr Handle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

    /// <summary>Facteur d'échelle de l'écran de la fenêtre (1,25 à 125 %).</summary>
    public static double Scale(Window window) => GetDpiForWindow(Handle(window)) / 96.0;

    public static void BringToFront(Window window) => SetForegroundWindow(Handle(window));

    /// <summary>Boîte de confirmation ; renvoie vrai si l'action principale est choisie.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string action)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = message,
            PrimaryButtonText = action,
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Menus natifs (clic droit sur l'icône) au thème clair ou sombre du système. API non documentée de uxtheme
    /// (ordinaux 135 et 136, Windows 10 1903 et plus), utilisée par la plupart des applis Win32 pour cet usage.
    /// </summary>
    public static void FollowSystemThemeInMenus()
    {
        try
        {
            SetPreferredAppMode(AllowDark);
            FlushMenuThemes();
        }
        catch (EntryPointNotFoundException)
        {
            // Windows trop ancien : menus clairs.
        }
    }

    private const int AllowDark = 1;

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
