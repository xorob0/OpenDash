// PanelCopy.cs: what the settings panel says, and the table that pairs a state with its verb.
//
// Apart from Widgets.cs for the reason PanelMetrics.cs is: the panel is WPF and the net8.0 test project
// cannot compile it, so the words live where PanelCopyTests can pin them character for character, as
// UpdateWordingTests pins the update sentences. Pure: no WPF types.
using System;
using System.Globalization;

namespace OpenDashPlugin
{
    /// <summary>Which of the canvas's two button faces an action wears. One primary per panel, so a row
    /// that offers a choice offers it as an outline.</summary>
    public enum PanelButton
    {
        Primary,
        Outline,
    }

    /// <summary>The right-hand side of an install row: what state the thing is in, the colour that state
    /// is said in, and the button beside it.</summary>
    public sealed class RowAction
    {
        public RowAction(string state, string stateHex, string button, PanelButton style)
        {
            State = state;
            StateHex = stateHex;
            Button = button;
            Style = style;
        }

        public string State { get; private set; }
        public string StateHex { get; private set; }
        public string Button { get; private set; }
        public PanelButton Style { get; private set; }
    }

    public static class PanelCopy
    {
        public const string AddScreen = "Add a screen";

        /// <summary>The sentence under the empty rig's pill, which the pill has already said is empty.</summary>
        /// <remarks>
        /// Here rather than inline in the WPF file so that a test can hold the wording: it is the one
        /// sentence a new user reads before anything else on the page, and the pill above it carries the
        /// announcement, so this says what adding a screen does instead of repeating that there is none.
        /// </remarks>
        public const string EmptyRig =
            "Add the screen your rig has and openDash installs its dashboard into SimHub.";
        public const string Installing = "Installing";
        public const string Installed = "Installed";
        public const string NotInstalled = "Not installed";
        public const string InstallFailed = "Install failed";

        /// <summary>The separator the panel joins facts with, which is the canvas's middle dot.</summary>
        private const string Join = " · ";

        /// <summary>"Installed · 0.4.0", or the word alone when the copy in SimHub does not say which
        /// version it is.</summary>
        public static string InstalledAt(string version)
        {
            return string.IsNullOrEmpty(version) ? Installed : Installed + Join + version;
        }

        /// <summary>"62%", from a fraction. Clamped where PanelMetrics clamps the bar, so the number and
        /// the bar cannot disagree.</summary>
        public static string Percent(double fraction)
        {
            return PanelMetrics.PercentOf(fraction).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The word a kind needs because the canvas gives it no icon, and null for the three that
        /// have one. A kind is either drawn or written, never neither.</summary>
        public static string KindWord(string kind)
        {
            return string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal) ? "slots" : null;
        }

        /// <summary>The card's second line: the size openDash installed, behind the kind when the kind has
        /// no icon to be carried by.</summary>
        public static string SizeLine(string kind, string size)
        {
            var word = KindWord(kind);
            return word == null ? size : word + Join + size;
        }

        /// <summary>
        /// What a light profile's row says and offers, from the state the install plan found it in.
        /// </summary>
        /// <remarks>
        /// One function rather than a label here and a style there, so that the panel cannot pair a verb
        /// with the wrong button: an update is the one accented action on the page, and everything else is
        /// an outline. The canvas draws two of these rows, the older profile and the uninstalled one. A
        /// profile already at this version takes the panel's own Reinstall wording, which the "This
        /// plugin" section draws as an outline; a failed install says what the status pill says and offers
        /// the same press again. Nothing is embedded and SimHub being unreachable have no row of their
        /// own, the section's own sentence covering both, so they read as not installed.
        /// </remarks>
        public static RowAction LightRow(FlagBoxInstallState state, string installedVersion)
        {
            switch (state)
            {
                case FlagBoxInstallState.Outdated:
                    return new RowAction(InstalledAt(installedVersion), Theme.StatusUpToDate, "Update", PanelButton.Primary);
                case FlagBoxInstallState.UpToDate:
                    return new RowAction(InstalledAt(installedVersion), Theme.StatusUpToDate, "Reinstall", PanelButton.Outline);
                case FlagBoxInstallState.Failed:
                    return new RowAction(InstallFailed, Theme.StatusFailed, "Install", PanelButton.Outline);
                default:
                    return new RowAction(NotInstalled, Theme.TextLabel, "Install", PanelButton.Outline);
            }
        }

        /// <summary>
        /// What a screen package's row says and offers.
        /// </summary>
        /// <remarks>
        /// Written and not yet drawn: whether the Install tab removes a screen at all is XOR's plugin-63,
        /// which is not settled. The table is here so that the answer, when it comes, is a call rather
        /// than a second set of strings.
        /// </remarks>
        public static RowAction ScreenRow(bool installed)
        {
            return installed
                ? new RowAction(Installed, Theme.StatusUpToDate, "Remove", PanelButton.Outline)
                : new RowAction(NotInstalled, Theme.TextLabel, "Add", PanelButton.Outline);
        }
    }
}
