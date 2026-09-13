// Mark.cs: the shape of the OpenDash mark, in the coordinates media/logo.svg declares.
//
// Kept apart from Widgets.cs, which is WPF and cannot be compiled by the net8.0 test project. The numbers are the
// thing worth testing, so they live where a test can reach them and Widgets draws from here.
namespace OpenDashPlugin
{
    /// <summary>
    /// The rev bar as a mark: four rising segments and a separated fifth, which is the shift point.
    /// </summary>
    /// <remarks>
    /// Four units of thirty-two is the narrowest anything here gets, which is two pixels when the mark is drawn at
    /// the 16 px of a browser tab, so nothing in it disappears. It carries one colour and reads as a shape without
    /// it, which is what makes it usable monochrome on a dark panel.
    /// </remarks>
    public static class MarkShape
    {
        /// <summary>The side of the square the mark is drawn in, in the SVG's own units.</summary>
        public const double Box = 32;

        /// <summary>Each bar as x, y, width, height, in the SVG's units and its order.</summary>
        public static readonly double[][] Bars =
        {
            new[] { 1d, 20d, 5d, 8d },
            new[] { 8d, 16d, 5d, 12d },
            new[] { 15d, 12d, 5d, 16d },
            new[] { 22d, 7d, 5d, 21d },
            new[] { 29d, 2d, 2d, 26d },
        };
    }
}
