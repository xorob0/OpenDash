// PanelUpdates.cs: the Updates page's words and decisions -- the update card and its states, the check row,
// the "In SimHub" table, Reinstall everything, the kept copy, and the support report.
//
// Drawn from Updates.dc.html (#503). Every string and every choice the page makes is here, where
// PanelUpdatesTests can pin it; SettingsControl.Updates*.cs only draws. The update sentences that were already
// pinned stay in UpdateWording, and the light rows' own copy in PanelLightRows and PanelCopy.LightRow.
// Pure: no WPF, no SimHub.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>What the update card at the top of the page is showing.</summary>
    public enum UpdatesCard
    {
        /// <summary>Nothing is offered: the card is gone and the check row carries the line.</summary>
        None,

        /// <summary>A newer release is offered, with Download and its notes.</summary>
        Available,

        /// <summary>The release is downloading: its line and a bar.</summary>
        Downloading,

        /// <summary>The plugin is staged and waits for SimHub to restart.</summary>
        Staged,
    }

    /// <summary>One row of the "In SimHub" table: what it is, at which version, and in what state.</summary>
    public sealed class UpdatesRow
    {
        public UpdatesRow(string name, string kind, string version, string state, string stateHex, string dotHex, string tooltip, bool offersUpdate)
        {
            Name = name ?? string.Empty;
            Kind = kind;
            Version = version ?? string.Empty;
            State = state;
            StateHex = stateHex;
            DotHex = dotHex;
            Tooltip = tooltip;
            OffersUpdate = offersUpdate;
        }

        /// <summary>The name SimHub lists it under.</summary>
        public string Name { get; private set; }

        /// <summary>What it is, written after the name: "dashboard", "LED profile", "matrix profile".</summary>
        public string Kind { get; private set; }

        /// <summary>The version SimHub holds, or empty when there is none to show.</summary>
        public string Version { get; private set; }

        public string State { get; private set; }

        /// <summary>The ink the state is written in.</summary>
        public string StateHex { get; private set; }

        /// <summary>The dot beside the state, which differs from the ink only where nothing is installed.</summary>
        public string DotHex { get; private set; }

        public string Tooltip { get; private set; }

        /// <summary>Whether the row carries an Update press: a light profile older than this build's.</summary>
        public bool OffersUpdate { get; private set; }
    }

    /// <summary>One line of the support report's lists: a thing on the rig, what it is, and its state.</summary>
    public sealed class UpdatesReportItem
    {
        public UpdatesReportItem(string name, string detail, string version, string state)
        {
            Name = name;
            Detail = detail;
            Version = version;
            State = state;
        }

        public string Name { get; private set; }
        public string Detail { get; private set; }
        public string Version { get; private set; }
        public string State { get; private set; }
    }

    /// <summary>What the support report is made of, gathered by the page at the press.</summary>
    public sealed class UpdatesReportInput
    {
        public DateTime GeneratedUtc { get; set; }
        public string PluginVersion { get; set; }
        public string RigVersion { get; set; }
        public string SimHubVersion { get; set; }
        public string SimHubRoot { get; set; }
        public string OsVersion { get; set; }
        public bool ChecksOn { get; set; }
        public long LastCheckedTicks { get; set; }
        public string UpdateLine { get; set; }
        public bool RestartPending { get; set; }
        public IList<UpdatesReportItem> Screens { get; set; }
        public IList<UpdatesReportItem> Strips { get; set; }
        public IList<UpdatesReportItem> Matrices { get; set; }

        /// <summary>The flag box profile's line, or null when there is no matrix to need it.</summary>
        public UpdatesReportItem FlagBox { get; set; }

        public string CarTables { get; set; }

        /// <summary>SimHub's log lines that are OpenDash's, the last <see cref="PanelUpdates.LogLines"/>.</summary>
        public IList<string> Log { get; set; }
    }

    /// <summary>
    /// What Reinstall everything did to the light profiles, counted per strip: a strip rewritten and a strip
    /// that could not be are two strips, whatever shape they share.
    /// </summary>
    public sealed class UpdatesLightsTally
    {
        /// <summary>Older strip profiles brought to this build's version.</summary>
        public int StripsUpdated { get; private set; }

        /// <summary>Missing strip profiles installed.</summary>
        public int StripsInstalled { get; private set; }

        public int StripsNotUpdated { get; private set; }
        public int StripsNotInstalled { get; private set; }

        /// <summary>What the flag box profile was left in, or null when the run did not write it.</summary>
        public FlagBoxInstallState? FlagBoxAfter { get; private set; }

        /// <summary>Whether the flag box profile the run wrote was an older one rather than a missing one.</summary>
        public bool FlagBoxWasOlder { get; private set; }

        /// <summary>One strip the run wrote: what SimHub held before, and what the write reported.</summary>
        public void Strip(FlagBoxInstallState before, FlagBoxInstallState after)
        {
            var older = before == FlagBoxInstallState.Outdated;
            if (after == FlagBoxInstallState.UpToDate)
            {
                if (older) StripsUpdated++;
                else StripsInstalled++;
            }
            else if (older) StripsNotUpdated++;
            else StripsNotInstalled++;
        }

        public void FlagBox(FlagBoxInstallState before, FlagBoxInstallState after)
        {
            FlagBoxWasOlder = before == FlagBoxInstallState.Outdated;
            FlagBoxAfter = after;
        }

        /// <summary>Whether that run is said in the ordinary ink: nothing it tried failed.</summary>
        public bool Ok
        {
            get
            {
                return StripsNotUpdated == 0 && StripsNotInstalled == 0
                    && (!FlagBoxAfter.HasValue || FlagBoxAfter == FlagBoxInstallState.UpToDate);
            }
        }
    }

    public static class PanelUpdates
    {
        public const string Title = "Updates";

        // --- The update card ------------------------------------------------------------------------

        /// <summary>The card's heading, a noun (voice.md): "OpenDash 0.5.1", or the name alone when the
        /// release is not known.</summary>
        public static string Heading(string version)
        {
            return string.IsNullOrWhiteSpace(version) ? "OpenDash" : "OpenDash " + version.Trim();
        }

        /// <summary>Beside the heading, before the version the rig runs, which is drawn as a numeral.</summary>
        public const string YouHave = "You have";

        /// <summary>Under the heading of an offer. OpenDash has no presets, and the step is what is said.</summary>
        public const string RestartNote = "Restart SimHub to finish updating. Your settings are kept.";

        public const string ReleaseNotesTitle = "Release notes";
        public const string EveryRelease = "Every release on GitHub";

        /// <summary>The line over the bar while a release downloads.</summary>
        public static string Downloading(string version)
        {
            return "Downloading " + (string.IsNullOrWhiteSpace(version) ? "the update" : version.Trim()) + "…";
        }

        /// <summary>
        /// What the card shows. A run in progress outranks everything; a staged plugin outranks the offer
        /// that staged it, since pressing Download again would fetch the same release rather than finish
        /// anything; otherwise the card is there only while a release is offered.
        /// </summary>
        public static UpdatesCard CardFor(UpdateState state, bool applying, bool pending)
        {
            if (applying) return UpdatesCard.Downloading;
            if (pending) return UpdatesCard.Staged;
            return state == UpdateState.UpdateAvailable ? UpdatesCard.Available : UpdatesCard.None;
        }

        /// <summary>The sentence under the card's heading in each state, or null for none.</summary>
        public static string CardNote(UpdatesCard card, string version)
        {
            switch (card)
            {
                case UpdatesCard.Available: return RestartNote;
                case UpdatesCard.Downloading: return Downloading(version);
                case UpdatesCard.Staged: return UpdateWording.RestartLater;
                default: return null;
            }
        }

        /// <summary>The card's padding and rhythm, from the artboard's inline styles.</summary>
        public const double CardPaddingX = 20;
        public const double CardPaddingY = 18;
        public const double CardGap = 18;
        public const double CardTextGap = 6;
        public const double CardHeadingGap = 12;
        public const double CardVersionSize = 15;
        public const double NotesPaddingTop = 14;
        public const double NotesPaddingBottom = 18;
        public const double NotesGap = 8;

        /// <summary>
        /// Whether a press sits beside the words it belongs to, on the card and the kept card, rather than
        /// under them. By the room the content has rather than by the sidebar's shape: a heading and its
        /// button are one row, not two columns, and they fit side by side well below TwoColumns.
        /// </summary>
        public const double ButtonBesideFrom = 520;

        public static bool ButtonBeside(double contentWidth)
        {
            return contentWidth >= ButtonBesideFrom;
        }

        // --- The check row --------------------------------------------------------------------------

        /// <summary>The switch's label. Its caption is UpdateWording.CheckCaption, which already says how often.</summary>
        public const string CheckTitle = "Check for updates";
        public const string CheckNow = "Check now";
        public const string NeverChecked = "Not checked yet";

        /// <summary>The artboard's .row: 14 above and below, with the Check now press 12 from the switch.</summary>
        public const double CheckControlsGap = 12;

        /// <summary>
        /// When the last check answered: "Last checked today, 09:12", "Last checked yesterday, 18:40",
        /// "Last checked 3 March, 09:12", with the year when it is not this one.
        /// </summary>
        /// <param name="ticks">OpenDashSettings.LastUpdateCheckTicks: UTC ticks, zero for never.</param>
        /// <param name="nowUtc">Now, in UTC.</param>
        /// <param name="zone">The zone to write the time in; null for the machine's own.</param>
        public static string LastChecked(long ticks, DateTime nowUtc, TimeZoneInfo zone = null)
        {
            if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return NeverChecked;
            zone = zone ?? TimeZoneInfo.Local;
            var then = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(ticks, DateTimeKind.Utc), zone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
            var time = then.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (then.Date == now.Date) return "Last checked today, " + time;
            if (then.Date == now.Date.AddDays(-1)) return "Last checked yesterday, " + time;
            var day = then.Year == now.Year
                ? then.ToString("d MMMM", CultureInfo.InvariantCulture)
                : then.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
            return "Last checked " + day + ", " + time;
        }

        /// <summary>
        /// The line the check row carries while the card is gone: checking, and the answer to a press. A
        /// background check that finds nothing says nothing (UpdateStatus.IsVisible), an offer is the card's,
        /// and checks being off is the switch's to say: the row does not repeat a switch that is visibly off
        /// (voice.md).
        /// </summary>
        public static string CheckLine(UpdateStatus status)
        {
            if (status == null) return null;
            switch (status.State)
            {
                case UpdateState.Checking:
                    return status.Line;
                case UpdateState.UpToDate:
                case UpdateState.Unreachable:
                    return status.Manual ? status.Line : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The check row's line beside the card that is showing. While the card is gone the row carries
        /// <see cref="CheckLine"/>; while an offer shows, a check in flight is said on the card, beside the
        /// Download it holds off (<see cref="CardLine"/>); under a download or a staged plugin the row still
        /// says a check is running, since nothing else would.
        /// </summary>
        public static string RowLine(UpdatesCard card, UpdateStatus status)
        {
            if (card == UpdatesCard.None) return CheckLine(status);
            if (card == UpdatesCard.Available || status == null || status.State != UpdateState.Checking) return null;
            return status.Line;
        }

        /// <summary>
        /// What the offer card's own line says of the check: "Checking for updates…" while one is in flight
        /// over a card that still shows the offer, and null otherwise, when the line is Download's.
        /// </summary>
        public static string CardLine(UpdatesCard card, UpdateStatus status)
        {
            return card == UpdatesCard.Available && status != null && status.State == UpdateState.Checking ? status.Line : null;
        }

        /// <summary>
        /// Whether Download can be pressed: not while a release is downloading, and not while a check is in
        /// flight, when the offer it would act on is being asked for again and the press could only say
        /// there is nothing to install.
        /// </summary>
        public static bool DownloadEnabled(UpdateState state, bool applying)
        {
            return !applying && state != UpdateState.Checking;
        }

        /// <summary>
        /// Whether Check now can be pressed: only while the checks are on, since the check refuses a press
        /// with the switch off, and neither during a download nor while a check is already in flight.
        /// </summary>
        public static bool CheckNowEnabled(bool checksOn, bool applying, UpdateState state)
        {
            return checksOn && !applying && state != UpdateState.Checking;
        }

        public const string CheckNowTooltip = "Asks GitHub for the newest release now.";

        /// <summary>Download's tooltip: "Downloads OpenDash 0.5.1."</summary>
        public static string DownloadTooltip(string version)
        {
            return "Downloads " + Heading(version) + ".";
        }

        /// <summary>
        /// What the page stands on once the switch is flipped. Off, the checks are off. On, what the panel
        /// opens on (UpdateMark.Opening), so a remembered offer comes back to the card and the sidebar's
        /// badge the moment the idle screen's mark shows it again; a "checks are off" this start heard
        /// before is not brought back.
        /// </summary>
        public static UpdateStatus Switched(bool on, UpdateStatus last, string offered, string installed)
        {
            if (!on) return new UpdateStatus { State = UpdateState.Disabled, InstalledVersion = installed };
            if (last != null && last.State == UpdateState.Disabled) last = null;
            return UpdateMark.Opening(true, last, offered, installed);
        }

        // --- In SimHub ------------------------------------------------------------------------------

        public const string InSimHubTitle = "In SimHub";

        /// <summary>The table's column heads, drawn as the artboard's .lbl in sentence case (brand.md).</summary>
        public const string ItemColumn = "Item";
        public const string VersionColumn = "Version";
        public const string StateColumn = "State";

        public const string DashboardKind = "dashboard";
        public const string StripKind = "LED profile";
        public const string MatrixKind = "matrix profile";

        /// <summary>What follows a row's name in the artboard's small caption, space included: " · dashboard".</summary>
        public static string KindCaption(string kind)
        {
            return string.IsNullOrEmpty(kind) ? string.Empty : " · " + kind;
        }

        /// <summary>The table with no row under its head: an empty rig, which points at the page that adds one.</summary>
        public const string NothingInSimHub = "Nothing yet. Add a screen on the Screens page.";

        public const string UpToDate = "Up to date";
        public const string UpdateAvailable = "Update available";
        public const string Missing = "Missing";
        public const string WaitingForRestart = "Waiting for a restart";

        /// <summary>The artboard's .f: a name column that takes the rest, 110 for the version and 150 for
        /// the state, 16 apart, padded 11 by 16; the head row pads 12 on top.</summary>
        public const double TableVersionWidth = 110;
        public const double TableStateWidth = 150;
        public const double TableGap = 16;
        public const double TableRowPaddingX = 16;
        public const double TableRowPaddingY = 11;
        public const double TableHeadPaddingTop = 12;
        public const double TableTextSize = 14;
        public const double TableKindSize = 12;
        public const double TableVersionSize = 15;
        public const double TableStateSize = 13;
        public const double TableDot = 7;
        public const double TableDotGap = 8;

        /// <summary>The sections' rhythm on this artboard: 12 under a heading and between what follows it
        /// (the table, its notes, the Reinstall row; the Support presses and their caption), with 14 between
        /// Reinstall everything and its line.</summary>
        public const double SectionGap = 12;
        public const double ReinstallLineGap = 14;

        /// <summary>
        /// Below this much content the version column goes and the name keeps its room: the three fixed
        /// columns take 324 of it before a row has a name.
        /// </summary>
        public const double TableVersionFrom = 480;

        public static double VersionWidth(double contentWidth)
        {
            return contentWidth >= TableVersionFrom ? TableVersionWidth : 0;
        }

        /// <summary>A version as the table writes it: empty for none, "Unknown" for a copy with no readable
        /// version, the version otherwise.</summary>
        public static string VersionText(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return string.Empty;
            if (string.Equals(version, Versioning.UnknownVersion, StringComparison.Ordinal)) return "Unknown";
            return version.Trim();
        }

        /// <summary>
        /// A rig dashboard's row.
        /// </summary>
        /// <param name="name">The screen's name, which is the Title SimHub lists it under.</param>
        /// <param name="package">What the installer last found for the screen's folder, or null.</param>
        /// <param name="installed">Whether the folder is in DashTemplates, or null when unknown.</param>
        /// <param name="waitsForRestart">Whether SimHub has yet to load it (ScreenFacts.AddedSinceStart).</param>
        /// <remarks>
        /// A failure outranks everything; then a folder that has gone, which is what Home calls a missing
        /// screen; then one SimHub has not loaded yet, known only when the shell's facts say so; then the
        /// installer's own status.
        /// </remarks>
        public static UpdatesRow DashboardRow(string name, PackageStatus package, bool? installed, bool? waitsForRestart)
        {
            var version = VersionText(package == null ? null : package.InstalledVersion);
            if (package != null && package.Status == InstallStatus.Failed)
            {
                return Row(name, DashboardKind, version, PanelCopy.InstallFailed, Theme.StatusFailed, "Install failed. See SimHub's log.");
            }
            if (installed == false)
            {
                return Row(name, DashboardKind, string.Empty, Missing, Theme.StatusUpdateAvailable, "SimHub does not have this dashboard. Reinstall everything writes it again.");
            }
            if (waitsForRestart == true)
            {
                return Row(name, DashboardKind, version, WaitingForRestart, Theme.StatusUpdateAvailable, "Restart SimHub to see it in the dashboard list.");
            }
            var status = package == null ? InstallStatus.NotInstalled : package.Status;
            switch (status)
            {
                case InstallStatus.UpToDate:
                    return Row(name, DashboardKind, version, UpToDate, Theme.StatusUpToDate, null);
                case InstallStatus.UpdateAvailable:
                    return Row(name, DashboardKind, version, UpdateAvailable, Theme.StatusUpdateAvailable,
                        package.HeldBack || package.Edited ? EditedTooltip : BringsItTo(package.EmbeddedVersion));
                default:
                    return Row(name, DashboardKind, version, PanelCopy.NotInstalled, Theme.TextLabel, "Reinstall everything writes it.");
            }
        }

        public const string EditedTooltip = "You have edited it, so OpenDash left it alone. " + PanelConfirmation.ReinstallLabel + " replaces it.";

        /// <summary>An older dashboard's tooltip, naming the version the row does not show: "Reinstall
        /// everything brings it to 0.5.0."</summary>
        public static string BringsItTo(string embeddedVersion)
        {
            return string.IsNullOrWhiteSpace(embeddedVersion)
                ? PanelConfirmation.ReinstallLabel + " brings it up to date."
                : PanelConfirmation.ReinstallLabel + " brings it to " + embeddedVersion.Trim() + ".";
        }

        /// <summary>A strip's row: its name, the version of its profile in SimHub, and the state
        /// PanelCopy.LightRow says, with an Update press while that profile is older than this build's.</summary>
        public static UpdatesRow StripRow(string name, FlagBoxPlan plan)
        {
            return LightRow(name, StripKind, plan, StripTooltip(plan));
        }

        /// <summary>The flag box profile's row, drawn when the rig has a matrix.</summary>
        /// <param name="extractedPath">Where OpenDash left the profile, which the tooltip names while SimHub's
        /// matrix settings cannot be reached and the by-hand route under the table is the way in.</param>
        public static UpdatesRow FlagBoxRow(string name, FlagBoxPlan plan, string extractedPath)
        {
            return LightRow(name, MatrixKind, plan, FlagBoxTooltip(plan, extractedPath));
        }

        /// <summary>
        /// A light profile's row, in PanelCopy.LightRow's words, with its dot from the same words: the Matrix
        /// page's pill reads PanelLightRows.DotHex beside words of its own (PanelMatrix.ProfileRow), so this
        /// table does not take its dot from there.
        /// </summary>
        private static UpdatesRow LightRow(string name, string kind, FlagBoxPlan plan, string tooltip)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            var words = PanelCopy.LightRow(state, plan == null ? null : plan.InstalledVersion);
            return new UpdatesRow(name, kind, VersionText(plan == null ? null : plan.InstalledVersion), words.State, words.StateHex,
                Dot(words.StateHex), tooltip, state == FlagBoxInstallState.Outdated);
        }

        /// <summary>
        /// A strip row's tooltip in each state. Up to date says no more than the state, since the version has a
        /// column of its own; an older profile says what the row's Update does; a missing one names the press
        /// on this page that installs it.
        /// </summary>
        public static string StripTooltip(FlagBoxPlan plan)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            switch (state)
            {
                case FlagBoxInstallState.NotInstalled:
                    return StripNotInstalled;
                case FlagBoxInstallState.UpToDate:
                    return InstalledUpToDate;
                default:
                    return PanelLightRows.Tooltip(1, plan);
            }
        }

        /// <summary>A row is one strip of the rig, so the census rows' "no strip of this shape" does not
        /// apply to it: the strip is there and its profile is not.</summary>
        public const string StripNotInstalled = "Its profile is not in SimHub. " + PanelConfirmation.ReinstallLabel + " installs it.";

        public const string InstalledUpToDate = "Installed and up to date.";

        /// <summary>
        /// The flag box row's tooltip in each state. Not FlagBoxInstallPlan.Summary whole, which was written
        /// for a row with a press beside it: this row has only Update, so an up-to-date profile is not told
        /// about a Reinstall it does not offer, and a missing one names the press on this page.
        /// </summary>
        public static string FlagBoxTooltip(FlagBoxPlan plan, string extractedPath)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            switch (state)
            {
                case FlagBoxInstallState.NotInstalled:
                    return FlagBoxNotInstalled;
                case FlagBoxInstallState.UpToDate:
                    return InstalledUpToDate;
                default:
                    return FlagBoxInstallPlan.Summary(plan, extractedPath);
            }
        }

        public const string FlagBoxNotInstalled = "Not installed. " + PanelConfirmation.ReinstallLabel + " installs it, then select it on your device.";

        /// <summary>A light row's Update press: what it costs, in the words the Matrix and LEDs pages warn with.</summary>
        public const string RowUpdateTooltip = FlagBoxInstallPlan.Replaces;

        private static UpdatesRow Row(string name, string kind, string version, string state, string hex, string tooltip)
        {
            return new UpdatesRow(name, kind, version, state, hex, Dot(hex), tooltip, false);
        }

        /// <summary>The dot beside words in this ink: the same colour, except that an uninstalled state's
        /// words are text.label and its dot status.notInstalled, as the canvas draws the pair.</summary>
        private static string Dot(string hex)
        {
            return string.Equals(hex, Theme.TextLabel, StringComparison.Ordinal) ? Theme.StatusNotInstalled : hex;
        }

        // --- Reinstall everything -------------------------------------------------------------------

        /// <summary>
        /// What Reinstall everything puts on its line before it replaces a dashboard somebody has edited,
        /// naming each by the name SimHub lists it under.
        /// </summary>
        public static string ReinstallQuestion(IReadOnlyCollection<string> names)
        {
            var count = names == null ? 0 : names.Count;
            return "You have edited " + (count == 1 ? "1 dashboard" : count + " dashboards")
                + ": " + string.Join(", ", names ?? new string[0])
                + ". Reinstalling replaces your version. A copy is kept, and \"" + PutMineBack + "\" restores it.";
        }

        /// <summary>Reinstall everything's tooltip: what it writes, and what that costs a profile somebody
        /// changed, which its question (about dashboards) does not cover.</summary>
        public const string ReinstallTooltip = "Installs every dashboard on your rig again, and each older or missing LED and matrix profile. A profile you have edited is replaced.";

        /// <summary>
        /// Whether Reinstall everything writes a light profile SimHub holds in this state (ruling 70): an
        /// older one, and one that is missing or whose install failed. Never one that is current, since a
        /// rewrite could only cost the edits made to it in SimHub, and never where SimHub cannot be reached
        /// or the build carries no profile.
        /// </summary>
        public static bool BringsForward(FlagBoxInstallState state)
        {
            return state == FlagBoxInstallState.Outdated || state == FlagBoxInstallState.NotInstalled || state == FlagBoxInstallState.Failed;
        }

        /// <summary>The flag box profile too, but only on a rig with a matrix: the table draws its row only
        /// there, and a run must not report a row the page does not show.</summary>
        public static bool BringsFlagBoxForward(bool hasMatrix, FlagBoxInstallState state)
        {
            return hasMatrix && BringsForward(state);
        }

        /// <summary>
        /// What Reinstall everything says when it has run: the dashboards it wrote and left alone and the step
        /// OpenDash does not take for them (voice.md), then the light profiles it brought forward, then what
        /// could not be written and where to look.
        /// </summary>
        /// <param name="replaced">Dashboards written.</param>
        /// <param name="held">Dashboards left alone because they were edited and nobody said to replace them.</param>
        /// <param name="wroteFonts">Whether a font went into DashFonts, which only a restart shows.</param>
        /// <param name="lights">What the run did to each light profile it wrote.</param>
        /// <param name="flagBoxName">The flag box profile's name in SimHub.</param>
        public static string ReinstallSummary(int replaced, int held, bool wroteFonts, UpdatesLightsTally lights, string flagBoxName)
        {
            lights = lights ?? new UpdatesLightsTally();
            var line = new StringBuilder();
            line.Append(replaced == 1 ? "Reinstalled 1 dashboard." : "Reinstalled " + replaced + " dashboards.");
            if (held == 1) line.Append(" The one you edited was left alone.");
            else if (held > 1) line.Append(" The " + held + " you edited were left alone.");
            // Straight after the dashboards, so "it" is one of them whatever the profiles did.
            if (replaced > 0) line.Append(" " + UpdateWording.ToSee(wroteFonts));
            Profiles(line, "Updated", lights.StripsUpdated);
            Profiles(line, "Installed", lights.StripsInstalled);
            var name = string.IsNullOrWhiteSpace(flagBoxName) ? FlagBoxProfile.ProfileName : flagBoxName;
            if (lights.FlagBoxAfter == FlagBoxInstallState.UpToDate) line.Append(" " + (lights.FlagBoxWasOlder ? "Updated " : "Installed ") + name + ".");
            NotWritten(line, lights.StripsNotUpdated, "updated");
            NotWritten(line, lights.StripsNotInstalled, "installed");
            if (lights.FlagBoxAfter.HasValue && lights.FlagBoxAfter != FlagBoxInstallState.UpToDate)
            {
                line.Append(" " + name + " could not be " + (lights.FlagBoxWasOlder ? "updated." : "installed."));
            }
            if (!lights.Ok) line.Append(" See SimHub's log.");
            return line.ToString();
        }

        private static void Profiles(StringBuilder line, string verb, int count)
        {
            if (count == 1) line.Append(" " + verb + " 1 LED profile.");
            else if (count > 1) line.Append(" " + verb + " " + count + " LED profiles.");
        }

        private static void NotWritten(StringBuilder line, int count, string participle)
        {
            if (count == 1) line.Append(" 1 LED profile could not be " + participle + ".");
            else if (count > 1) line.Append(" " + count + " LED profiles could not be " + participle + ".");
        }

        /// <summary>What Reinstall everything says when the dashboards could not be written.</summary>
        public static string ReinstallFailed(string reason)
        {
            return "The reinstall did not finish: " + reason;
        }

        // --- The kept copy --------------------------------------------------------------------------

        public const string PutMineBack = "Put mine back";

        /// <summary>The artboard's kept card: 14 by 18, 16 between the words and the press, 3 between the
        /// lines, its title at 15.</summary>
        public const double KeptPaddingX = 18;
        public const double KeptPaddingY = 14;
        public const double KeptGap = 16;
        public const double KeptTextGap = 3;
        public const double KeptTitleSize = 15;

        /// <summary>"Your edited Rim was kept", "Your edited Rim and Pit wall were kept".</summary>
        public static string KeptTitle(IList<string> names)
        {
            var list = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (list.Count == 0) return "Your edited dashboard was kept";
            if (list.Count == 1) return "Your edited " + list[0] + " was kept";
            return "Your edited " + string.Join(", ", list.Take(list.Count - 1)) + " and " + list[list.Count - 1] + " were kept";
        }

        /// <summary>
        /// The kept card's caption. It does not name the press that replaced the dashboard: an update, Reinstall
        /// everything and the Screens page's reinstall each keep a copy, and the card cannot tell which.
        /// </summary>
        public static string KeptCaption(int count)
        {
            return count > 1
                ? "OpenDash replaced them. Your copies are still here."
                : "OpenDash replaced it. Your copy is still here.";
        }

        /// <summary>
        /// Whether a folder is drawn on the kept card: while a copy kept from edited work is there to put back,
        /// and only while the folder in SimHub is not the driver's own. Put mine back leaves the copy where it
        /// was, so a folder already put back, or edited again since, is the driver's and drops off the card;
        /// the next replacement they agree to brings it back. That also keeps Put mine back from ever
        /// overwriting a folder that holds edits, which it would do without asking.
        /// </summary>
        /// <param name="copies">PackageExtractor.KeptCopies for the folder.</param>
        /// <param name="edited">Whether the installer finds the folder in SimHub edited (PackageStatus.Edited).</param>
        public static bool ShowsKept(IEnumerable<string> copies, bool edited)
        {
            return !edited && (copies ?? Enumerable.Empty<string>()).Any(path => path != null && path.Contains(PackageExtractor.EditedSuffix));
        }

        /// <summary>The name SimHub lists a folder under: its screen's, or the folder's own for a folder no
        /// screen on the rig is written to.</summary>
        public static string ScreenName(IEnumerable<ScreenInstance> screens, string folder)
        {
            var screen = (screens ?? Enumerable.Empty<ScreenInstance>())
                .FirstOrDefault(s => s != null && string.Equals(s.Folder, folder, StringComparison.OrdinalIgnoreCase));
            return screen == null || string.IsNullOrWhiteSpace(screen.Name) ? folder : screen.Name;
        }

        /// <summary>What Put mine back says when it has run.</summary>
        public static string PutBack(int restored)
        {
            if (restored <= 0) return "There was nothing to put back.";
            return "Put back " + (restored == 1 ? "1 dashboard" : restored + " dashboards") + ". " + UpdateWording.Reopen;
        }

        // --- Support --------------------------------------------------------------------------------

        public const string SupportTitle = "Support";
        public const string CopyReport = "Copy a support report";
        public const string OpenLog = "Open the log";
        public const string ReportIssue = "Report an issue";
        public const string ReadGuide = "Read the guide";
        public const string SupportCaption = "Versions, devices and the last 200 log lines, copied to paste. Nothing is sent.";
        public const string Licence = "OpenDash is free software under the MIT licence.";

        public const double SupportButtonGap = 8;
        public const double SupportCaptionSize = 12;

        public const string ReportCopied = "Support report copied. Paste it into your issue.";

        public static string ReportFailed(string reason)
        {
            return "Could not copy the support report: " + reason;
        }

        public const string LogMissing = "SimHub has not written a log yet.";

        public static string LogFailed(string reason)
        {
            return "Could not open SimHub's log folder: " + reason;
        }

        /// <summary>Where SimHub writes its log, under its own folder: Logs\SimHub.txt is the current one and
        /// SimHub.N.txt the rotations (docs/testing-vm.md).</summary>
        public const string LogFolder = "Logs";
        public const string LogFile = "SimHub.txt";

        /// <summary>What marks a line of SimHub's log as OpenDash's: Log.Prefix, without its space.</summary>
        public const string LogMarker = "[OpenDash]";

        /// <summary>How many of those lines the report carries, which the caption promises.</summary>
        public const int LogLines = 200;

        /// <summary>The last <paramref name="max"/> lines that are OpenDash's, oldest first.</summary>
        public static IList<string> Tail(IEnumerable<string> lines, int max = LogLines)
        {
            var kept = new Queue<string>();
            if (lines == null || max <= 0) return kept.ToList();
            foreach (var line in lines)
            {
                if (line == null || line.IndexOf(LogMarker, StringComparison.Ordinal) < 0) continue;
                kept.Enqueue(line);
                if (kept.Count > max) kept.Dequeue();
            }
            return kept.ToList();
        }

        /// <summary>
        /// The support report: plain text to paste into an issue, and nothing more. It carries versions,
        /// the rig and its devices, the update check and OpenDash's own log lines, and no setting value
        /// beyond those, since nothing else helps to read a report.
        /// </summary>
        public static string Report(UpdatesReportInput input)
        {
            input = input ?? new UpdatesReportInput();
            var text = new StringBuilder();
            text.AppendLine("OpenDash support report");
            text.AppendLine("Written " + input.GeneratedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC");
            text.AppendLine();
            text.AppendLine("OpenDash plugin: " + Known(input.PluginVersion));
            text.AppendLine("Dashboards on the rig: " + Known(input.RigVersion));
            text.AppendLine("SimHub: " + Known(input.SimHubVersion) + (string.IsNullOrWhiteSpace(input.SimHubRoot) ? string.Empty : ", in " + input.SimHubRoot));
            text.AppendLine("Windows: " + Known(input.OsVersion));
            text.AppendLine("Update check: " + (input.ChecksOn ? "on" : "off") + ". " + LastChecked(input.LastCheckedTicks, input.GeneratedUtc, TimeZoneInfo.Utc) + " (UTC).");
            if (!string.IsNullOrWhiteSpace(input.UpdateLine)) text.AppendLine("Update status: " + input.UpdateLine);
            if (input.RestartPending) text.AppendLine("Update status: " + UpdateWording.RestartLater);
            List("Screens", input.Screens, text);
            List("LED strips", input.Strips, text);
            List("Matrices", input.Matrices, text);
            if (input.FlagBox != null)
            {
                text.AppendLine();
                text.AppendLine("Flag box profile: " + Item(input.FlagBox));
            }
            text.AppendLine();
            text.AppendLine("Car tables: " + Known(input.CarTables));
            var log = input.Log ?? new string[0];
            text.AppendLine();
            text.AppendLine("SimHub's log, the last " + log.Count + " OpenDash lines:");
            foreach (var line in log) text.AppendLine(line);
            return text.ToString();
        }

        private static void List(string title, IList<UpdatesReportItem> items, StringBuilder text)
        {
            var list = items ?? new UpdatesReportItem[0];
            text.AppendLine();
            text.AppendLine(title + " (" + list.Count + ")");
            foreach (var item in list) text.AppendLine("- " + Item(item));
        }

        private static string Item(UpdatesReportItem item)
        {
            var line = item.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(item.Detail)) line += " (" + item.Detail + ")";
            var facts = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.State)) facts.Add(item.State);
            if (!string.IsNullOrWhiteSpace(item.Version)) facts.Add(item.Version);
            return facts.Count == 0 ? line : line + ": " + string.Join(", ", facts);
        }

        private static string Known(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }

        // --- Anchors and search ---------------------------------------------------------------------

        public const string AnchorPlugin = "updates.plugin";
        public const string AnchorCheck = "updates.check";
        public const string AnchorPackages = "updates.packages";
        public const string AnchorLights = "updates.lights";
        public const string AnchorReinstall = "updates.reinstall";
        public const string AnchorKept = "updates.kept";
        public const string AnchorSupport = "updates.support";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(CheckTitle, PanelPage.Updates, AnchorCheck, "update", "github", "release", "version", "download"),
            new PanelSearch.Entry(InSimHubTitle, PanelPage.Updates, AnchorPackages, "dashboards", "profiles", "installed", "version", "flag box"),
            new PanelSearch.Entry(PanelConfirmation.ReinstallLabel, PanelPage.Updates, AnchorReinstall, "repair", "dashboards", "profiles"),
            // The kept card is there only while a copy is; without it, AnchorKept is on Reinstall everything,
            // whose question says what Put mine back is.
            new PanelSearch.Entry(PutMineBack, PanelPage.Updates, AnchorKept, "restore", "edited"),
            new PanelSearch.Entry(SupportTitle, PanelPage.Updates, AnchorSupport, "help", "bug"),
            new PanelSearch.Entry(CopyReport, PanelPage.Updates, AnchorSupport, "diagnostics", "log"),
            new PanelSearch.Entry(OpenLog, PanelPage.Updates, AnchorSupport, "logs", "simhub"),
            new PanelSearch.Entry(ReportIssue, PanelPage.Updates, AnchorSupport, "bug", "github"),
            new PanelSearch.Entry(ReadGuide, PanelPage.Updates, AnchorSupport, "documentation", "help", "manual"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries). Updates draws none.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>
        /// Search labels this page draws through something other than the constant, each with the text its
        /// sources draw it by, so the list is a record rather than a way round PanelSearchTests.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Reinstall everything's label is bound to its line, through PanelConfirmation.Label, so that it
            // reads "Replace anyway" only while its own question is showing.
            { PanelConfirmation.ReinstallLabel, "UpdatesLabelFrom(updatesReinstallLine, ReplacingAction.Reinstall)" },
        };
    }
}
