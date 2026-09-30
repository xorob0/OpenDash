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

        /// <summary>Whether the card draws "You have" and the rig's version: only while that version is known,
        /// since the words alone would promise a number that is not there.</summary>
        public static bool ShowsYouHave(string rigVersion)
        {
            return !string.IsNullOrWhiteSpace(rigVersion);
        }

        /// <summary>Under the heading of an offer. OpenDash has no presets, and the step is what is said.</summary>
        public const string RestartNote = "Restart SimHub to finish updating. Your settings are kept.";

        public const string ReleaseNotesTitle = "Release notes";

        /// <summary>The card foot's heading over the release's opening sentence, or null when there is none: a
        /// remembered offer (UpdateMark.Opening) carries no notes, and a heading over nothing but the link is
        /// not drawn.</summary>
        public static string NotesHeading(string summary)
        {
            return string.IsNullOrWhiteSpace(summary) ? null : ReleaseNotesTitle;
        }
        public const string EveryRelease = "Every release on GitHub";

        /// <summary>The line over the bar while a release downloads.</summary>
        public static string Downloading(string version)
        {
            return "Downloading " + (string.IsNullOrWhiteSpace(version) ? "the update" : version.Trim()) + "…";
        }

        /// <summary>
        /// Whether a check's answer runs the Download that was waiting for it: only a press that was waiting,
        /// only onto an offer, and never over a staged plugin, which the check offers again because the old
        /// one still runs until the restart.
        /// </summary>
        public static bool AppliesWhenAnswered(bool waiting, UpdateState state, bool pending)
        {
            return waiting && state == UpdateState.UpdateAvailable && !pending;
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

        /// <summary>The table with no row under its head: an empty rig, in the one phrase the panel has for
        /// it (PanelScreens.NoScreens), pointing at the page that adds one.</summary>
        public const string NothingInSimHub = PanelScreens.NoScreens + ". Add a screen on the Screens page.";

        public const string UpToDate = "Up to date";
        public const string UpdateAvailable = "Update available";

        /// <summary>A rig dashboard whose folder has gone from DashTemplates. The table's own word, as each
        /// state word here is: the Screens card says the same states in words that are its page's to change.</summary>
        public const string MissingFromSimHub = "Missing from SimHub";

        /// <summary>A rig dashboard SimHub has not loaded since it was written (ruling 69).</summary>
        public const string WaitingForRestart = "Waiting for a restart";

        /// <summary>A rig dashboard this build ships nothing for, whose folder SimHub has.</summary>
        public const string InSimHubState = "In SimHub";

        /// <summary>Under the table when this build carries no dashboard at all, as a dev build does: every
        /// row would otherwise read as if Reinstall everything could write it. Every note about what the build
        /// carries opens "This build ships no", as PanelLightRows.NoProfiles does.</summary>
        public const string NoDashboards = "This build ships no dashboards.";

        /// <summary>Under the table when the installer's last read or write of a folder failed.</summary>
        public const string InstallerFailed = "OpenDash could not read or write a dashboard. See SimHub's log.";

        /// <summary>
        /// The sentences under the table, in order: what this build ships and what the installer could not
        /// do, then the light profiles' own (uncovered 24): OpenDash never installs one on its own, said only
        /// over a light row, a build with none for the rig's strips, and SimHub's LED settings out of reach.
        /// </summary>
        /// <param name="shipsDashboards">DashboardInstaller.HasEmbeddedPackage.</param>
        /// <param name="installerError">DashboardInstaller.LastError.</param>
        /// <param name="rowFailed">Whether a dashboard row already says "Install failed", which the
        /// installer's error would repeat.</param>
        /// <param name="hasLightRows">Whether the table draws a strip or flag box row: the sentence about
        /// profiles is about those rows, and a rig with none has nothing it would be said of.</param>
        /// <param name="hasStrips">Whether the rig has a strip.</param>
        /// <param name="shipsProfiles">Whether this build embeds any strip profile.</param>
        /// <param name="stripsReachable">Whether SimHub's LED settings could be read.</param>
        public static IList<string> TableNotes(bool shipsDashboards, string installerError, bool rowFailed, bool hasLightRows, bool hasStrips, bool shipsProfiles, bool stripsReachable)
        {
            var notes = new List<string>();
            if (!shipsDashboards) notes.Add(NoDashboards);
            else if (!string.IsNullOrWhiteSpace(installerError) && !rowFailed) notes.Add(InstallerFailed);
            if (hasLightRows) notes.Add(PanelLightRows.SectionCaption);
            if (hasStrips && !shipsProfiles) notes.Add(PanelLightRows.NoProfiles);
            else if (hasStrips && !stripsReachable) notes.Add(PanelLightRows.Unavailable);
            return notes;
        }

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

        /// <summary>The table card's border, one on each side, which the rows are inside.</summary>
        public const double TableBorder = 2 * PanelMetrics.BorderWeight;

        /// <summary>
        /// The press column's width while a light row carries its Update: the small outline press's own width
        /// and the 16 before it. Every row shares the column (SharedSizeGroup), the head included, so one
        /// press takes this from every row's name.
        /// </summary>
        public const double TablePressColumn = 80;

        /// <summary>The least a name keeps before the version column gives its room up.</summary>
        public const double TableNameMin = 154;

        /// <summary>
        /// The version's own width, the artboard's 110, or 0 where the column goes: the column goes when the
        /// name would keep less than <see cref="TableNameMin"/>. What the name keeps is the content less the
        /// card's border, the row's padding, the two fixed columns with their gaps (2 + 16 + 16 + 110 + 16 +
        /// 150 + 16 = 326) and the press column when any row has a press, so the version goes below 480
        /// without a press and below 560 with one.
        /// </summary>
        public static double VersionWidth(double contentWidth, bool hasPress = false)
        {
            var name = contentWidth - TableBorder - 2 * TableRowPaddingX - ColumnWidth(TableVersionWidth) - ColumnWidth(TableStateWidth)
                - (hasPress ? TablePressColumn : 0);
            return name >= TableNameMin ? TableVersionWidth : 0;
        }

        /// <summary>Whether any light row draws its Update press, which takes the press column's room from
        /// every row: a profile older than this build's.</summary>
        public static bool TableHasPress(IEnumerable<FlagBoxPlan> lightPlans)
        {
            return (lightPlans ?? Enumerable.Empty<FlagBoxPlan>()).Any(plan => plan != null && plan.State == FlagBoxInstallState.Outdated);
        }

        /// <summary>
        /// A fixed column's width in the grid: its content's width and the 16 before it, since the artboard's
        /// gap is outside its 110 and 150 and each cell sits 16 in from its column's left edge. A hidden
        /// column takes no gap either.
        /// </summary>
        public static double ColumnWidth(double contentWidth)
        {
            return contentWidth > 0 ? contentWidth + TableGap : 0;
        }

        /// <summary>What the table writes for a version it cannot read, and for a light profile's state the
        /// page cannot know (PanelCopy.LightRow).</summary>
        public const string Unknown = "Unknown";

        /// <summary>A version as the table writes it: empty for none, <see cref="Unknown"/> for a copy with no
        /// readable version, the version otherwise.</summary>
        public static string VersionText(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return string.Empty;
            if (string.Equals(version, Versioning.UnknownVersion, StringComparison.Ordinal)) return Unknown;
            return version.Trim();
        }

        /// <summary>
        /// A rig dashboard's row.
        /// </summary>
        /// <param name="screen">The screen, whose name is the Title SimHub lists it under.</param>
        /// <param name="package">What the installer last found for the screen's folder, or null when this
        /// build ships no package for it: DashboardInstaller.Plan leaves such a folder exactly as it is, so
        /// Reinstall everything does not write it.</param>
        /// <param name="installed">Whether the folder is in DashTemplates, or null when unknown.</param>
        /// <param name="waitsForRestart">Whether SimHub has yet to load it (ScreenFacts.AddedSinceStart).</param>
        /// <remarks>
        /// A failure outranks everything; then a folder that has gone, in the failure ink; then one SimHub has
        /// not loaded yet, known only when the shell's facts say so; then the installer's own status. The
        /// state words are this table's own (<see cref="MissingFromSimHub"/>, <see cref="WaitingForRestart"/>,
        /// <see cref="InSimHubState"/>), not read from another page. A screen this build ships nothing for is
        /// said from the facts alone, with a tooltip that says why no press here changes it.
        /// </remarks>
        public static UpdatesRow DashboardRow(ScreenInstance screen, PackageStatus package, bool? installed, bool? waitsForRestart)
        {
            var name = screen == null ? string.Empty : screen.Name;
            var version = VersionText(package == null ? null : package.InstalledVersion);
            if (package == null)
            {
                var shipsNo = ShipsNo(screen);
                if (installed == false) return Row(name, DashboardKind, string.Empty, MissingFromSimHub, Theme.StatusFailed, shipsNo);
                if (installed == true) return Row(name, DashboardKind, string.Empty, InSimHubState, Theme.TextLabel, shipsNo);
                return Row(name, DashboardKind, string.Empty, PanelCopy.NotInstalled, Theme.TextLabel, shipsNo);
            }
            if (package.Status == InstallStatus.Failed)
            {
                return Row(name, DashboardKind, version, PanelCopy.InstallFailed, Theme.StatusFailed, "Install failed. See SimHub's log.");
            }
            if (installed == false)
            {
                return Row(name, DashboardKind, string.Empty, MissingFromSimHub, Theme.StatusFailed, MissingTooltip(name));
            }
            if (waitsForRestart == true)
            {
                return Row(name, DashboardKind, version, WaitingForRestart, Theme.Caution, RestartStep(name));
            }
            switch (package.Status)
            {
                case InstallStatus.UpToDate:
                    return Row(name, DashboardKind, version, UpToDate, Theme.StatusUpToDate, null);
                case InstallStatus.UpdateAvailable:
                    return Row(name, DashboardKind, version, UpdateAvailable, Theme.StatusUpdateAvailable,
                        package.HeldBack || package.Edited ? EditedTooltip : BringsItTo(package.EmbeddedVersion));
                default:
                    return Row(name, DashboardKind, version, PanelCopy.NotInstalled, Theme.TextLabel, NotInstalledTooltip);
            }
        }

        /// <summary>A missing dashboard's tooltip, in Home's words for the same state.</summary>
        public static string MissingTooltip(string name)
        {
            return name + "'s dashboard is missing from SimHub. " + PanelConfirmation.ReinstallLabel + " installs it again.";
        }

        /// <summary>A dashboard SimHub has not loaded yet: the one step, as Home and the Screens fix box say it.</summary>
        public static string RestartStep(string name)
        {
            return "Restart SimHub, then assign \"" + name + "\" to this display in Dash Studio.";
        }

        public const string NotInstalledTooltip = PanelConfirmation.ReinstallLabel + " installs it.";

        /// <summary>"This build ships no 1280 × 480 face." for a screen whose package this build does not
        /// carry.</summary>
        public static string ShipsNo(ScreenInstance screen)
        {
            if (screen == null) return NoDashboards;
            return "This build ships no " + screen.SizeLabel + " " + PanelAddScreen.KindName(screen.Kind).ToLowerInvariant() + ".";
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

        /// <summary>Whether the table draws the flag box profile's row: on a rig with a matrix, and only when
        /// this build carries the profile, since a row for one it cannot write could say nothing true.</summary>
        public static bool DrawsFlagBoxRow(bool hasMatrix, bool embedded)
        {
            return hasMatrix && embedded;
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
        /// A strip row's tooltip in each state, or null where the row already says it all (voice.md): an
        /// up-to-date profile, whose version has a column of its own, has none, as a dashboard's row has none.
        /// An older profile names the version its Update brings, in the dashboard rows' form (BringsItTo), and
        /// leaves what the press costs to the press's own tooltip; a missing one names the press on this page
        /// that installs it and the step OpenDash does not take.
        /// </summary>
        public static string StripTooltip(FlagBoxPlan plan)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            switch (state)
            {
                case FlagBoxInstallState.NotInstalled:
                    return StripNotInstalled;
                case FlagBoxInstallState.UpToDate:
                    return null;
                case FlagBoxInstallState.Outdated:
                    return UpdateBringsItTo(plan.EmbeddedVersion);
                case FlagBoxInstallState.Unavailable:
                    return PanelLightRows.Unavailable;
                case FlagBoxInstallState.NotEmbedded:
                    return StripNotEmbedded;
                default:
                    return LightFailed;
            }
        }

        /// <summary>A row is one strip of the rig, so the census rows' "no strip of this shape" does not
        /// apply to it: the strip is there and its profile is not, which the row's state already says.</summary>
        public const string StripNotInstalled = PanelConfirmation.ReinstallLabel + " installs it, then select it on its device.";

        /// <summary>A strip whose shape this build carries no profile for, whatever SimHub holds.</summary>
        public const string StripNotEmbedded = "This build ships no LED profile for it.";

        public const string LightFailed = "Install failed. See SimHub's log.";

        /// <summary>An older light profile's tooltip, naming the version the row's Update brings: "Update
        /// brings it to 0.5.0."</summary>
        public static string UpdateBringsItTo(string embeddedVersion)
        {
            return string.IsNullOrWhiteSpace(embeddedVersion)
                ? RowUpdate + " brings it up to date."
                : RowUpdate + " brings it to " + embeddedVersion.Trim() + ".";
        }

        /// <summary>
        /// The flag box row's tooltip in each state. An older profile is said as a strip's is, and a missing one
        /// names the press on this page; an up-to-date one keeps only the step OpenDash does not take, since
        /// installing a profile does not select it (FlagBoxInstallPlan.Summary). The rest are
        /// FlagBoxInstallPlan.Summary's, which names the file to import by hand while SimHub's matrix settings
        /// cannot be reached.
        /// </summary>
        public static string FlagBoxTooltip(FlagBoxPlan plan, string extractedPath)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            switch (state)
            {
                case FlagBoxInstallState.NotInstalled:
                    return FlagBoxNotInstalled;
                case FlagBoxInstallState.UpToDate:
                    return FlagBoxSelect;
                case FlagBoxInstallState.Outdated:
                    return UpdateBringsItTo(plan.EmbeddedVersion);
                default:
                    return FlagBoxInstallPlan.Summary(plan, extractedPath);
            }
        }

        public const string FlagBoxNotInstalled = PanelConfirmation.ReinstallLabel + " installs it, then select it on your device.";

        /// <summary>The step after the flag box profile is installed, in FlagBoxInstallPlan.Summary's words.</summary>
        public const string FlagBoxSelect = "Select it on your device to use it.";

        /// <summary>The step after strip profiles are installed, for one and for several: installing adds a
        /// profile to its device's list without selecting it.</summary>
        public const string StripSelect = "Select it on its device in SimHub to use it.";
        public const string StripsSelect = "Select each on its device in SimHub to use it.";

        /// <summary>The light row's one press, while its profile is older than this build's.</summary>
        public const string RowUpdate = "Update";

        /// <summary>A light row's Update press: what it costs, in the words the Matrix and LEDs pages warn with.
        /// The row's hover names the version the press brings; this is the press's own.</summary>
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
        /// changed, which its question (about dashboards) does not cover. Only an older one: a current profile
        /// is never rewritten (<see cref="BringsForward"/>).</summary>
        public const string ReinstallTooltip = "Installs every dashboard on your rig again, and each older or missing LED and matrix profile. An older profile you have edited is replaced.";

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

        /// <summary>Reinstall everything's choice among the rig's strips, as the press passes it to the write:
        /// every strip SimHub holds a profile for in a state <see cref="BringsForward"/> writes.</summary>
        public static bool ReinstallWrites(LedBar bar, FlagBoxInstallState state)
        {
            return BringsForward(state);
        }

        /// <summary>A strip row's Update: that strip alone, and only while its profile is older than this
        /// build's -- never a current one, which a rewrite could only cost the edits made to it.</summary>
        public static Func<LedBar, FlagBoxInstallState, bool> RowUpdateWrites(string rowKey)
        {
            return (bar, state) => state == FlagBoxInstallState.Outdated && string.Equals(StripKey(bar), rowKey, StringComparison.Ordinal);
        }

        /// <summary>The key a strip's plan is held under: its namespace, which its profile's id is made from,
        /// so two strips of one shape are two keys.</summary>
        public static string StripKey(LedBar bar)
        {
            return bar == null ? string.Empty : bar.Namespace ?? bar.Name ?? string.Empty;
        }

        /// <summary>
        /// The strips a press writes, in the census's order, each with what SimHub held for it: those
        /// <paramref name="wanted"/> picks, and none while SimHub's LED settings cannot be read.
        /// </summary>
        public static IList<KeyValuePair<LedBar, FlagBoxPlan>> StripsToWrite(IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> census, bool reachable, Func<LedBar, FlagBoxInstallState, bool> wanted)
        {
            if (!reachable || wanted == null) return new List<KeyValuePair<LedBar, FlagBoxPlan>>();
            return (census ?? Enumerable.Empty<KeyValuePair<LedBar, FlagBoxPlan>>())
                .Where(entry => entry.Key != null && entry.Value != null && wanted(entry.Key, entry.Value.State))
                .ToList();
        }

        /// <summary>
        /// What a strip the press picked is reported as when it is not written, or null when it can be: no
        /// profile in this build for its shape, or a device SimHub no longer has. The second is a failure
        /// rather than a write, because installing takes the strip's copy out of every device first, and
        /// running it there would remove a strip that still lights.
        /// </summary>
        public static FlagBoxPlan Unwritable(bool embedded, bool deviceFound)
        {
            if (!embedded) return new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
            if (!deviceFound) return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            return null;
        }

        /// <summary>
        /// Every strip's plan after a press, which repaints every strip row: what the write reported for a
        /// strip it could not bring up to date, so its row says so, and what SimHub now holds for the rest.
        /// Each row is its own strip's, never its shape's: one plan per shape once made a healthy strip read
        /// "Install failed" when another strip of that shape could not be written.
        /// </summary>
        /// <param name="census">What SimHub holds for each strip, read after the writes.</param>
        /// <param name="reachable">Whether that read reached SimHub's LED settings.</param>
        /// <param name="written">What each write reported, by <see cref="StripKey"/>.</param>
        public static IDictionary<string, FlagBoxPlan> AfterWrite(IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> census, bool reachable, IDictionary<string, FlagBoxPlan> written)
        {
            var plans = new Dictionary<string, FlagBoxPlan>(StringComparer.Ordinal);
            foreach (var entry in census ?? Enumerable.Empty<KeyValuePair<LedBar, FlagBoxPlan>>())
            {
                if (entry.Key == null) continue;
                var key = StripKey(entry.Key);
                FlagBoxPlan result = null;
                if (!reachable) plans[key] = new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
                else if (written != null && written.TryGetValue(key, out result) && result != null && result.State != FlagBoxInstallState.UpToDate) plans[key] = result;
                else plans[key] = entry.Value;
            }
            return plans;
        }

        /// <summary>The flag box profile too, but only on a rig with a matrix: the table draws its row only
        /// there, and a run must not report a row the page does not show.</summary>
        public static bool BringsFlagBoxForward(bool hasMatrix, FlagBoxInstallState state)
        {
            return hasMatrix && BringsForward(state);
        }

        /// <summary>
        /// What Reinstall everything says when it has run: the dashboards it wrote and left alone and the step
        /// OpenDash does not take for them (voice.md), then the light profiles it brought forward, each install
        /// followed by the select step installing does not take, then what could not be written and where to
        /// look. No dashboards clause where there was no dashboard to write.
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
            if (replaced > 0)
            {
                line.Append(replaced == 1 ? " Reinstalled 1 dashboard." : " Reinstalled " + replaced + " dashboards.");
                if (held == 1) line.Append(" The one you edited was left alone.");
                else if (held > 1) line.Append(" The " + held + " you edited were left alone.");
                // Straight after the dashboards, so what it points at is what the line has just named.
                line.Append(" " + ToSee(replaced, wroteFonts));
            }
            else if (held == 1) line.Append(" The dashboard you edited was left alone.");
            else if (held > 1) line.Append(" The " + held + " dashboards you edited were left alone.");
            Profiles(line, "Updated", lights.StripsUpdated);
            if (lights.StripsInstalled > 0)
            {
                Profiles(line, "Installed", lights.StripsInstalled);
                line.Append(" " + (lights.StripsInstalled == 1 ? StripSelect : StripsSelect));
            }
            var name = string.IsNullOrWhiteSpace(flagBoxName) ? FlagBoxProfile.ProfileName : flagBoxName;
            if (lights.FlagBoxAfter == FlagBoxInstallState.UpToDate)
            {
                line.Append(lights.FlagBoxWasOlder ? " Updated " + name + "." : " Installed " + name + ". " + FlagBoxSelect);
            }
            NotWritten(line, lights.StripsNotUpdated, "updated");
            NotWritten(line, lights.StripsNotInstalled, "installed");
            if (lights.FlagBoxAfter.HasValue && lights.FlagBoxAfter != FlagBoxInstallState.UpToDate)
            {
                line.Append(" " + name + " could not be " + (lights.FlagBoxWasOlder ? "updated." : "installed."));
            }
            if (!lights.Ok) line.Append(" See SimHub's log.");
            return line.Length == 0 ? NothingToReinstall : line.ToString(1, line.Length - 1);
        }

        /// <summary>A run that wrote nothing and had nothing to leave alone: a rig with no dashboard and every
        /// light profile current.</summary>
        public const string NothingToReinstall = "There was nothing to reinstall.";

        /// <summary>
        /// The step OpenDash does not take after writing <paramref name="count"/> dashboards: UpdateWording's
        /// for one, and the plural for several, so "it" never points at a dashboard the line has not named.
        /// </summary>
        public static string ToSee(int count, bool wroteFonts)
        {
            if (count <= 1) return UpdateWording.ToSee(wroteFonts);
            return wroteFonts ? RestartToSeeThem : ReopenThem;
        }

        public const string ReopenThem = "Close and reopen your dashboards to see them.";
        public const string RestartToSeeThem = "Restart SimHub to see them.";

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

        /// <summary>What Reinstall everything says when the dashboards could not be written. The reason is in
        /// SimHub's log, and the line says so rather than repeating it (voice.md).</summary>
        public const string ReinstallFailed = "The reinstall did not finish. See SimHub's log.";

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
            return "Your edited " + And(list) + (list.Count == 1 ? " was kept" : " were kept");
        }

        /// <summary>"Rim", "Rim and Pit wall", "Rim, Pit wall and Main dash".</summary>
        private static string And(IList<string> names)
        {
            if (names.Count <= 1) return names.Count == 0 ? string.Empty : names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
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

        /// <summary>
        /// What Put mine back says when it has run: what it restored and the step after, then each copy it
        /// could not restore and where to look. "Nothing to put back" only when it found no copy at all.
        /// </summary>
        /// <param name="restored">Copies put back.</param>
        /// <param name="failed">The names, as SimHub lists them, of the dashboards whose copy could not be put back.</param>
        public static string PutBack(int restored, IList<string> failed = null)
        {
            var names = (failed ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var line = new List<string>();
            if (restored > 0) line.Add("Put back " + (restored == 1 ? "1 dashboard" : restored + " dashboards") + ". " + ToSee(restored, false));
            if (names.Count > 0) line.Add("Could not put back " + And(names) + ". See SimHub's log.");
            return line.Count == 0 ? "There was nothing to put back." : string.Join(" ", line);
        }

        // --- Support --------------------------------------------------------------------------------

        public const string SupportTitle = "Support";
        public const string CopyReport = "Copy a support report";
        public const string OpenLog = "Open the log";
        public const string ReportIssue = "Report an issue";
        public const string ReadGuide = "Read the guide";
        public const string SupportCaption = "Versions, devices and the last 200 log lines, copied to paste. Nothing is sent.";
        public const string Licence = "MIT licence";

        /// <summary>
        /// The Support presses that carry the NEW tag, drawn for one release. The artboard tags Copy a support
        /// report; Open the log is new in the same release, as no press opened SimHub's log before this page,
        /// and the one-release rule marks every row unreleased since the last cut, as Delta precision and
        /// Clock are.
        /// </summary>
        public static readonly IReadOnlyList<string> NewTagged = new[] { CopyReport, OpenLog };

        public static bool IsNew(string label)
        {
            return NewTagged.Contains(label);
        }

        public const double SupportButtonGap = 8;
        public const double SupportCaptionSize = 12;

        public const string ReportCopied = "Support report copied. Paste it into your issue.";

        /// <summary>The clipboard would not take the report: another program holding it open is the usual
        /// reason, and it passes.</summary>
        public const string ReportFailed = "Could not copy the support report. Try again.";

        /// <summary>The report could not be put together at all; the reason is in SimHub's log.</summary>
        public const string ReportNotWritten = "Could not write the support report. See SimHub's log.";

        public const string LogMissing = "SimHub has not written a log yet.";

        /// <summary>Open the log could not open the folder, named rather than the exception's words.</summary>
        public static string LogFailed(string folder)
        {
            return "Could not open " + folder + ".";
        }

        // --- The support report's lines -----------------------------------------------------------------

        /// <summary>A screen as the report names it: "Face, 1280 × 480".</summary>
        public static string ScreenDetail(string kind, int width, int height)
        {
            return PanelAddScreen.KindName(kind) + ", " + width + " × " + height;
        }

        /// <summary>A strip as the report names it: "3/9/3 Fanatec on fanatec-1", or the shape alone when it
        /// names no device.</summary>
        public static string StripDetail(string shape, string device)
        {
            return (shape ?? string.Empty) + (string.IsNullOrWhiteSpace(device) ? string.Empty : " on " + device.Trim());
        }

        /// <summary>A matrix as the report names it: "Matrix 1".</summary>
        public static string MatrixName(int matrix)
        {
            return "Matrix " + matrix.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Lovely Car Data as the report says it: CarLightService.Status, which already carries the count and
        /// its age ("412 cars, updated 3 days ago"), and the date it was fetched, which the age alone does not
        /// pin down in a report read later.
        /// </summary>
        public static string CarTables(string status, DateTime? fetched)
        {
            return (string.IsNullOrWhiteSpace(status) ? "unknown" : status.Trim())
                + (fetched.HasValue ? " (fetched " + fetched.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")" : string.Empty);
        }

        /// <summary>SimHub's executable, under its own folder, whose file version is the report's SimHub version.</summary>
        public const string SimHubExe = "SimHubWPF.exe";

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
            text.AppendLine("Lovely Car Data: " + Known(input.CarTables));
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
