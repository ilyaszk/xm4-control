using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Xm4Control.App.Views;

namespace Xm4Control.App;

/// <summary>Icône de la zone de notification : le logo de l'appli, atténué quand le casque n'est pas connecté.</summary>
internal static class TrayIcons
{
    private const int Size = 32;
    private const float DisconnectedOpacity = 0.4f;

    public static Icon Create(bool connected)
    {
        // Choisit l'image de app.ico la plus proche de la taille demandée.
        using var logo = new Icon(Ui.IconPath, Size, Size);
        using var source = logo.ToBitmap();
        if (connected) return (Icon)logo.Clone();

        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // Niveaux de gris et transparence : même convention que les icônes système hors ligne.
            var matrix = new ColorMatrix(
            [
                [0.3f, 0.3f, 0.3f, 0, 0],
                [0.59f, 0.59f, 0.59f, 0, 0],
                [0.11f, 0.11f, 0.11f, 0, 0],
                [0, 0, 0, DisconnectedOpacity, 0],
                [0, 0, 0, 0, 1],
            ]);
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            g.DrawImage(source, new Rectangle(0, 0, Size, Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
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

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
