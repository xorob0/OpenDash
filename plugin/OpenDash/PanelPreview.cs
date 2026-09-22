// PanelPreview.cs: the box a live preview is drawn in, and the one line said where there is none.
//
// Apart from ScreenPreview.cs for the reason PanelFacePlan.cs is apart from SettingsControl.Panes.cs:
// the preview itself is SimHub's own WPF control and the net8.0 test project cannot compile a line of
// it, so the decision a test can hold lives where PanelPreviewTests can reach it. Pure: no WPF, no
// SimHub.
//
// The fit is uniform and it never re-lays anything out. A page is laid out for the shape of the box it
// is given (docs/design/zones.md), so the one honest way to show a 1920 x 480 face in a pane 896 wide
// is to scale the whole dashboard by one factor and let it be small. Anything else would be a second
// layout, drawn by us, of a face the driver's screen draws differently, which is the class of lie
// ADR 0008 refused and ADR 0020 keeps refusing.
using System;

namespace OpenDashPlugin
{
    /// <summary>The size a dashboard is drawn at inside the pane.</summary>
    public struct PreviewBox
    {
        public double Width;
        public double Height;

        /// <summary>What the dashboard's own pixels are multiplied by. 1 is actual size, and 0 is a
        /// dashboard there is no room to draw, which is the caller's signal not to ask SimHub for a
        /// renderer at all.</summary>
        public double Scale;
    }

    public static class PanelPreview
    {
        /// <summary>
        /// The tallest the preview is allowed to be, whatever shape the screen is.
        /// </summary>
        /// <remarks>
        /// A portrait face is 600 x 686 and a portrait pit wall is 1080 x 1920, so fitting on width
        /// alone would hand the first one 1024 pixels of the pane and the second 1593, and the controls
        /// the preview sits above would be below the fold on every window smaller than the artboard.
        /// </remarks>
        public const double MaxHeight = 320;

        /// <summary>
        /// The box a screen of this size is drawn in: uniform, fitted to whichever of the two limits
        /// binds first, and never enlarged past actual size.
        /// </summary>
        /// <remarks>
        /// The clamp at 1 is what makes a 480 round preview worth looking at. Every face smaller than
        /// the pane is drawn at exactly the pixels the driver's screen draws, and only the faces that
        /// cannot fit are reduced.
        /// </remarks>
        public static PreviewBox Fit(int width, int height, double available, double maxHeight)
        {
            if (width <= 0 || height <= 0 || available <= 0 || maxHeight <= 0)
            {
                return new PreviewBox { Width = 0, Height = 0, Scale = 0 };
            }

            var scale = Math.Min(available / width, maxHeight / height);
            if (scale > 1) scale = 1;
            return new PreviewBox { Width = width * scale, Height = height * scale, Scale = scale };
        }

        /// <summary>The one line the pane shows where SimHub could not draw the screen. Why it could not
        /// goes to the log, which is where a contributor reads and a driver does not.</summary>
        public const string FailedLine = "SimHub could not draw this screen here.";
    }
}
