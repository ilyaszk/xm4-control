using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Xm4Control.App;

/// <summary>Icônes dessinées à partir du glyphe "casque" des polices d'icônes Windows.</summary>
internal static class AppIcons
{
    private const string Headphone = "";
    private static readonly string? GlyphFont = FindFont("Segoe Fluent Icons", "Segoe MDL2 Assets");

    public static Icon Tray(bool connected)
    {
        var color = TaskbarIsLight() ? Color.Black : Color.White;
        return Render(SystemInformation.SmallIconSize.Width, connected ? color : Color.FromArgb(110, color));
    }

    public static Icon Window() => Render(32, Color.FromArgb(0, 120, 212));

    private static Bitmap RenderBitmap(int size, Color color)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(color);
        if (GlyphFont is null)
        {
            g.FillEllipse(brush, 1, 1, size - 2, size - 2);
            return bitmap;
        }
        using var font = new Font(GlyphFont, size * 0.85f, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Headphone, font, brush, new RectangleF(0, 1, size, size), format);
        return bitmap;
    }

    private static Icon Render(int size, Color color)
    {
        using var bitmap = RenderBitmap(size, color);
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

    private static bool TaskbarIsLight() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is 1;

    private static string? FindFont(params string[] names)
    {
        using var fonts = new InstalledFontCollection();
        return names.FirstOrDefault(name => fonts.Families.Any(f => f.Name == name));
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
