// PanelMatrix.cs: the Matrix page's words, numbers and decisions -- everything SettingsControl.Matrix.cs
// draws, so a test can hold it.
//
// The page is Matrix.dc.html: the flag box profile on the title's line, a card for each matrix the rig has,
// and for the one selected its preview beside what may take it over, in priority order, and what it shows
// at rest. The words follow docs/design/voice.md where the artboard differs, and docs/design/plugin.md's
// departures table records each difference: one noun, "matrix", for the thing a driver owns; the row names
// the panel has always used ("Idle display", "Mounting side", "Redline flash"); and a profile named the way
// SimHub lists it.
//
// The matrix copy PanelLights still holds for the old tab is copied here rather than moved, because
// PanelLights is the LEDs page's; this page reads none of it. Pure: no WPF.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelMatrix
    {
        public const string Title = "Matrix";

        /// <summary>
        /// The empty state, here and under Home's Matrix eyebrow, which reads this constant. One noun for the thing
        /// a driver owns (voice.md, "One word per thing"): a matrix, never a panel, which in this panel is the
        /// settings window itself.
        /// </summary>
        public const string NoPanels = "No matrices yet.";

        /// <summary>How many a rig can have: SimHub composes four matrix contents and not five.</summary>
        public const int MaxPanels = 4;

        // --- The profile, on the title's line ---------------------------------------------------------

        /// <summary>
        /// What the flag box profile's line says after the profile's name, the ink of the dot beside it, and the
        /// press. The Matrix page's own table: the Updates page's rows read PanelCopy.LightRow, so neither page
        /// rewords a state for the other. The state is the version SimHub holds, since the name already says which
        /// profile it is: "OpenDash Flag box · 0.5.0". With the line reduced to the name and the version, as
        /// Matrix.dc.html's title line draws it, the dot is the one part of it that tells an older profile from a
        /// current one, so each state's ink is the dot's, drawn by the page itself (the words are text.secondary in
        /// every state): a current profile green, an older one purpose.status.updateAvailable as the sidebar's dot,
        /// Home and the Updates artboard draw it, a failed one red, and none purpose.status.notInstalled.
        /// </summary>
        /// <param name="hasMatrix">Whether the rig has a matrix. Without one, Home files no update issue, the
        /// sidebar draws no dot and Updates leaves the profile's row out (PanelUpdates.DrawsFlagBoxRow), so an
        /// older profile's Update is an outline press like Install and Reinstall rather than the page's one
        /// primary: no page offers it as something to act on while nothing on the rig uses it.</param>
        public static RowAction ProfileRow(FlagBoxInstallState state, string installedVersion, bool hasMatrix = true)
        {
            var installed = string.IsNullOrEmpty(installedVersion) ? Installed : installedVersion;
            switch (state)
            {
                case FlagBoxInstallState.Outdated:
                    return new RowAction(installed, Theme.StatusUpdateAvailable, Update, hasMatrix ? PanelButton.Primary : PanelButton.Outline);
                case FlagBoxInstallState.UpToDate:
                    return new RowAction(installed, Theme.StatusUpToDate, "Reinstall", PanelButton.Outline);
                case FlagBoxInstallState.Failed:
                    return new RowAction(PanelCopy.InstallFailed, Theme.StatusFailed, "Install", PanelButton.Outline);
                default:
                    return new RowAction(PanelCopy.NotInstalled, Theme.StatusNotInstalled, "Install", PanelButton.Outline);
            }
        }

        /// <summary>The press over an older profile, which its hover names (<see cref="UpdateBringsItTo"/>).</summary>
        public const string Update = "Update";

        /// <summary>The state of a profile SimHub holds whose version it does not say.</summary>
        public const string Installed = "Installed";

        /// <summary>What the title's line says when this build carries no profile to install.</summary>
        public const string NoProfile = "This build ships no flag box profile.";

        /// <summary>
        /// The line beside the status dot: the profile as SimHub lists it and its state. A build with no
        /// profile says so and nothing further; a SimHub whose matrix settings could not be reached names the
        /// profile only, since the by-hand import under the title says the rest.
        /// </summary>
        public static string ProfileLine(string profile, FlagBoxInstallState state, string installedVersion)
        {
            if (state == FlagBoxInstallState.NotEmbedded) return NoProfile;
            if (state == FlagBoxInstallState.Unavailable) return profile;
            return profile + " · " + ProfileRow(state, installedVersion).State;
        }

        /// <summary>The state a plan reads as: a build with no profile has no plan and nothing to install.</summary>
        public static FlagBoxInstallState StateOf(FlagBoxPlan plan)
        {
            return plan == null ? FlagBoxInstallState.NotEmbedded : plan.State;
        }

        /// <summary>Whether the by-hand import is drawn under the title's line, which Matrix.dc.html does not draw:
        /// only when SimHub's matrix settings could not be reached, since the press there does the rest.</summary>
        public static bool ShowsImportFallback(FlagBoxInstallState state)
        {
            return state == FlagBoxInstallState.Unavailable;
        }

        /// <summary>
        /// Whether the page keeps the press's own result as the plan it draws, rather than asking SimHub again:
        /// only a failure, which asking again cannot see (the plan never reads Failed), so the title's line can
        /// say "Install failed" until the page is left or asked again.
        /// </summary>
        public static bool KeepsPressResult(FlagBoxInstallState result)
        {
            return result == FlagBoxInstallState.Failed;
        }

        /// <summary>Whether the line offers its press: never when there is no profile to install or nowhere
        /// to put it. The press is left out then rather than drawn disabled.</summary>
        public static bool ProfileHasButton(FlagBoxInstallState state)
        {
            return state != FlagBoxInstallState.NotEmbedded && state != FlagBoxInstallState.Unavailable;
        }

        /// <summary>
        /// The press's tooltip in the state it is drawn for: Install puts a profile beside the driver's own,
        /// and Update and Reinstall replace the copy in SimHub, which is the cost they carry. After a failed
        /// press the line says Install failed and offers Install, but SimHub may still hold the copy the
        /// press was made over, and pressing again removes it and adds the embedded one (FlagBoxInstaller),
        /// the driver's edits with it; so the failed state is warned as the state the press was made in
        /// (<paramref name="pressedFrom"/>, see <see cref="PressedFrom"/>) would be.
        /// </summary>
        public static string ProfileTooltip(FlagBoxInstallState state, FlagBoxInstallState pressedFrom)
        {
            var held = state == FlagBoxInstallState.Failed ? pressedFrom : state;
            return InSimHub(held) ? FlagBoxInstallPlan.Replaces : InstallTooltip;
        }

        /// <summary>The state a press is made in, for the tooltip after it fails: the line's own state, or,
        /// when the line already says Install failed, the state the failed press before it was made in.</summary>
        public static FlagBoxInstallState PressedFrom(FlagBoxInstallState state, FlagBoxInstallState failedFrom)
        {
            return state == FlagBoxInstallState.Failed ? failedFrom : state;
        }

        /// <summary>Install's hover, in the press's own verb (voice.md: "Removes this screen" beside "Remove this
        /// screen"), the profile by the page's own noun for it, as <see cref="NoProfile"/> and
        /// <see cref="RemoveCaption"/> name it; "Adds" is the add tile's verb on this page. One sentence, the
        /// press's action, in the LEDs page's form for its own Install press; the Replaces warning is Update's
        /// and Reinstall's.</summary>
        public const string InstallTooltip = "Installs the flag box profile in SimHub.";

        /// <summary>Whether SimHub holds a copy of the profile, which a press then replaces.</summary>
        private static bool InSimHub(FlagBoxInstallState state)
        {
            return state == FlagBoxInstallState.UpToDate || state == FlagBoxInstallState.Outdated;
        }

        /// <summary>
        /// What is said once the press has run, as one message in the order the driver acts on it, or null when the
        /// line above already says it all. The verb first, one per press (voice.md, "One word per thing", which
        /// holds verbs to it too): Installed, Updated over an older copy, Reinstalled over a current one, the name
        /// unquoted after it as on the other pages. Then the installer's note when it has one
        /// (FlagBoxInstallPlan.BuiltInModeNote), which has to be done before the profile can be selected at all,
        /// and makes the message caution rather than information, as the LEDs page says the same note. Then, after
        /// a first install, which adds the profile and does not select it, the select step in the form <see
        /// cref="StepsLeft"/> gives it (<see cref="SelectStep"/>): for the one matrix, for each of several, and
        /// none on a rig with none. A press that failed is said in its own verb, the one on the press the line drew
        /// (<paramref name="pressedIn"/>, the line's state): Could not update after Update, Could not reinstall
        /// after Reinstall, Could not install after Install, the Install offered after a failure included, as the
        /// LEDs and Updates pages word the same failure.
        /// </summary>
        public static PanelMessage InstallSaid(FlagBoxInstallState before, FlagBoxInstallState pressedIn, FlagBoxInstallState after, string profile, string note, IList<int> panels)
        {
            switch (after)
            {
                case FlagBoxInstallState.UpToDate:
                case FlagBoxInstallState.Outdated:
                    var verb = before == FlagBoxInstallState.Outdated ? "Updated "
                        : before == FlagBoxInstallState.UpToDate ? "Reinstalled " : "Installed ";
                    var parts = new List<string> { verb + profile + "." };
                    var noted = !string.IsNullOrWhiteSpace(note);
                    if (noted) parts.Add(note.Trim());
                    var first = before != FlagBoxInstallState.Outdated && before != FlagBoxInstallState.UpToDate;
                    var select = first ? SelectStep(profile, panels) : null;
                    if (select != null) parts.Add(select);
                    return new PanelMessage(string.Join(" ", parts), noted ? PanelTone.Caution : PanelTone.Info);
                case FlagBoxInstallState.Unavailable:
                    return PanelMessage.Caution(Unreachable + " Import " + profile + " by hand " + FromTheTop + ".");
                case FlagBoxInstallState.NotEmbedded:
                    return null;
                default:
                    return PanelMessage.Danger("Could not " + ProfileRow(pressedIn, null).Button.ToLowerInvariant() + " " + profile + ". " + SeeLog);
            }
        }

        /// <summary>
        /// The select step after a first install, in <see cref="StepsLeft"/>'s form, the profile quoted as the
        /// entry to pick in SimHub's list: on the one matrix's device with its content number; on each matrix's
        /// device when the rig has several, each with its own number; and nothing on a rig with none, which has
        /// no device to name. The Updates page says the same step in the same words (PanelUpdates.FlagBoxSelect
        /// on its branch).
        /// </summary>
        public static string SelectStep(string profile, IList<int> panels)
        {
            if (panels == null || panels.Count == 0) return null;
            if (panels.Count == 1) return SelectOn(profile) + YourDevice + ContentStep(panels[0]);
            return SelectOn(profile) + EachDevice + " and set " + ContentField + " to the matrix's number.";
        }

        /// <summary>The select step's opening, the profile quoted as the entry SimHub lists.</summary>
        private static string SelectOn(string profile)
        {
            return "Select \"" + profile + "\" on ";
        }

        /// <summary>Where the select step is taken when the rig has several matrices.</summary>
        public const string EachDevice = "each matrix's device in SimHub";

        /// <summary>SimHub's matrix settings out of reach, worded as the by-hand import under the title's line
        /// words it (FlagBoxInstallPlan.Summary), so the page says one thing about one state.</summary>
        public const string Unreachable = "SimHub's matrix settings are not available.";

        /// <summary>
        /// The hover on the title's line: in one sentence what the line cannot show, and nothing where the
        /// line, its press or the by-hand import under it already says it all (Not installed, whose press says
        /// what Install does; no profile in this build; SimHub's matrix settings out of reach). What a press
        /// replaces is the press's own tooltip. An older profile and a failed one are hovered in the Updates
        /// page's words for the same profile in the same state (PanelUpdates.UpdateBringsItTo and
        /// PanelUpdates.LightFailed). The other two states are hovered differently on the two pages, on
        /// purpose. A current profile's hover here is the select step, which an earlier review asked for, while
        /// the Updates row has none (PanelUpdates.FlagBoxTooltip: "a current one has none"). A missing profile
        /// has no hover here, since the press beside the line is Install, while the Updates row names the press
        /// (PanelUpdates.FlagBoxNotInstalled).
        /// </summary>
        /// <param name="profile">The profile as SimHub lists it, which the select step quotes.</param>
        /// <param name="panels">The rig's matrices: a current profile's hover is the select step, in the one
        /// form the page says it (<see cref="SelectStep"/>), and none on a rig with no matrix to name.</param>
        public static string ProfileLineTooltip(FlagBoxInstallState state, string embeddedVersion, string profile, IList<int> panels)
        {
            switch (state)
            {
                case FlagBoxInstallState.UpToDate:
                    // The dot and the version already say it is current: the hover gives the one step the
                    // line cannot show, which FlagBoxInstallPlan.Summary carried.
                    return SelectStep(profile, panels);
                case FlagBoxInstallState.Outdated:
                    // The line already shows the version SimHub has: the hover names the one the press brings.
                    return UpdateBringsItTo(embeddedVersion);
                case FlagBoxInstallState.Failed:
                    // The line already says the install failed: the hover says where to look.
                    return SeeLog;
                default:
                    return null;
            }
        }

        /// <summary>An older profile's hover, naming the version its Update brings: "Update brings it to
        /// 0.5.0.", and "Update brings it up to date." when this build does not say its version.</summary>
        public static string UpdateBringsItTo(string embeddedVersion)
        {
            return string.IsNullOrWhiteSpace(embeddedVersion)
                ? Update + " brings it up to date."
                : Update + " brings it to " + embeddedVersion.Trim() + ".";
        }

        /// <summary>A failed install's hover, and the close of the message after one: where to look.</summary>
        public const string SeeLog = "See SimHub's log.";

        public const double ProfileDotSize = 7;
        public const double ProfileGap = 10;

        /// <summary>The ghost press on the title's line is padded 6, not the kit's 10 (.btn.sm.ghost, padding 0 6).</summary>
        public const double ProfileButtonPaddingX = 6;

        // --- The cards ---------------------------------------------------------------------------------

        /// <summary>What the cards are, for a screen reader: the artboard's tablist. Never drawn, so search
        /// lists it as a keyword of "Add a matrix" rather than as a label.</summary>
        public const string PanelsTitle = "Your matrices";

        public const string AddPanel = "Add a matrix";
        public const string AddTooltip = "Adds a matrix.";
        public const string AllInUse = "All four matrices are in use.";

        /// <summary>The count after the add tile's words: "2 / 4".</summary>
        public static string AddCount(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " / " + MaxPanels.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Whether another can be added by count.</summary>
        public static bool CanAdd(int count)
        {
            return count < MaxPanels;
        }

        /// <summary>Whether the add tile is enabled: fewer than four, and one of SimHub's contents free.</summary>
        public static bool AddEnabled(int count, int freeSlot)
        {
            return CanAdd(count) && freeSlot != 0;
        }

        /// <summary>The artboard's three columns 12 apart, and the narrowest a card reads at: its border and
        /// 14 either side, the 55 px 8x8, 12, and "Not shown in SimHub" at 12 px (110.5 by the bundled Barlow),
        /// 207.5 in all, so three columns appear only where that line fits.</summary>
        public const double CardMinWidth = 208;
        public const double CardGap = 12;
        public const int CardColumns = 3;

        /// <summary>Which of SimHub's four contents a matrix is: the number a driver matches on the device.</summary>
        public static string Slot(int matrix)
        {
            return "Matrix " + matrix.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The name a matrix is added with, and the one it is called by when it has none.</summary>
        public static string DefaultName(int matrix)
        {
            return Slot(matrix);
        }

        public static string NameOf(string name, int matrix)
        {
            return string.IsNullOrWhiteSpace(name) ? DefaultName(matrix) : name;
        }

        /// <summary>
        /// Where a matrix's own settings sit in each per-matrix array (FlagBoxFlags and the rest): matrix n at
        /// n - 1, the entry Settings' readers (MatrixFlags and the rest, through Pick) take it from. Every
        /// switch on the page writes through this, so a write can never land on another matrix's entry.
        /// </summary>
        public static int Index(int matrix)
        {
            return matrix - 1;
        }

        /// <summary>The selection a card keeps for the session (SettingsControl.Select), as text.</summary>
        public static string SlotId(int matrix)
        {
            return matrix.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The matrix the page opens on: the one selected when the rig still has it, else the
        /// first, else none (0).</summary>
        public static int SelectedSlot(IList<int> panels, string selected)
        {
            if (panels == null || panels.Count == 0) return 0;
            int slot;
            if (selected != null && int.TryParse(selected, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) && panels.Contains(slot)) return slot;
            return panels[0];
        }

        /// <summary>
        /// Mounting side's choices in the artboard's order, Left | Right | Both sides, which is also the order
        /// of the Rig page's spotter chips (Car left, Car right, Both sides); <see cref="Contract.FlagBoxSides"/>
        /// lists the same values with "both" first, which is the stored default's order and not the driver's.
        /// </summary>
        public static readonly string[] SideValues = { "left", "right", "both" };

        /// <summary>One label per <see cref="SideValues"/> value, in its order: the artboard's "Both sides" for
        /// the one that is not a side.</summary>
        public static readonly string[] SideLabels = { "Left", "Right", "Both sides" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        /// <summary>A side's label, and the default side's for a value the contract does not have.</summary>
        public static string SideLabel(string side)
        {
            var i = Array.IndexOf(SideValues, side);
            if (i < 0) i = Array.IndexOf(SideValues, Contract.DefaultFlagBoxSide);
            return SideLabels[i];
        }

        public const string Showing = "Showing";
        public const string NotShown = "Not shown in SimHub";

        /// <summary>
        /// The line under a card's name. Whether a device shows the matrix is not on SimHub's public
        /// surface today (MatrixFacts' Shown is null), and a card never claims what nothing read: it then
        /// says which content it is and which side it is mounted on, written as its control names it. The
        /// content number is left out when the name above already is it, as <see cref="SlotCaption"/> does.
        /// </summary>
        public static string CardLine(string name, int matrix, string side, bool? shown)
        {
            if (shown == true) return Showing;
            if (shown == false) return NotShown;
            var slot = SlotCaption(name, matrix);
            return slot == null ? SideLabel(side) : slot + " · " + SideLabel(side);
        }

        /// <summary>
        /// A card's name for a screen reader: its name and the line under it, both of which the card draws.
        /// The name replaces the card's content, so the line would go unheard without it. The line's middle dot
        /// is spoken as a comma: "Left pillar, Matrix 2, Left".
        /// </summary>
        public static string CardName(string name, int matrix, string side, bool? shown)
        {
            if (shown == true) return name + ", " + Showing;
            if (shown == false) return name + ", " + NotShown;
            var slot = SlotCaption(name, matrix);
            return name + ", " + (slot == null ? SideLabel(side) : slot + ", " + SideLabel(side));
        }

        public static string CardLineHex(bool? shown)
        {
            return shown == false ? Theme.Caution : Theme.TextSecondary;
        }

        // --- The selected matrix -------------------------------------------------------------------------

        /// <summary>The line beside the selected matrix's name: its content number, unless the name is it.</summary>
        public static string SlotCaption(string name, int matrix)
        {
            var slot = Slot(matrix);
            return string.Equals(name, slot, StringComparison.Ordinal) ? null : slot;
        }

        public const string Rename = "Rename";
        public const string RenameTooltip = "Renames this matrix.";
        public const string Remove = "Remove";
        /// <summary>Remove's hover: what goes with the matrix, as <see cref="RemoveCaption"/> says it in the sheet
        /// and as the Screens and LEDs pages' Remove hovers name what goes with theirs.</summary>
        public const string RemoveTooltip = "Removes this matrix and its settings.";

        public static string RenameTitle(string name)
        {
            return "Rename " + name;
        }

        public static string RemoveTitle(string name)
        {
            return "Remove " + name;
        }

        /// <summary>What the one question before a removal says: the consequence, not the reason, in the shape
        /// Screens and LEDs give theirs. It names the matrix, since the sheet's title trims a long name and the
        /// body wraps: the Danger press is never left under a sheet that does not say what it removes.</summary>
        public static string RemoveCaption(string name)
        {
            return "Removes " + name + " and its settings. The flag box profile stays in SimHub.";
        }

        /// <summary>The sheet's Danger press, the verb Screens and LEDs confirm with.</summary>
        public const string RemoveConfirm = "Remove it";

        public static string Renamed(string name)
        {
            return "Renamed to " + name + ".";
        }

        /// <summary>Whether the Rename press does anything: a blank name is ignored by the settings.</summary>
        public static bool CanRename(string typed)
        {
            return !string.IsNullOrWhiteSpace(typed);
        }

        /// <summary>What is said after the Rename press, or null when the stored name did not change.</summary>
        public static string RenameSaid(string before, string after)
        {
            return string.Equals(before, after, StringComparison.Ordinal) ? null : Renamed(after);
        }

        /// <summary>What is said after a removal: the step it leaves, which is a device that now shows nothing.</summary>
        public static string Removed(string name, int matrix)
        {
            return "Removed " + name + ". A device with " + ContentField + " set to " + matrix.ToString(CultureInfo.InvariantCulture) + " stays dark.";
        }

        /// <summary>
        /// The device's field that picks which matrix it draws, spelt as SimHub labels it: the
        /// localisation key ArduinoHardwareSettings_Label_RGBMatrixContent in SimHub.Plugins.dll and the same
        /// string in SimHubWPF.exe read "RGB Matrix content", and docs/flag-box.md tells drivers to set it by
        /// that name. Matrix.dc.html's "Matrix content: 2" names a field SimHub does not have.
        /// </summary>
        public const string ContentField = "RGB Matrix content";

        public const string NameTitle = "Name";

        /// <summary>
        /// The line under the name box, which says what the name is not: one profile paints every matrix, so
        /// SimHub's matrix profile list carries the profile's name and never this one. One sentence, in the form
        /// the Screens and LEDs sheets give the same row ("Also shown in SimHub's LED profile list."), the list
        /// named as docs/flag-box.md and FlagBoxInstaller's log name it.
        /// </summary>
        public const string NameCaption = "Not shown in SimHub's matrix profile list.";

        public const string Cancel = "Cancel";

        /// <summary>The gap between a sheet's caption and its name row, and under the empty state.</summary>
        public const double SheetGap = 12;
        public const double EmptyGap = 12;

        /// <summary>The name box's width in the sheets.</summary>
        public const double NameWidth = 280;

        /// <summary>What the add sheet says above the name: the steps left, as <see cref="PanelAdded"/> says them
        /// once it exists. The content number is in them, and the name box under it opens on it, so the
        /// caption does not say it a third time.</summary>
        public static string AddPanelCaption(int matrix, string profile, FlagBoxInstallState state)
        {
            return StepsLeft(matrix, profile, state);
        }

        /// <summary>
        /// What is said once a matrix exists: the step SimHub does not take, in the order it is taken. When
        /// SimHub has no copy of the profile, the sentence sends the driver to the title's line, which can
        /// install it, or to the by-hand import under it; and when this build has none, it says so.
        /// </summary>
        public static string PanelAdded(string name, int matrix, string profile, FlagBoxInstallState state)
        {
            return "Added " + name + ". " + StepsLeft(matrix, profile, state);
        }

        /// <summary>
        /// The steps between a new matrix and a lit one, in the order they are taken, in one form wherever the
        /// page says them: the profile first when SimHub has no copy, then the select step on the device. The
        /// profile is quoted only inside the select step, where it is the entry to pick in SimHub's list.
        /// </summary>
        private static string StepsLeft(int matrix, string profile, FlagBoxInstallState state)
        {
            var content = ContentStep(matrix);
            switch (state)
            {
                case FlagBoxInstallState.UpToDate:
                case FlagBoxInstallState.Outdated:
                    return SelectOn(profile) + YourDevice + content;
                case FlagBoxInstallState.NotEmbedded:
                    return NoProfile;
                case FlagBoxInstallState.Unavailable:
                    return "Import " + profile + " by hand " + FromTheTop + ", then select it on " + YourDevice + content;
                default:
                    return "Install " + profile + " at the top of this page, then select it on " + YourDevice + content;
            }
        }

        /// <summary>The content step that closes every select step: " and set RGB Matrix content to 2."</summary>
        private static string ContentStep(int matrix)
        {
            return " and set " + ContentField + " to " + matrix.ToString(CultureInfo.InvariantCulture) + ".";
        }

        /// <summary>Where the by-hand import is, in the one form the page says it.</summary>
        private const string FromTheTop = "from the top of this page";

        /// <summary>Where the select step is taken, in the one form the page says it.</summary>
        public const string YourDevice = "your matrix's device in SimHub";

        /// <summary>Whether <see cref="PanelAdded"/> asks for something before the matrix will light, which
        /// is what decides the ink it is said in.</summary>
        public static bool NeedsInstall(FlagBoxInstallState state)
        {
            return state != FlagBoxInstallState.UpToDate && state != FlagBoxInstallState.Outdated;
        }

        /// <summary>The fix box's title, drawn only when something has read that no device shows the matrix: the
        /// card's state, one phrase for the one state on the card, on Home and here (#524, ruling 8). The matrix
        /// number is in the steps under it (<see cref="FixSteps"/>'s Content line).</summary>
        public const string FixTitle = NotShown;

        /// <summary>SimHub names its devices itself, so the crumb names the one a driver is to open.</summary>
        public const string YourMatrixCrumb = "your matrix";

        /// <summary>The fix box's steps: the device in SimHub, the profile to select on it, the content to set.</summary>
        public static IList<string[]> FixSteps(int matrix, string profile)
        {
            return new List<string[]>
            {
                new[] { PanelAttention.DevicesCrumb, YourMatrixCrumb },
                new[] { "Profile: " + profile },
                new[] { ContentField + ": " + matrix.ToString(CultureInfo.InvariantCulture) },
            };
        }

        // The selected matrix's section: a rule over it, 20 under the rule and 18 between its parts; the name
        // at 22 with its content number 12 after it, and Rename and Remove 6 apart.
        public const double SectionPaddingTop = 20;
        public const double SectionGap = 18;
        public const double HeaderGap = 12;
        public const double ActionGap = 6;

        /// <summary>How far above the name's foot the content number sits: 22 over 13 puts the smaller
        /// line's baseline on the name's about 4 up.</summary>
        public const double SlotCaptionLift = 4;

        // --- The preview -----------------------------------------------------------------------------

        /// <summary>
        /// The scenario the idle display is drawn in, on the cards and under the preview's first chip: the revs
        /// at idle, which is the box at rest -- the gear in its one white, whatever Shift colours says, as Home
        /// draws the matrix and as the Rig page's "Idle" chip does, so one picture has one name.
        /// </summary>
        public const string IdleScenario = PanelEmulation.Idle;

        /// <summary>
        /// The chip that draws the gear at revs, the Rig page's "Mid revs": where Shift colours shows, green
        /// with it on and white with it off. Matrix.dc.html shows the switch on its "At rest" chip instead,
        /// but the box at rest draws the gear white whatever the switch says (gear.ts), so the idle display
        /// stays the box and this chip is where the switch can be seen.
        /// </summary>
        public const string RevsScenario = PanelEmulation.Mid;

        /// <summary>The preview's chips, in the artboard's order, with the revs chip after the idle display.</summary>
        public static readonly string[] PreviewScenarios =
        {
            IdleScenario, RevsScenario, PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.Limiter,
            PanelEmulation.CarLeft, PanelEmulation.LowFuel, PanelEmulation.Chequer,
        };

        /// <summary>
        /// What the preview draws, for a screen reader (the artboard's role=img alt text): the glyph the matrix
        /// shows under the chip drawn (<see cref="DrawnScenario"/>), in the options it is drawn with
        /// (<see cref="OptionsFor"/> with that chip), so the name is always the picture's. The gear in its first
        /// shift colour is named for that colour, which is the one picture that tells it from the idle display:
        /// under the revs chip with Shift colours on, and under any chip the matrix does not show, since a gear
        /// matrix that nothing takes over draws the gear at that chip's revs (PanelEmulation's rest glyph), as
        /// the Rig page draws it.
        /// </summary>
        public static string PreviewAlt(string drawn, MatrixOptions options)
        {
            var glyph = PanelEmulation.GlyphFor(drawn, options);
            if (glyph == null) return PreviewDark;
            if (glyph == RestGearGlyph) return PreviewGear;
            if (glyph == RevsGearGlyph) return PreviewRevs;
            switch (drawn)
            {
                case PanelEmulation.Yellow: return "Yellow flag";
                case PanelEmulation.Blue: return "Blue flag";
                case PanelEmulation.Limiter: return "Pit limiter frame";
                case PanelEmulation.CarLeft: return "Car on the left";
                case PanelEmulation.LowFuel: return "Fuel pump";
                case PanelEmulation.Chequer: return "Chequered flag";
            }
            return PreviewLabel(drawn);
        }

        /// <summary>The glyphs the gear is drawn in at rest, and at revs with Shift colours on, as
        /// PanelEmulation names them from flag-box-glyphs.json.</summary>
        private const string RestGearGlyph = "Gear " + PanelEmulation.Gear + " rest";
        private const string RevsGearGlyph = "Gear " + PanelEmulation.Gear + " stage1";

        /// <summary>The preview's alt text when the gear is drawn in its one white.</summary>
        public const string PreviewGear = "Gear " + PanelEmulation.Gear;

        /// <summary>The preview's alt text when the idle display rests dark.</summary>
        public const string PreviewDark = "Dark";

        /// <summary>The preview's alt text under the revs chip with Shift colours on.</summary>
        public const string PreviewRevs = "Gear " + PanelEmulation.Gear + " in the first shift colour";

        /// <summary>A chip's label: the Rig page's word for the scenario, and the idle display by its row's name.</summary>
        public static string PreviewLabel(string scenarioId)
        {
            if (scenarioId == IdleScenario) return IdleDisplayTitle;
            var scenario = PanelEmulation.Find(scenarioId);
            return scenario == null ? IdleDisplayTitle : scenario.Label;
        }

        /// <summary>The chip the preview draws: the one held for the session when it is one of the chips.</summary>
        public static string PreviewScenario(string held)
        {
            return held != null && Array.IndexOf(PreviewScenarios, held) >= 0 ? held : IdleScenario;
        }

        /// <summary>
        /// The chips a matrix offers: every one, less the revs chip while the idle display rests dark. The revs
        /// change only the gear, so on a dark matrix that chip would press and draw the same dark frame, named
        /// the same, as the idle display's; Shift colours, which it is there to show, is hidden then too. Every
        /// chip when the settings are not known.
        /// </summary>
        public static IList<string> PreviewChips(MatrixOptions options)
        {
            var gear = options == null || ShowsGearRows(options.Rest);
            return PreviewScenarios.Where(id => gear || id != RevsScenario).ToList();
        }

        /// <summary>The chip the preview draws for a matrix: the one held for the session when that matrix
        /// offers it (<see cref="PreviewChips"/>), else the idle display. A held revs chip comes back when the
        /// idle display rests on the gear again.</summary>
        public static string PreviewScenario(string held, MatrixOptions options)
        {
            return held != null && PreviewChips(options).Contains(held) ? held : IdleScenario;
        }

        /// <summary>
        /// A matrix's settings as its picture reads them under a chip: the Rig page's own
        /// (PanelRigMap.MatrixOptionsFor), called rather than copied, so the preview and the matrix's tile on
        /// the Rig page, which "All devices at once" opens on the same chip, draw one matrix under one chip
        /// alike. A family switched off, or a car on the side the matrix is not mounted on, leaves the matrix
        /// at its idle display (PanelEmulation.GlyphFor); with Critical flags only on, the flags are off for a
        /// flag that is news rather than a warning (the chequer, the white and the green: flags.ts's
        /// "critical"). Without a chip, the settings alone.
        /// </summary>
        public static MatrixOptions OptionsFor(OpenDashSettings settings, int matrix, string scenario = null)
        {
            return PanelRigMap.MatrixOptionsFor(settings, matrix, scenario);
        }

        /// <summary>
        /// The scenario a chip is drawn in: the chip's own, as the Rig page draws it, so a chip the matrix
        /// would not show draws what the matrix shows then -- on a matrix that rests on the gear, the gear at
        /// that chip's revs (PanelEmulation.MatrixFrame) rather than the idle display's white gear. The idle
        /// display alone is drawn as the idle scenario, and so is the revs chip on a matrix that rests dark,
        /// whose revs change nothing it shows.
        /// </summary>
        public static string DrawnScenario(string scenario, MatrixOptions options)
        {
            if (scenario == null || scenario == IdleScenario) return IdleScenario;
            if (scenario == RevsScenario && (options == null || !ShowsGearRows(options.Rest))) return IdleScenario;
            return scenario;
        }

        /// <summary>What the chips are, for a screen reader: a noun, as voice.md's "The caption" says a heading
        /// is, the artboard's own from its clause "What to preview", and one noun for the one set of chips on
        /// every page (#524, ruling 6). Not "scenarios", which is the code's word for PanelEmulation's chips and
        /// no page shows a driver.</summary>
        public const string PreviewChipsName = PanelRigMap.ScenariosName;

        /// <summary>The link under the preview, to the Rig page on the same scenario.</summary>
        public const string AllDevices = "All devices at once";

        // The preview column: 300 wide and 36 from the priority list, its 8x8 in a frame padded 20 on the
        // inset ground, 14 between the frame, the link and the chips, and the chips 6 apart.
        public const double PreviewColumnWidth = 300;
        public const double BodyGap = 36;
        public const double PreviewFramePadding = 20;
        public const double PreviewGap = 14;
        public const double ChipGap = 6;

        /// <summary>The gap between the preview and the priority list when they are stacked: the page's own.</summary>
        public const double StackedGap = 22;

        /// <summary>The gap under the New tag, which sits on its own line over the preview's frame (Map.dc.html
        /// marks the Live 8×8 preview itself new): the frame's padding is narrower than the tag is tall, so it
        /// cannot sit inside the frame clear of the lamps.</summary>
        public const double NewTagGap = 8;

        // --- What may take the matrix over -------------------------------------------------------------

        public const string PriorityTitle = "Priority";

        /// <summary>The greyed caption over the list, until the order can be changed (#505).</summary>
        public const string DragToReorder = "Drag to reorder";
        public const double ReorderGap = 8;

        public const string FlagsTitle = "Flags";
        public const string PitLaneTitle = "Pit lane";
        public const string SpotterTitle = "Spotter";
        public const string WarningsTitle = "Warnings";

        /// <summary>
        /// What may take the matrix over, first first, as Matrix.dc.html numbers it, fixed until #505 lets a
        /// driver change it. This is not quite the box's own order: flag-box.md and profile.ts (belowFlags)
        /// paint the spotter as an overlay over everything, a standing yellow included, and rank the rest
        /// flags, pit lane, warnings, gear. The numbers are the artboard's; the disagreement is the author's
        /// to settle, not this page's.
        /// </summary>
        public static readonly string[] Layers = { FlagsTitle, PitLaneTitle, SpotterTitle, WarningsTitle };

        /// <summary>A layer's number in the list, 1 for the first.</summary>
        public static string Rank(string layer)
        {
            var i = Array.IndexOf(Layers, layer);
            return i < 0 ? string.Empty : (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        public const string WarningsCaption = "Low fuel, oil temperature and water temperature.";

        public const string CriticalFlagsOnlyTitle = "Critical flags only";
        public const string MountingSideTitle = "Mounting side";

        /// <summary>The spotter's slide, which every matrix shares: the caption says so, since the row sits
        /// among one matrix's own.</summary>
        public const string SpotterAnimationTitle = "Spotter bar animation";
        public const string SpotterAnimationCaption = "Every matrix.";

        /// <summary>The link on the Warnings row: the triggers are the rig's, on Settings' Alerts, named by the
        /// plural of the column they are in there (#524, ruling 7).</summary>
        public const string ThresholdsLink = PanelSettings.ThresholdColumn + "s";
        public static readonly PanelRoute ThresholdsRoute = new PanelRoute(PanelPage.Settings, PanelSettings.AnchorAlerts);

        public const string IdleDisplayTitle = "Idle display";
        public const string ShiftColoursTitle = "Shift colours";
        public const string RedlineFlashTitle = "Redline flash";

        /// <summary>The digit's colours following the car's own shift lights: "shift points", as plugin.md's
        /// departures table records the row, since "thresholds" is the settings model's word and not the driver's;
        /// and "Car-specific", the name voice.md gives the car's own tables, which is also what Lovely Sim Racing
        /// calls the tables behind them, so a driver who met them there recognises the word.</summary>
        public const string CarShiftPointsTitle = "Car-specific shift points";

        /// <summary>The rows under Idle display appear only when the gear is what it shows.</summary>
        public static bool ShowsGearRows(string rest)
        {
            return string.Equals(rest, "gear", StringComparison.Ordinal);
        }

        /// <summary>The car's own shift points colour the gear, so the row is there only with Shift colours on.</summary>
        public static bool ShowsCarShiftPoints(string rest, bool bands)
        {
            return ShowsGearRows(rest) && bands;
        }

        /// <summary>The flash is the last shift colour, the car's own included, so it shows whenever the
        /// gear changes colour at all.</summary>
        public static bool ShowsRedlineFlash(string rest, bool bands)
        {
            return ShowsGearRows(rest) && bands;
        }

        /// <summary>
        /// The line under Car-specific shift points, which is the LEDs page's line under its car switch
        /// (PanelLeds.CarLine), called rather than copied, and so in the same cases: only while the switch is on and a
        /// car is loaded in a game the tables cover. Then, with no tables ever downloaded, that they are not
        /// and where the download is, since this page has none; else whether the tables have measured the
        /// car, which says whether the gear takes the car's own points or falls back to openDash's. Null
        /// otherwise, since then there is nothing true to say: the plugin reads iRacing's tables alone
        /// (CarLightLibrary), so in any other game every car would read as missing from a dataset that may well
        /// measure it, and a download sent for from there could never apply. Null too while tables that are on
        /// disk are still being read, or when the copy on disk could not be read: the LEDs page's Lovely Car
        /// Data row then says "Loading" or that it could not be read, and a line saying they were never downloaded would
        /// contradict it, and be false.
        /// </summary>
        /// <param name="tablesMissing">Whether no tables were ever downloaded (<see cref="TablesMissing"/>).</param>
        /// <remarks>Only the missing-tables sentence is this page's own, since it names the page the download is
        /// on, which the LEDs page's need not.</remarks>
        public static string CarLine(bool on, string car, bool hasTable, bool tablesLoaded, bool tablesMissing, bool tablesCoverGame)
        {
            var line = PanelLeds.CarLine(on, car, hasTable, tablesLoaded, tablesMissing, tablesCoverGame);
            return line == PanelLeds.CarTablesMissing ? CarTablesMissing : line;
        }

        /// <summary>The car line with no tables on disk: the tables are downloaded on the LEDs page only
        /// (ADR 0018), under the section every strip shares, named here by that page's own constants.</summary>
        public const string CarTablesMissing = "Lovely Car Data is not downloaded yet. Download it on the "
            + PanelLeds.Title + " page, under " + PanelLeds.EveryStripTitle + ".";

        /// <summary>Whether no tables were ever downloaded: none read, and the service's status opens with the
        /// sentence it gives a rig with none (a failed download adds its reason after it), rather than saying
        /// that it is still reading them or could not read the copy on disk: the LEDs page's own test
        /// (PanelLeds.TablesMissing), called rather than copied.</summary>
        public static bool TablesMissing(int carCount, string status)
        {
            return PanelLeds.TablesMissing(carCount, status);
        }

        /// <summary>The game whose tables the plugin reads, as SimHub names it or codes it ("iRacing",
        /// "IRacing"): the LEDs page's (PanelLeds.TablesGame).</summary>
        public const string TablesGame = PanelLeds.TablesGame;

        /// <summary>Whether the tables cover the game SimHub is set to: iRacing's alone, the LEDs page's test
        /// (PanelLeds.TablesCoverGame).</summary>
        public static bool TablesCoverGame(string game)
        {
            return PanelLeds.TablesCoverGame(game);
        }

        /// <summary>Whether the car line is the good news, in green, rather than something to act on, in
        /// caution: only a car the downloaded tables have measured.</summary>
        public static bool CarLineGood(bool hasTable, bool tablesLoaded)
        {
            return tablesLoaded && hasTable;
        }

        /// <summary>The car line's ink.</summary>
        public static string CarLineHex(bool good)
        {
            return good ? Theme.StatusUpToDate : Theme.Caution;
        }

        /// <summary>The greyed press on the SimHub device row (#363).</summary>
        public const string SimHubDeviceButton = "Choose a device";

        // The list, as Matrix.dc.html draws it: the heading 8 over the first rule; each layer a rule over a
        // 12-padded line of its number (16 in the display family, 14 wide), 12, its name at 15 and its switch;
        // each option under it padded 7, at 14, its control 16 from its words; a line under an option at 12,
        // 2 below; the Thresholds link 8 before the switch. The artboard indents the options 46 because its
        // lines start with a 10 px grip and 12; the build draws no grip until #505 makes the order draggable, so
        // the options and the rank-less SimHub device row start where a layer's name now does, the rank's 14 and 12
        // in.
        public const double PriorityHeadGap = 8;
        public const double LayerPaddingY = 12;
        public const double LayerGap = 12;
        public const double RankWidth = 14;
        public const double RankSize = 16;
        public const double OptionIndent = RankWidth + LayerGap;

        /// <summary>The dot in the rank column of the unranked Idle display, the artboard's '·'.</summary>
        public const double UnrankedDotSize = 4;
        public const double OptionPaddingY = 7;
        public const double OptionTextSize = 14;
        public const double OptionGap = 16;
        public const double OptionLineSize = 12;
        public const double OptionLineGap = 2;
        public const double ThresholdsGap = 8;

        // --- Search and anchors ------------------------------------------------------------------------

        public const string AnchorProfile = "matrix.profile";
        public const string AnchorPanels = "matrix.panels";
        public const string AnchorPreview = "matrix.preview";
        public const string AnchorPriority = "matrix.priority";
        public const string AnchorFlags = "matrix.flags";
        public const string AnchorPitLane = "matrix.pit-lane";
        public const string AnchorSpotter = "matrix.spotter";
        public const string AnchorSpotterAnimation = "matrix.spotter-animation";
        public const string AnchorWarnings = "matrix.warnings";
        public const string AnchorIdleDisplay = "matrix.idle-display";

        /// <summary>Whether search lists a row of this page on a rig of <paramref name="matrices"/> matrices: the
        /// profile's line and the cards on any rig, every other row only where there is a matrix to select, since
        /// the page draws them for the selected one.</summary>
        public static bool SearchDrawn(string anchor, int matrices)
        {
            return anchor == null || anchor == AnchorProfile || anchor == AnchorPanels || matrices > 0;
        }

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(FlagBoxProfile.ProfileName, PanelPage.Matrix, AnchorProfile, "flag box profile", "install", "reinstall", "matrix profile", "8x8"),
            new PanelSearch.Entry(AddPanel, PanelPage.Matrix, AnchorPanels, "your matrices", "new", "8x8", "flag box", "pillar", "panel", "panels"),
            new PanelSearch.Entry(PriorityTitle, PanelPage.Matrix, AnchorPriority, "order", "layers", "takes over"),
            new PanelSearch.Entry(FlagsTitle, PanelPage.Matrix, AnchorFlags, "race flags", "yellow", "blue"),
            new PanelSearch.Entry(CriticalFlagsOnlyTitle, PanelPage.Matrix, AnchorFlags, "chequer", "white", "green"),
            new PanelSearch.Entry(PitLaneTitle, PanelPage.Matrix, AnchorPitLane, "limiter", "speeding", "pit status"),
            new PanelSearch.Entry(SpotterTitle, PanelPage.Matrix, AnchorSpotter, "cars alongside"),
            new PanelSearch.Entry(MountingSideTitle, PanelPage.Matrix, AnchorSpotter, "left", "right", "both"),
            new PanelSearch.Entry(SpotterAnimationTitle, PanelPage.Matrix, AnchorSpotterAnimation, "slide in"),
            new PanelSearch.Entry(WarningsTitle, PanelPage.Matrix, AnchorWarnings, "fuel", "oil", "water", "car warnings"),
            new PanelSearch.Entry(IdleDisplayTitle, PanelPage.Matrix, AnchorIdleDisplay, "at rest", "gear", "dark"),
            new PanelSearch.Entry(ShiftColoursTitle, PanelPage.Matrix, AnchorIdleDisplay, "gear", "revs"),
            new PanelSearch.Entry(CarShiftPointsTitle, PanelPage.Matrix, AnchorIdleDisplay, "lovely", "car data", "car-specific", "thresholds"),
            new PanelSearch.Entry(RedlineFlashTitle, PanelPage.Matrix, AnchorIdleDisplay, "gear", "shift"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = { PanelSoon.PriorityOrder, PanelSoon.RpmColourForEverything, PanelSoon.SimHubDevice };

        /// <summary>
        /// Search labels this page draws through something other than the constant, each with the text its
        /// sources draw it by, so the list is a record rather than a way round PanelSearchTests.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // The profile's line names it as SimHub lists it, FlagBoxName(), whose fallback is this.
            { FlagBoxProfile.ProfileName, "FlagBoxName()" },
        };
    }
}
