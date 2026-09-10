// PluginIcon.cs: the 32x32 icon SimHub shows beside "openDash" in its left menu: the cyan segment mark of
// the wordmark, drawn with System.Drawing and handed to SimHub's ToIcon(). Null when drawing fails.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Media;
using SimHub.Plugins;

namespace OpenDashPlugin
{
    internal static class PluginIcon
    {
        public static ImageSource Create(IWPFSettings plugin)
        {
            try
            {
                using (var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        graphics.Clear(System.Drawing.Color.Transparent);
                        using (var path = RoundedRectangle(new RectangleF(2, 11, 28, 10), (float)Theme.Radius))
                        using (var brush = new SolidBrush(ColorTranslator.FromHtml(Theme.Accent)))
                        {
                            graphics.FillPath(brush, path);
                        }
                    }
                    return plugin.ToIcon(bitmap);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not draw the menu icon: " + ex.Message);
                return null;
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
