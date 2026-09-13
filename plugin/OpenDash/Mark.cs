// Mark.cs: the shape of the OpenDash mark, in the coordinates media/logo.svg declares.
//
// Kept apart from Widgets.cs, which is WPF and cannot be compiled by the net8.0 test project. The numbers are the
// thing worth testing, so they live where a test can reach them and Widgets draws from here.
namespace OpenDashPlugin
{
    /// <summary>
    /// The rev bar as a mark: three segments sweeping up and to the right, and a fourth, set apart and heavier,
    /// which is the shift point.
    /// </summary>
    /// <remarks>
    /// Three progressions run together and none of them is a baseline: each segment starts 6 units higher than the
    /// one before, ends 4 units higher, and is 2 units taller. Bars sharing a baseline read as a measurement, and
    /// this is an instrument. The segments are hard-edged, as radius.none says race dashes are, and nothing is
    /// narrower than 5 units of 32 or spaced closer than 3, so at the 16 px of a browser tab it neither loses a
    /// segment nor merges into a blob. It carries one colour and reads as a shape without it.
    /// </remarks>
    public static class MarkShape
    {
        /// <summary>The side of the square the mark is drawn in, in the SVG's own units.</summary>
        public const double Box = 32;

        /// <summary>Each segment as x, y, width, height, in the SVG's units and its order.</summary>
        public static readonly double[][] Bars =
        {
            new[] { 0d, 20d, 5d, 10d },
            new[] { 8d, 14d, 5d, 12d },
            new[] { 16d, 8d, 5d, 14d },
            new[] { 26d, 2d, 6d, 16d },
        };
    }
}
