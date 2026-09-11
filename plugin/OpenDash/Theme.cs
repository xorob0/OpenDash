// Theme.cs: the settings panel's colours, type and sizes, mirrored from design/tokens.json.
// They are hard-coded so that the assembly does not read the token file at runtime; every constant carries
// the token path it mirrors in a trailing comment, and OpenDash.Tests resolves each path in tokens.json and
// fails when a value drifts. Pure: no WPF types here (Widgets.cs turns the hex strings into brushes).
namespace OpenDashPlugin
{
    public static class Theme
    {
        // Surfaces
        public const string SurfaceBase = "#0A0B0D"; // color.surface.base
        public const string SurfaceZone = "#14161A"; // color.surface.zone
        public const string SurfaceRaised = "#1C1F24"; // color.surface.raised

        // Text
        public const string TextPrimary = "#F5F7FA"; // color.text.primary
        public const string TextSecondary = "#8A9099"; // color.text.secondary
        public const string TextLabel = "#5A6069"; // color.text.label
        public const string TextDim = "#33383F"; // color.text.dim

        // Plugin UI (brand cyan is allowed here and nowhere on the dash face)
        public const string Accent = "#33D9F2"; // purpose.ui.accent
        public const string AccentHover = "#5CE1F5"; // purpose.ui.accentHover
        public const string OnAccent = "#0A0B0D"; // purpose.ui.onAccent
        public const string Border = "#33383F"; // purpose.ui.border
        public const string Rule = "#1C1F24"; // purpose.ui.rule
        public const string Hover = "#1C1F24"; // purpose.ui.hover
        public const string Field = "#14161A"; // purpose.ui.field
        public const string Caution = "#FFB300"; // color.caution.primary

        // Install status dot
        public const string StatusUpToDate = "#00D96A"; // purpose.status.upToDate
        public const string StatusUpdateAvailable = "#FFB300"; // purpose.status.updateAvailable
        public const string StatusFailed = "#FF2D46"; // purpose.status.failed
        public const string StatusNotInstalled = "#33383F"; // purpose.status.notInstalled

        // Type
        public const string FontData = "openDash Display"; // font.family.data
        public const string FontLabel = "Barlow"; // font.family.label
        public const double SizeWordmark = 28; // font.size.ui.wordmark
        public const double SizeTitle = 16; // font.size.ui.title
        public const double SizeBody = 14; // font.size.ui.body
        public const double SizeSmall = 13; // font.size.ui.small
        public const double SizeLabel = 12; // font.size.ui.label
        public const double SizeNumeral = 16; // font.size.ui.numeral
        public const double TrackingLabel = 0.14; // font.tracking.label

        // Controls
        public const double ControlHeight = 32; // control.height
        public const double ControlHeightSm = 24; // control.heightSm
        public const double IconSize = 16; // control.icon
        public const double Radius = 2; // radius.sm
    }
}
