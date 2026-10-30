using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Xm4Control.App;

/// <summary>Icône de la zone de notification : glyphe "casque" monochrome, comme les icônes système.</summary>
internal static class TrayIcons
{
    private static readonly string Headphone = char.ConvertFromUtf32(0xE7F6); // glyphe "casque" de Segoe Fluent Icons
    private const int Size = 32;
    private static readonly Typeface Glyphs = new(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static System.Drawing.Icon Create(bool connected)
    {
        var color = TaskbarIsLight() ? Colors.Black : Colors.White;
        if (!connected) color.A = 110;

        var text = new FormattedText(Headphone, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Glyphs, Size, Brushes.Black, 1.0);
        var geometry = text.BuildGeometry(new Point());
        var bounds = geometry.Bounds;
        var scale = (Size - 2) / Math.Max(bounds.Width, bounds.Height);
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2));
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new TranslateTransform(Size / 2.0, Size / 2.0));
        geometry.Transform = transform;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawGeometry(new SolidColorBrush(color), null, geometry);
        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        // H.NotifyIcon ne sait convertir que des images chargées depuis un fichier : on passe par une icône GDI.
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        png.Position = 0;
        using var gdiBitmap = new System.Drawing.Bitmap(png);
        var handle = gdiBitmap.GetHicon();
        try
        {
            return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static bool TaskbarIsLight() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is 1;
}
