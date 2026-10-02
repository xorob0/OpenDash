// PanelCopy.cs: what the settings panel says, and the table that pairs a state with its verb.
//
// Apart from Widgets.cs for the reason PanelMetrics.cs is: the panel is WPF and the net8.0 test project
// cannot compile it, so the words live where PanelCopyTests can pin them character for character, as
// UpdateWordingTests pins the update sentences. Pure: no WPF types.
//
// Shared by every page, and not divided into per-page regions: a page adds the constants it needs and changes
// only those its own page alone draws, never one another page reads (SettingsControl.cs's ownership table).
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
        /// <summary>The line under OpenDash's name in SimHub's "New plugins have been detected !" prompt.</summary>
        /// <remarks>
        /// SimHub's PluginFinder.GetDescription looks up "PluginDescription_" + the class name in its own
        /// translations and falls back to the class's [PluginDescription]. It has no translation for a
        /// third-party plugin, so without the attribute the prompt printed the key itself,
        /// "PluginDescription_OpenDash" (#475). It is the first sentence of OpenDash a driver reads.
        /// </remarks>
        public const string PluginDescription = "Dashboards for the screens on your rig, and a page to choose what each one shows.";

        /// <summary>The sentence under the empty rig's pill, which the pill has already said is empty.</summary>
        /// <remarks>
        /// Here rather than inline in the WPF file so that a test can hold the wording: it is the one
        /// sentence a new user reads before anything else on the page, and the pill above it carries the
        /// announcement, so this says what adding a screen does instead of repeating that there is none.
        /// </remarks>
        public const string EmptyRig = "Add the screen your rig has.";
        public const string Installing = "Installing";

        /// <summary>The flag box's by-hand route (SettingsControl.Profiles.cs), which Matrix and Updates both
        /// draw: its press, the press's hover, the path box's hover, and what a copy says. A failure points at
        /// the log rather than quoting an exception (voice.md).</summary>
        public const string CopyForImport = "Copy to SimHub's import folder";
        public const string CopyForImportTooltip = "Puts a copy in Documents\\SimHub.";
        public const string ImportPathTooltip = "Where OpenDash left the profile.";
        public const string CopyForImportFailed = "Could not copy the profile. See SimHub's log.";

        public static string CopiedForImport(string path)
        {
            return "Copied to " + path + ". In SimHub, open your device's profiles and press Import.";
        }
        public const string Installed = "Installed";
        public const string NotInstalled = "Not installed";
        public const string InstallFailed = "Install failed";

        /// <summary>
        /// A dashboard written after SimHub started, which SimHub has not read yet: one phrase for the one state
        /// on every page that draws it (#524, ruling 1). The Screens card's state and its fix box's title, Home's
        /// screen line and the first step of Home's issue, and the Updates table's state all read this constant.
        /// </summary>
        /// <remarks>
        /// Home's issue keeps a title that names the screen ("Rim is not in SimHub yet"), since a list of issues
        /// is read by what each is about, and says this phrase as its step. Where a card or a cell is too narrow
        /// for it the phrase wraps to a second line; it is never trimmed, since a trimmed step is one nobody can
        /// follow.
        /// </remarks>
        public const string RestartToLoad = "Restart SimHub to load it";

        /// <summary>
        /// The sentence a glance row ends on: the binding is a hold, and the press type the dialog
        /// offers is not the driver's to choose.
        /// </summary>
        /// <remarks>
        /// SettingsControl.HoldWhilePressed rewrites every mapping made on a glance binder to SimHub's
        /// `During`, the one press type under which SimHub calls the release, and the dialog opens on
        /// ShortAndLongPress. The correction stays (#435): a glance bound any other way appears and
        /// vanishes in one frame, so there is no other choice a driver could make and keep a glance,
        /// and a warning would leave that binding in place to fail. What changed is that the row says
        /// so, so a driver who picks a press type and watches it change back has been told why.
        /// </remarks>
        public const string GlanceBoundAsHold = "Bound as a hold, whatever press type you pick.";

        /// <summary>The glance row's caption on a face.</summary>
        public const string FaceGlance = "Hold to show one page, release to return. " + GlanceBoundAsHold;

        /// <summary>The glance row's caption on a pit wall, where the page is lent to one zone.</summary>
        public const string PitWallGlance = "Hold to show one page, release to put the zone back. " + GlanceBoundAsHold;

        /// <summary>The glance row's caption on a companion, where release goes back to whichever module
        /// was up, including one a tap paged to.</summary>
        public const string CompanionGlance = "Hold to show one module, release to go back to the one you were on. " + GlanceBoundAsHold;

        /// <summary>
        /// How a companion is paged, and where the button for it is bound, which is not in OpenDash.
        /// </summary>
        /// <remarks>
        /// SimHub pages a companion, and its NextScreen and PreviousScreen are bound in the Controls and
        /// events of the device the companion runs on. The panel cannot host that binder the way it
        /// hosts its own, since SimHub's ControlsEditor binds a plugin action by name and these belong
        /// to a device, so the sentence names the place rather than only the action: a driver told
        /// only what to bind was left to find where (#435). plugin/INSTALL.md and the site's install
        /// page carry the same passage.
        /// </remarks>
        public const string CompanionPaging =
            "Tap the left or right half of the screen to change module. For a wheel button, open the "
            + "device or window the companion runs on in SimHub, go to its Controls and events, and bind "
            + "NextScreen, with PreviousScreen to go back. Those bindings belong to that device, so the "
            + "button that pages the companion does not page your dash.";

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
        /// have one. A kind is either drawn or written, never neither, and written by its one name,
        /// PanelAddScreen.KindName ("Round"), never the settings model's "Slots".</summary>
        public static string KindWord(string kind)
        {
            return string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal) ? PanelAddScreen.KindName(kind) : null;
        }

        /// <summary>The card's second line: the size OpenDash installed, behind the kind when the kind has
        /// no icon to be carried by.</summary>
        public static string SizeLine(string kind, string size)
        {
            var word = KindWord(kind);
            return word == null ? size : word + Join + size;
        }

        /// <summary>
        /// What a light profile's row on the Updates page says, and the press it offers, from the state the
        /// install plan found it in.
        /// </summary>
        /// <remarks>
        /// The Updates page's: its "In SimHub" table reads it, dot included (PanelUpdates), and the Updates
        /// agent may reword it. The Matrix page's header row has its own table, PanelMatrix.ProfileRow, and
        /// its pill's dot, PanelLightRows.DotHex, is a fixed ink per state that reads neither table.
        ///
        /// The state is a word and the version has a column of its own, so the words are the four the
        /// dashboards' rows use (InstallStatus.Label): an older profile is "Update available" in the update
        /// ink, as the artboard draws "Out of date". The one press the table carries is an older profile's
        /// Update (PanelUpdates.RowUpdate), and every other state offers none, so its button is null; the
        /// page draws it as an outline, its one primary being the update card's Download. A state the page
        /// cannot know -- SimHub's settings out of reach, or no profile in this build to compare with -- is
        /// "Unknown", the word the table already writes for a version it cannot read, and the row's hover
        /// says why: calling it "Not installed" would claim what nobody has checked.
        ///
        /// Only State and StateHex are drawn. The row's press is PanelUpdates.StripRow's and FlagBoxRow's
        /// OffersUpdate, since it also depends on SimHub listing the strip's device, so Button and Style here
        /// are the RowAction's shape, read by no page: pinned as an outline so a primary never creeps back.
        /// </remarks>
        public static RowAction LightRow(FlagBoxInstallState state, string installedVersion)
        {
            switch (state)
            {
                case FlagBoxInstallState.Outdated:
                    return new RowAction(InstallStatus.UpdateAvailable.Label(), Theme.StatusUpdateAvailable, PanelUpdates.RowUpdate, PanelButton.Outline);
                case FlagBoxInstallState.UpToDate:
                    return new RowAction(InstallStatus.UpToDate.Label(), Theme.StatusUpToDate, null, PanelButton.Outline);
                case FlagBoxInstallState.Failed:
                    return new RowAction(InstallFailed, Theme.StatusFailed, null, PanelButton.Outline);
                case FlagBoxInstallState.Unavailable:
                case FlagBoxInstallState.NotEmbedded:
                    return new RowAction(PanelUpdates.Unknown, Theme.TextLabel, null, PanelButton.Outline);
                default:
                    return new RowAction(NotInstalled, Theme.TextLabel, null, PanelButton.Outline);
            }
        }

        /// <summary>
        /// What a strip's row says: <see cref="LightRow"/>'s words, except that a strip whose profile this build
        /// does not ship reads "Not installed", as the LEDs card and Home say it (#524, ruling 2), where the flag
        /// box's row keeps "Unknown".
        /// </summary>
        public static RowAction StripRow(FlagBoxInstallState state, string installedVersion)
        {
            return LightRow(state == FlagBoxInstallState.NotEmbedded ? FlagBoxInstallState.NotInstalled : state, installedVersion);
        }
    }
}
