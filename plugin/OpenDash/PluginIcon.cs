// PluginIcon.cs: the 32x32 icon SimHub shows beside "OpenDash" in its left menu.
//
// It is the mark itself, rendered from the same Ui.Mark the panel draws, so the menu carries the shape
// media/logo.svg declares rather than a second drawing of it. It used to be a lone rounded bar drawn with
// System.Drawing, which reads as a placeholder at 32 px rather than as a logo.
//
// SimHub paints the icon in the menu's own foreground and keeps only the alpha, so the mark arrives white
// whatever Ui.Mark fills it with, and its separations read at all because the even-odd rule cuts them out
// of the housing rather than drawing them in a second colour. Anything drawn here has to survive being
// reduced to a silhouette, and the menu draws it at 24 px, which is the size MarkTests holds the mark to.
// Null when rendering fails, which leaves the entry without an icon rather than unloadable.
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenDashPlugin
{
    internal static class PluginIcon
    {
        public static ImageSource Create()
        {
            try
            {
                var side = (int)MarkShape.Box;
                var mark = Ui.Mark(MarkShape.Box);
                mark.Measure(new Size(side, side));
                mark.Arrange(new Rect(0, 0, side, side));
                var bitmap = new RenderTargetBitmap(side, side, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(mark);
                bitmap.Freeze(); // Cached in a field and read by whichever thread asks for the property.
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not draw the menu icon: " + ex.Message);
                return null;
            }
        }
    }
}
