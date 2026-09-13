// Mark.cs: the shape of the OpenDash mark, in the coordinates media/logo.svg declares.
//
// Kept apart from Widgets.cs, which is WPF and cannot be compiled by the net8.0 test project. The numbers are the
// thing worth testing, so they live where a test can reach them and Widgets draws from here.
namespace OpenDashPlugin
{
    /// <summary>
    /// The dash: a housing wider than it is tall, square across the bottom and closed over the top by a half
    /// circle, with a tachometer needle swept up and to the right inside it.
    /// </summary>
    /// <remarks>
    /// One shape with one hole. <see cref="PathData"/> is the housing followed by the needle, and the even-odd fill
    /// rule cuts the second out of the first, so the needle is whatever is behind the mark rather than a second
    /// colour. The needle runs from the tangent points of its own hub out to a single point, which is what makes it
    /// a needle rather than a comma at 24 px, and it pivots at the half circle's own centre.
    ///
    /// The same string is the SVG's `d`, character for character, and MarkTests compares them. That is a stronger
    /// join than the four numbers per bar this held before: the mark used to be rectangles, and the copies had
    /// still drifted, because the test compared positions and sizes and the panel was rounding the corners.
    /// </remarks>
    public static class MarkShape
    {
        /// <summary>The side of the square the mark is drawn in, in the SVG's own units.</summary>
        public const double Box = 32;

        /// <summary>The housing: left, top, width, height, and the radius of the half circle over it.</summary>
        public const double HousingX = 1;
        public const double HousingY = 5;
        public const double HousingWidth = 30;
        public const double HousingHeight = 22;

        /// <summary>Half the width, which is what makes the top one half circle rather than two rounded corners.</summary>
        public const double HousingRadius = HousingWidth / 2;

        /// <summary>The needle: where it pivots, how big its hub is, and how far and how high it reaches.</summary>
        public const double PivotX = 16;
        public const double PivotY = HousingY + HousingRadius;
        public const double HubRadius = 3;
        public const double SweepDegrees = 52;
        public const double NeedleLength = 12;

        /// <summary>
        /// The whole mark as SVG path data: the housing, then the needle, which the even-odd rule makes a hole.
        /// WPF reads the same string; <c>Ui.Mark</c> prefixes "F0 " for the even-odd rule the SVG spells out.
        /// </summary>
        public const string PathData =
            "M1,27 L1,20 A15,15 0 0 1 31,20 L31,27 Z " +
            "M14.173,17.621 L23.388,10.544 L18.751,21.197 A3,3 0 1 1 14.173,17.621 Z";
    }
}
