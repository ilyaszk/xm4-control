using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Xm4Control.App;

/// <summary>Icône de la zone de notification : glyphe "casque" monochrome, comme les icônes système.</summary>
internal static class TrayIcons
{
    private const int Size = 32;
    private static readonly string Headphone = MainViewModel.Glyph(0xE7F6);
    private static readonly string? GlyphFont = FindFont("Segoe Fluent Icons", "Segoe MDL2 Assets");

    public static Icon Create(bool connected)
    {
        var color = TaskbarIsLight() ? Color.Black : Color.White;
        if (!connected) color = Color.FromArgb(110, color);

        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(color);
            if (GlyphFont is null)
            {
                g.FillEllipse(brush, 2, 2, Size - 4, Size - 4);
            }
            else
            {
                using var font = new Font(GlyphFont, Size * 0.85f, GraphicsUnit.Pixel);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(Headphone, font, brush, new RectangleF(0, 1, Size, Size), format);
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
