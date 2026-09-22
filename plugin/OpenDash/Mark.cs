// Mark.cs: the shape of the OpenDash mark, in the coordinates media/logo.svg declares.
//
// Kept apart from Widgets.cs, which is WPF and cannot be compiled by the net8.0 test project. The numbers are the
// thing worth testing, so they live where a test can reach them and Widgets draws from here.
namespace OpenDashPlugin
{
    /// <summary>
    /// The face's own layout: a full width strip across the top, a full width strip across the bottom, and three
    /// zones between them, the middle one wider than its neighbours because the gear is.
    /// </summary>
    /// <remarks>
    /// One shape with four holes. <see cref="PathData"/> is the housing followed by the two band separations and
    /// the two column separations, and the even-odd fill rule cuts them out of it, so every separation is whatever
    /// is behind the mark rather than a second colour. The vertical slots run through the middle band only,
    /// because the header and the footer are full width on the face and it is the middle that is divided.
    ///
    /// The same string is the SVG's `d`, character for character, and MarkTests compares them, which is a
    /// stronger join than the constants alone: the mark used to be rectangles held by four numbers per bar, and
    /// the copies still drifted, because the test compared positions and sizes and the panel was rounding the
    /// corners.
    /// </remarks>
    public static class MarkShape
    {
        /// <summary>The side of the square the mark is drawn in, in the SVG's own units.</summary>
        public const double Box = 32;

        /// <summary>The housing: left, top, width and height. Square on all four corners, which is radius.none.</summary>
        public const double HousingX = 1;
        public const double HousingY = 5;
        public const double HousingWidth = 30;
        public const double HousingHeight = 22;

        /// <summary>The full width strips. The header is the thicker of the two, as it is on the face.</summary>
        public const double HeaderHeight = 3.5;
        public const double FooterHeight = 2.5;

        /// <summary>
        /// What separates one zone from the next. The face draws a one pixel rule; the mark cannot, so it spends
        /// 2.5 units, which is what survives being drawn at 24 px.
        /// </summary>
        public const double Gap = 2.5;

        /// <summary>The gear zone, and the two that flank it. Wider, because on the face it is.</summary>
        public const double CentreWidth = 10.5;
        public const double SideWidth = (HousingWidth - CentreWidth - 2 * Gap) / 2;

        /// <summary>Where the three zones start and stop, which is what the vertical slots span.</summary>
        public const double MiddleTop = HousingY + HeaderHeight + Gap;
        public const double MiddleBottom = HousingY + HousingHeight - FooterHeight - Gap;

        /// <summary>
        /// The whole mark as SVG path data: the housing, then the two band separations, then the two column
        /// separations, which the even-odd rule makes holes. WPF reads the same string; <c>Ui.Mark</c> prefixes
        /// "F0 " for the even-odd rule the SVG spells out.
        /// </summary>
        public const string PathData =
            "M1,27 L1,5 L31,5 L31,27 Z " +
            "M1,11 L1,8.5 L31,8.5 L31,11 Z " +
            "M1,24.5 L1,22 L31,22 L31,24.5 Z " +
            "M8.25,22 L8.25,11 L10.75,11 L10.75,22 Z " +
            "M21.25,22 L21.25,11 L23.75,11 L23.75,22 Z";
    }
}
