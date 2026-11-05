using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Xm4Control.App;

/// <summary>
/// Icône de la zone de notification : une pile façon macOS avec le niveau de batterie du casque,
/// rouge quand la batterie est faible, vide et atténuée quand le casque n'est pas connecté.
/// </summary>
internal static class TrayIcons
{
    private const int SM_CXSMICON = 49;
    private const int FrameAlpha = 150;
    private const int DisconnectedFrameAlpha = 110;
    private static readonly Color LowOnLight = Color.FromArgb(255, 59, 48);
    private static readonly Color LowOnDark = Color.FromArgb(255, 69, 58);

    /// <summary>Ce dont dépend l'image : l'icône n'est redessinée que si l'un de ces éléments change.</summary>
    public readonly record struct Look(int? Level, bool LightTaskbar, int Size);

    /// <param name="level">Niveau de batterie, ou null quand le casque n'est pas connecté.</param>
    public static Look LookFor(int? level) => new(level, TaskbarIsLight(), IconSize());

    public static Icon Create(Look look)
    {
        // Dessin prévu pour 16 px (corps 15 x 9, téton 1 x 3, jauge à 1 px du contour), arrondi au pixel à chaque taille.
        var size = look.Size;
        var unit = size / 16f;
        var stroke = Math.Max(1, (int)unit);
        var bodyWidth = size - stroke;
        var bodyHeight = Px(9 * unit);
        var top = (size - bodyHeight) / 2;
        var nubHeight = Px(3 * unit);
        if ((bodyHeight - nubHeight) % 2 != 0) nubHeight++;
        var inset = 2 * stroke;

        var ink = look.LightTaskbar ? Color.Black : Color.White;
        var frame = Color.FromArgb(look.Level is null ? DisconnectedFrameAlpha : FrameAlpha, ink);

        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using var pen = new Pen(frame, stroke);
            using var body = RoundedRect(stroke / 2f, top + stroke / 2f, bodyWidth - stroke, bodyHeight - stroke, 2 * unit);
            g.DrawPath(pen, body);

            using var frameBrush = new SolidBrush(frame);
            using var nub = new GraphicsPath();
            nub.AddArc(bodyWidth - stroke, top + (bodyHeight - nubHeight) / 2, 2 * stroke, nubHeight, 270, 180);
            nub.CloseFigure();
            g.FillPath(frameBrush, nub);

            if (look.Level is { } level && level > 0)
            {
                var low = level <= MainViewModel.LowBatteryThreshold;
                using var fillBrush = new SolidBrush(low ? (look.LightTaskbar ? LowOnLight : LowOnDark) : ink);
                var width = Math.Max(1, Px((bodyWidth - 2 * inset) * Math.Min(level, 100) / 100f));
                var height = bodyHeight - 2 * inset;
                using var fill = RoundedRect(inset, top + inset, width, height, Math.Min(0.8f * unit, Math.Min(width, height) / 2f));
                g.FillPath(fillBrush, fill);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
    {
        var d = 2 * radius;
        var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static int Px(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);

    private static bool TaskbarIsLight() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is 1;

    // Taille des petites icônes à l'échelle de la barre des tâches : 16 px à 100 %, 20 px à 125 %…
    private static int IconSize()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var dpi = taskbar == IntPtr.Zero ? 0 : GetDpiForWindow(taskbar);
        return GetSystemMetricsForDpi(SM_CXSMICON, dpi == 0 ? 96 : dpi);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
