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
    /// What a press that writes light profiles did, strip by strip and by name: a strip rewritten and a strip
    /// that could not be are two strips, whatever shape they share, and each is said by the name SimHub lists
    /// its profile under. Reinstall everything fills it, and so does a light row's Update.
    /// </summary>
    public sealed class UpdatesLightsTally
    {
        private readonly List<string> updated = new List<string>();
        private readonly List<KeyValuePair<string, string>> installed = new List<KeyValuePair<string, string>>();
        private readonly List<string> notUpdated = new List<string>();
        private readonly List<string> notInstalled = new List<string>();
        private readonly List<string> noDevice = new List<string>();
        private readonly List<string> notedOn = new List<string>();

        /// <summary>Older strip profiles brought to this build's version, by name.</summary>
        public IReadOnlyList<string> Updated => updated;

        /// <summary>Missing strip profiles installed, each with the name of the SimHub device it went to (null
        /// when SimHub gives none), which the select step names.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Installed => installed;

        public IReadOnlyList<string> NotUpdated => notUpdated;
        public IReadOnlyList<string> NotInstalled => notInstalled;

        /// <summary>Strips the press left alone because SimHub does not list the device each names: installing
        /// takes the strip's copy out of every device first, so writing one there would remove a strip that
        /// still lights.</summary>
        public IReadOnlyList<string> NoDevice => noDevice;

        public int StripsUpdated => updated.Count;
        public int StripsInstalled => installed.Count;
        public int StripsNotUpdated => notUpdated.Count;
        public int StripsNotInstalled => notInstalled.Count;

        /// <summary>What the flag box profile was left in, or null when the run did not write it.</summary>
        public FlagBoxInstallState? FlagBoxAfter { get; private set; }

        /// <summary>Whether the flag box profile the run wrote was an older one rather than a missing one.</summary>
        public bool FlagBoxWasOlder { get; private set; }

        /// <summary>What an install reported beside its state (FlagBoxPlan.Note): a device listing only its
        /// maker's profiles, which the driver has to change before the select step can be taken.</summary>
        public string Note { get; private set; }

        /// <summary>The SimHub devices whose install reported <see cref="Note"/>, by the name the select step
        /// gives each (null where SimHub gives none), so a run that names several devices can say which one the
        /// note is about.</summary>
        public IReadOnlyList<string> NotedOn => notedOn;

        /// <summary>Whether the flag box's install reported <see cref="Note"/>: its device is the matrix's.</summary>
        public bool FlagBoxNoted { get; private set; }

        /// <summary>One strip the run wrote: its name, the SimHub device it names, what SimHub held before,
        /// and what the write reported.</summary>
        public void Strip(string name, string device, FlagBoxInstallState before, FlagBoxPlan after)
        {
            var older = before == FlagBoxInstallState.Outdated;
            if (after != null && after.State == FlagBoxInstallState.UpToDate)
            {
                if (older) updated.Add(name);
                else installed.Add(new KeyValuePair<string, string>(name, device));
                if (Noted(after) && !notedOn.Contains(device)) notedOn.Add(device);
            }
            else if (older) notUpdated.Add(name);
            else notInstalled.Add(name);
        }

        /// <summary>A strip the run picked and did not write, because SimHub does not list its device.</summary>
        public void DeviceNotListed(string name)
        {
            noDevice.Add(name);
        }

        public void FlagBox(FlagBoxInstallState before, FlagBoxPlan after)
        {
            FlagBoxWasOlder = before == FlagBoxInstallState.Outdated;
            FlagBoxAfter = after == null ? FlagBoxInstallState.Failed : after.State;
            if (FlagBoxAfter == FlagBoxInstallState.UpToDate && Noted(after)) FlagBoxNoted = true;
        }

        /// <summary>Keeps the first install's note, and says whether this install reported the same one.</summary>
        private bool Noted(FlagBoxPlan after)
        {
            if (string.IsNullOrWhiteSpace(after.Note)) return false;
            if (Note == null) Note = after.Note;
            return string.Equals(Note, after.Note, StringComparison.Ordinal);
        }

        /// <summary>Whether a write failed, whose reason is in SimHub's log.</summary>
        public bool Failed
        {
            get
            {
                return notUpdated.Count > 0 || notInstalled.Count > 0
                    || (FlagBoxAfter.HasValue && FlagBoxAfter != FlagBoxInstallState.UpToDate);
            }
        }

        /// <summary>Whether that run is said in the ordinary ink: nothing it tried failed, nothing it picked was
        /// left for want of a device, and nothing stands between an install and its use.</summary>
        public bool Ok
        {
            get { return !Failed && noDevice.Count == 0 && Note == null; }
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

        /// <summary>Under the heading of an offer: what taking it will cost, said as a fact. Nothing is downloaded
        /// yet, so restarting now finishes nothing; "Restart SimHub to finish updating" is the next state's step
        /// (the Staged card's UpdateWording.RestartLater, Home's restart issue), never the offer's. OpenDash has
        /// no presets.</summary>
        public const string RestartNote = "Needs a SimHub restart. Your settings are kept.";

        public const string ReleaseNotesTitle = "Release notes";

        /// <summary>The card foot's heading over the release's opening sentence, or null when there is none: a
        /// remembered offer (UpdateMark.Opening) carries no notes, and a heading over nothing but the link is
        /// not drawn.</summary>
        public static string NotesHeading(string summary)
        {
            return string.IsNullOrWhiteSpace(summary) ? null : ReleaseNotesTitle;
        }
        public const string EveryRelease = "Every release on GitHub";

        /// <summary>What the page says of a download that threw, in the shape of Reinstall everything's failure
        /// (ReinstallFailed) and voice.md's "Install failed. See SimHub's log.": the exception is in the log,
        /// and the line points there.</summary>
        public const string UpdateFailed = "The update did not finish. See SimHub's log.";

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

        /// <summary>The sentence under the card's heading in each state, or null for none. A download has none:
        /// the heading names the release and the bar's head says the run and its percent (<see cref="Downloading"/>),
        /// so a caption between them said the run twice in two verbs and the version twice.</summary>
        public static string CardNote(UpdatesCard card, string version)
        {
            switch (card)
            {
                case UpdatesCard.Available: return RestartNote;
                case UpdatesCard.Staged: return UpdateWording.RestartLater;
                default: return null;
            }
        }

        /// <summary>
        /// The head word of a download's bar: the verb its press and its line use ("Download", "OpenDash itself
        /// was downloaded", "OpenDash is downloaded"), where the shared bar's PanelCopy.Installing gave one run
        /// two verbs on one card (voice.md: one word per thing).
        /// </summary>
        public const string Downloading = "Downloading";

        /// <summary>The card's padding and rhythm, from the artboard's inline styles.</summary>
        public const double CardPaddingX = 20;
        public const double CardPaddingY = 18;
        public const double CardGap = 18;
        public const double CardTextGap = 6;
        public const double CardHeadingGap = 12;
        public const double CardVersionSize = 15;

        /// <summary>"You have" and its numeral 4 apart, the pair set 1 above the heading's bottom so the words
        /// sit on the heading's baseline.</summary>
        public const double YouHaveGap = 4;
        public const double YouHaveBaseline = 1;
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

        /// <summary>The measure of the check row's two lines under its caption: when the last check answered,
        /// and checking or the answer to a press.</summary>
        public const double CheckLineWidth = 520;

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

        /// <summary>A rig dashboard whose folder has gone from DashTemplates, in the ruled word the Screens card
        /// and Home use for the same state (ambiguity 27, PanelScreens.Missing), so the panel says it one way.
        /// Held here as this table's own literal rather than read from PanelScreens, whose constants are that
        /// page's to change; "from SimHub" would repeat the section's own heading.</summary>
        public const string Missing = "Missing";

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

        /// <summary>
        /// What follows a row's kind in its name cell when the version column has gone: " · 0.5.0", so the
        /// version SimHub holds stays on the row at every width (a column that cannot fit stacks, and never
        /// vanishes), and nothing while the column shows it or there is no version.
        /// </summary>
        public static string VersionInName(double versionWidth, string version)
        {
            return versionWidth > 0 || string.IsNullOrWhiteSpace(version) ? string.Empty : " · " + version.Trim();
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

        /// <summary>
        /// What the state cell leaves its words after the dot and its gap, 150 - 7 - 8 = 135: every state the
        /// table writes fits on one line in it, measured in Barlow 13 (PanelUpdatesTests).
        /// </summary>
        public const double TableStateRoom = TableStateWidth - TableDot - TableDotGap;

        /// <summary>The sections' rhythm on this artboard: 12 under a heading and between what follows it
        /// (the table, its notes, the Reinstall row; the Support presses and their caption), with 14 between
        /// Reinstall everything and its line.</summary>
        public const double SectionGap = 12;
        public const double ReinstallLineGap = 14;

        /// <summary>Whether the table draws the flag box's by-hand route under it: only while SimHub's matrix
        /// settings cannot be reached at all, where the one-click route is not there (ADR 0013).</summary>
        public static bool ShowsImportFallback(FlagBoxPlan flagBoxPlan)
        {
            return flagBoxPlan != null && flagBoxPlan.State == FlagBoxInstallState.Unavailable;
        }

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

        /// <summary>
        /// The widest content width the page draws anything differently at, and so the most of it the build
        /// reads (ContentWidthUpTo): <see cref="VersionWidth"/>'s threshold with a press, 2 + 32 + 126 + 166 +
        /// 80 + 154 = 560, the highest of the page's three width decisions (the other two are the version
        /// column's 480 without a press and <see cref="ButtonBesideFrom"/>'s 520). Past it the page is drawn
        /// the same at every width, so a resize there leaves the page alone -- its question, the by-hand
        /// route's line and the disk it read -- rather than rebuilding the panel's heaviest build on SimHub's
        /// interface thread for a layout that has not changed.
        /// </summary>
        public const double WidthDrawnUpTo = TableBorder + 2 * TableRowPaddingX + TableVersionWidth + TableGap + TableStateWidth + TableGap
            + TablePressColumn + TableNameMin;

        /// <summary>Whether any light row draws its Update press, which takes the press column's room from
        /// every row: a profile older than this build's, on a device SimHub lists.</summary>
        public static bool TableHasPress(IEnumerable<UpdatesRow> lightRows)
        {
            return (lightRows ?? Enumerable.Empty<UpdatesRow>()).Any(row => row != null && row.OffersUpdate);
        }

        /// <summary>
        /// Which light row a route to AnchorLights lands on (Home's strip-outdated fix goes to LEDs since #523): the
        /// first whose Update press is the fix, or -1 for the table itself when no row offers one.
        /// </summary>
        public static int LightsAnchor(IList<UpdatesRow> lightRows)
        {
            if (lightRows == null) return -1;
            for (var i = 0; i < lightRows.Count; i++)
            {
                if (lightRows[i] != null && lightRows[i].OffersUpdate) return i;
            }
            return -1;
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
        /// state words are held here (<see cref="Missing"/>), except the restart's, which is the panel's one
        /// phrase for that state (PanelCopy.RestartToLoad, #524 ruling 1) and wraps to a second line in the cell. A screen this build ships nothing for is said from the facts alone -- installed
        /// in the installed ink, as the Screens card draws it, or "Unknown" while the facts are not read --
        /// with a tooltip that says why no press here changes it. A hover never repeats the state beside it.
        /// </remarks>
        public static UpdatesRow DashboardRow(ScreenInstance screen, PackageStatus package, bool? installed, bool? waitsForRestart)
        {
            var name = screen == null ? string.Empty : screen.Name;
            var version = VersionText(package == null ? null : package.InstalledVersion);
            if (package == null)
            {
                var shipsNo = ShipsNo(screen);
                if (installed == false) return Row(name, DashboardKind, string.Empty, Missing, Theme.StatusFailed, shipsNo);
                if (installed == true) return Row(name, DashboardKind, string.Empty, PanelCopy.Installed, Theme.StatusUpToDate, shipsNo);
                return Row(name, DashboardKind, string.Empty, Unknown, Theme.TextLabel, shipsNo);
            }
            if (package.Status == InstallStatus.Failed)
            {
                return Row(name, DashboardKind, version, PanelCopy.InstallFailed, Theme.StatusFailed, SeeLog);
            }
            if (installed == false)
            {
                return Row(name, DashboardKind, string.Empty, Missing, Theme.StatusFailed, MissingTooltip);
            }
            if (waitsForRestart == true)
            {
                return Row(name, DashboardKind, version, PanelCopy.RestartToLoad, Theme.Caution, AfterRestart(name));
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

        /// <summary>A missing dashboard's tooltip: the press that puts it back, since the state beside it
        /// already says it is missing.</summary>
        public const string MissingTooltip = PanelConfirmation.ReinstallLabel + " installs it again.";

        /// <summary>A dashboard SimHub has not loaded yet: the step after the restart the state already names, in
        /// the words the Screens fix box and Home's issue use for it (PanelScreens.RestartDetail).</summary>
        public static string AfterRestart(string name)
        {
            return PanelScreens.RestartDetail(name);
        }

        public const string NotInstalledTooltip = PanelConfirmation.ReinstallLabel + " installs it.";

        /// <summary>"This build ships no 1280 × 480 face." for a screen whose package this build does not
        /// carry. A screen whose size is not known (a migrated one whose folder spells none, which
        /// RepairScreenSizes cannot match) leaves the size out rather than say "0 × 0", as the Screens card
        /// does. A slots screen is named as the Screens page names it: a square one is a "round screen" and
        /// any other a "card face" (PanelScreens.KindOf, CardsCaption), never a third name for the kind.</summary>
        /// <remarks>
        /// The rectangular case is the common one: OpenDash.csproj keeps "OpenDash slots *.simhubdash" out of
        /// the embedded packages, so a migrated "OpenDash slots 1280x480" screen has no installer entry on any
        /// release build and its row always carries this hover.
        /// </remarks>
        public static string ShipsNo(ScreenInstance screen)
        {
            if (screen == null) return NoDashboards;
            var size = screen.Width > 0 && screen.Height > 0 ? screen.SizeLabel + " " : string.Empty;
            return "This build ships no " + size + KindNoun(screen) + ".";
        }

        /// <summary>A slots screen in a sentence where it is round, as the Screens page writes it in prose.</summary>
        public const string RoundScreen = "round screen";

        /// <summary>A slots screen in a sentence where it is not round, PanelScreens.CardFace's words.</summary>
        public const string CardFace = "card face";

        /// <summary>A screen's kind as a noun in a sentence: "face", "round screen", "card face", "companion",
        /// "pit wall". Round is the Screens page's test (PanelScreens.IsRound): a slots screen of a known,
        /// square size; held here as this page's own literals, since the Screens page's are its own to change.</summary>
        private static string KindNoun(ScreenInstance screen)
        {
            if (screen.IsSlots) return screen.Width > 0 && screen.Width == screen.Height ? RoundScreen : CardFace;
            return PanelAddScreen.KindName(screen.Kind).ToLowerInvariant();
        }

        /// <summary>An edited dashboard's hover: the fact, then the press, as every row hover on the page says
        /// it. What the installer does with an edited folder is its mechanism, not the driver's (voice.md).</summary>
        public const string EditedTooltip = "You have edited it. " + PanelConfirmation.ReinstallLabel + " replaces it.";

        /// <summary>An older dashboard's tooltip, naming the version the row does not show: "Reinstall
        /// everything brings it to 0.5.0."</summary>
        public static string BringsItTo(string embeddedVersion)
        {
            return string.IsNullOrWhiteSpace(embeddedVersion)
                ? PanelConfirmation.ReinstallLabel + " brings it up to date."
                : PanelConfirmation.ReinstallLabel + " brings it to " + embeddedVersion.Trim() + ".";
        }

        /// <summary>
        /// A strip's row: its name, the version of its profile in SimHub, and the state PanelCopy.StripRow says,
        /// with an Update press while that profile is older than this build's and SimHub lists the strip's
        /// device. Where it does not, the press could only fail, so the row offers none and its hover says the
        /// step, as the LEDs page does; the state still says what SimHub holds.
        /// </summary>
        /// <param name="plan">The strip's plan from <see cref="StripPlans"/>.</param>
        /// <param name="deviceListed">Whether SimHub lists the device the strip names (<see cref="DeviceListed"/>).</param>
        public static UpdatesRow StripRow(string name, FlagBoxPlan plan, bool deviceListed = true)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            var noDevice = !deviceListed && NeedsDevice(state);
            return LightRow(name, StripKind, plan, noDevice ? DeviceNotListed : StripTooltip(plan), state == FlagBoxInstallState.Outdated && !noDevice);
        }

        /// <summary>The states a press would write a strip from, which it can only do on a device SimHub lists.</summary>
        private static bool NeedsDevice(FlagBoxInstallState state)
        {
            return state == FlagBoxInstallState.Outdated || state == FlagBoxInstallState.NotInstalled || state == FlagBoxInstallState.Failed;
        }

        /// <summary>A strip whose SimHub device is not listed, in the LEDs page's one phrase for a device SimHub
        /// lacks (PanelLeds.DeviceNotListed, whose picker names the state "Device not in SimHub"), with the page
        /// named, since the field it points at is not on this one.</summary>
        public const string DeviceNotListed = "This strip's device is not in SimHub. Choose one under SimHub device on the LEDs page.";

        /// <summary>
        /// The strips' plans as the table and the presses read them: Unavailable for every strip while SimHub's
        /// LED settings cannot be read, and NotEmbedded for a strip whose shape this build carries no profile
        /// for, whatever SimHub holds.
        /// </summary>
        /// <remarks>
        /// The census compares SimHub's copy with no embedded version at all for such a strip, so it calls a
        /// stamped copy older and an unstamped one current: rc.2 built brow-N profiles that no later build
        /// carries, and those strips read "Update available" with a press that could write nothing. The
        /// version SimHub holds is kept, since the table's column says it truly.
        /// </remarks>
        /// <param name="census">What SimHub holds for each strip (BarCensus).</param>
        /// <param name="reachable">Whether that read reached SimHub's LED settings.</param>
        /// <param name="embeddedShapeIds">The shape ids this build carries a profile for, of the rig's strips.</param>
        public static IList<KeyValuePair<LedBar, FlagBoxPlan>> StripPlans(IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> census, bool reachable, ICollection<string> embeddedShapeIds)
        {
            var plans = new List<KeyValuePair<LedBar, FlagBoxPlan>>();
            foreach (var entry in census ?? Enumerable.Empty<KeyValuePair<LedBar, FlagBoxPlan>>())
            {
                if (entry.Key == null) continue;
                FlagBoxPlan plan;
                if (!reachable) plan = new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
                else if (entry.Key.ProfileShapeId == null || embeddedShapeIds == null || !embeddedShapeIds.Contains(entry.Key.ProfileShapeId))
                    plan = new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded, InstalledVersion = entry.Value == null ? null : entry.Value.InstalledVersion };
                else plan = entry.Value;
                plans.Add(new KeyValuePair<LedBar, FlagBoxPlan>(entry.Key, plan));
            }
            return plans;
        }

        /// <summary>Whether SimHub lists the device a strip names, off one read of SimHub's LED devices: their
        /// names by id (LedTargets.All).</summary>
        public static bool DeviceListed(IDictionary<string, string> devices, LedBar bar)
        {
            return bar != null && devices != null && devices.ContainsKey(LedBar.NormaliseDevice(bar.Device) ?? string.Empty);
        }

        /// <summary>The name SimHub shows for the device a strip names, or null when it lists none.</summary>
        public static string DeviceName(IDictionary<string, string> devices, LedBar bar)
        {
            string name;
            return bar != null && devices != null && devices.TryGetValue(LedBar.NormaliseDevice(bar.Device) ?? string.Empty, out name) ? name : null;
        }

        /// <summary>Whether the table draws the flag box profile's row: whenever the rig has a matrix (ruling
        /// 69). A build that carries no profile still draws it, reading "Unknown" with the hover "This build
        /// ships no flag box profile." (FlagBoxRow), as a strip whose profile the build lacks is drawn, so a
        /// matrix never goes missing from the table without a word. Only the writes need the profile, and
        /// they check for it themselves (UpdatesBringLightsForward).</summary>
        public static bool DrawsFlagBoxRow(bool hasMatrix)
        {
            return hasMatrix;
        }

        /// <summary>The flag box profile's row, drawn when the rig has a matrix.</summary>
        /// <param name="extractedPath">Where OpenDash left the profile, which the tooltip names while SimHub's
        /// matrix settings cannot be reached and the by-hand route under the table is the way in.</param>
        public static UpdatesRow FlagBoxRow(string name, FlagBoxPlan plan, string extractedPath)
        {
            return LightRow(name, MatrixKind, plan, FlagBoxTooltip(plan, extractedPath), plan != null && plan.State == FlagBoxInstallState.Outdated);
        }

        /// <summary>
        /// A light profile's row, in PanelCopy.LightRow's words, with its dot from the same words: the Matrix
        /// page's pill reads PanelLightRows.DotHex beside words of its own (PanelMatrix.ProfileRow), so this
        /// table does not take its dot from there.
        /// </summary>
        private static UpdatesRow LightRow(string name, string kind, FlagBoxPlan plan, string tooltip, bool offersUpdate)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            var version = plan == null ? null : plan.InstalledVersion;
            var words = kind == StripKind ? PanelCopy.StripRow(state, version) : PanelCopy.LightRow(state, version);
            return new UpdatesRow(name, kind, VersionText(plan == null ? null : plan.InstalledVersion), words.State, words.StateHex,
                Dot(words.StateHex), tooltip, offersUpdate);
        }

        /// <summary>
        /// A strip row's tooltip in each state, or null where the page already says it all (voice.md): an
        /// up-to-date profile, whose version has a column of its own, has none, as a dashboard's row has none,
        /// and neither has one SimHub's LED settings hide, since the note under the table says why
        /// (PanelLightRows.Unavailable, TableNotes). An older profile names the version its Update brings, in
        /// the dashboard rows' form (BringsItTo), and leaves what the press costs to the press's own tooltip; a
        /// missing one names the press on this page that installs it, whose own line then gives the select
        /// step in the page's one form.
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
                    return null;
                case FlagBoxInstallState.NotEmbedded:
                    return StripNotEmbedded;
                default:
                    return LightFailed;
            }
        }

        /// <summary>A row is one strip of the rig, so the census rows' "no strip of this shape" does not
        /// apply to it: the strip is there and its profile is not, which the row's state already says. The
        /// hover names the press, as a missing dashboard's does; the select step is said once, in its one form
        /// with the strip's name and device ('Select "Rim" on Fanatec in SimHub to use it.'), by the line the
        /// press writes (ReinstallSummary), since the row names no device for "its device" to point at.</summary>
        public const string StripNotInstalled = NotInstalledTooltip;

        /// <summary>A strip whose shape this build carries no profile for, whatever SimHub holds, in the LEDs
        /// page's words for it (PanelLeds.NoProfileForStrip).</summary>
        public const string StripNotEmbedded = "This build ships no profile for this strip.";

        /// <summary>A failed row's hover: the state already says it failed, and this says where to look.</summary>
        public const string LightFailed = SeeLog;

        public const string SeeLog = "See SimHub's log.";

        /// <summary>An older light profile's tooltip, naming the version the row's Update brings: "Update
        /// brings it to 0.5.0."</summary>
        public static string UpdateBringsItTo(string embeddedVersion)
        {
            return string.IsNullOrWhiteSpace(embeddedVersion)
                ? RowUpdate + " brings it up to date."
                : RowUpdate + " brings it to " + embeddedVersion.Trim() + ".";
        }

        /// <summary>
        /// The flag box row's tooltip in each state. An older profile is said as a strip's is, a missing one
        /// names the press on this page, and a current one has none, as a current strip's has none. A failed
        /// one says where to look. One SimHub's matrix settings hide has none: the by-hand route under the
        /// table prints FlagBoxInstallPlan.Summary's sentence for it, and the hover would repeat it. The rest,
        /// a build with no profile among them, are FlagBoxInstallPlan.Summary's.
        /// </summary>
        public static string FlagBoxTooltip(FlagBoxPlan plan, string extractedPath)
        {
            var state = plan == null ? FlagBoxInstallState.NotInstalled : plan.State;
            switch (state)
            {
                case FlagBoxInstallState.NotInstalled:
                    return FlagBoxNotInstalled;
                case FlagBoxInstallState.UpToDate:
                    return null;
                case FlagBoxInstallState.Outdated:
                    return UpdateBringsItTo(plan.EmbeddedVersion);
                case FlagBoxInstallState.Failed:
                    return LightFailed;
                case FlagBoxInstallState.Unavailable:
                    return null;
                default:
                    return FlagBoxInstallPlan.Summary(plan, extractedPath);
            }
        }

        /// <summary>A missing flag box profile's hover: the press on this page that installs it, as a strip's
        /// says. The select step, with the matrix's content number, is said once, in the Matrix page's form,
        /// by the line the press writes (FlagBoxSelect).</summary>
        public const string FlagBoxNotInstalled = NotInstalledTooltip;

        /// <summary>Where the flag box's select step is taken, as the Matrix page says it: the device that
        /// draws a matrix, and on a rig with several, each one's.</summary>
        public const string MatrixDevice = "your matrix's device in SimHub";
        public const string EachMatrixDevice = "each matrix's device in SimHub";

        /// <summary>The device's field that picks which matrix it draws, spelt as SimHub labels it (ruling 69,
        /// PanelMatrix.ContentField).</summary>
        public const string MatrixContentField = "RGB Matrix content";

        /// <summary>
        /// The flag box's select step after it is installed, in the Matrix page's form (PanelMatrix.SelectStep):
        /// 'Select "OpenDash Flag box" on your matrix's device in SimHub and set RGB Matrix content to 2.', on
        /// each matrix's device with the matrix's number on a rig with several, and nothing on a rig with none,
        /// which has no device to name. Said apart from the strips' step, since a matrix's device also needs
        /// its content number, which a strip's does not.
        /// </summary>
        /// <param name="matrices">The rig's matrix numbers (OpenDashSettings.MatrixPanels).</param>
        public static string FlagBoxSelect(string name, IList<int> matrices)
        {
            if (matrices == null || matrices.Count == 0) return null;
            var quoted = "Select \"" + name + "\" on ";
            if (matrices.Count == 1) return quoted + MatrixDevice + " and set " + MatrixContentField + " to " + matrices[0].ToString(CultureInfo.InvariantCulture) + ".";
            return quoted + EachMatrixDevice + " and set " + MatrixContentField + " to the matrix's number.";
        }

        /// <summary>
        /// The step after profiles are installed, in the one form the LEDs page says it (PanelLeds.SelectIt):
        /// 'Select "Rim" on Fanatec in SimHub to use it.', and for several 'Select "Rim" on Fanatec and
        /// "OpenDash Flag box" in SimHub to use them.' Installing adds a profile to its device's list without
        /// selecting it, and each is named with the device it went to, since after the run every row reads
        /// "Up to date" and nothing on the page says which were installed. A device SimHub gives no name for is
        /// left out rather than guessed. The flag box's step is <see cref="FlagBoxSelect"/>'s.
        /// </summary>
        public static string SelectIt(IList<KeyValuePair<string, string>> installed)
        {
            var each = (installed ?? new KeyValuePair<string, string>[0])
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .Select(pair => "\"" + pair.Key.Trim() + "\"" + (string.IsNullOrWhiteSpace(pair.Value) ? string.Empty : " on " + pair.Value.Trim()))
                .ToList();
            if (each.Count == 0) return null;
            return "Select " + And(each) + " in SimHub to use " + (each.Count == 1 ? "it." : "them.");
        }

        /// <summary>The light row's one press, while its profile is older than this build's.</summary>
        public const string RowUpdate = "Update";

        /// <summary>A light row's Update press: what it costs, in the words the Matrix page's flag box Update
        /// warns with (FlagBoxInstallPlan.Replaces), and the LEDs page's strip Update too (#524, ruling 12). The
        /// row's hover names the version the press brings; this is the press's own.</summary>
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
        /// <paramref name="wanted"/> picks on a device SimHub lists, and none while SimHub's LED settings cannot
        /// be read. The census is <see cref="StripPlans"/>', so a strip this build has no profile for is
        /// NotEmbedded and neither rule picks it.
        /// </summary>
        /// <param name="listed">Whether SimHub lists a strip's device (<see cref="DeviceListed"/>).</param>
        public static IList<KeyValuePair<LedBar, FlagBoxPlan>> StripsToWrite(IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> census, bool reachable, Func<LedBar, FlagBoxInstallState, bool> wanted, Func<LedBar, bool> listed)
        {
            return Picked(census, reachable, wanted).Where(entry => listed == null || listed(entry.Key)).ToList();
        }

        /// <summary>
        /// The strips the rule picks that no press writes, because SimHub does not list the device each names:
        /// installing takes the strip's copy out of every device first, so writing one there would remove a
        /// strip that still lights. The run names them rather than calling them failures.
        /// </summary>
        public static IList<LedBar> StripsWithoutDevice(IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> census, bool reachable, Func<LedBar, FlagBoxInstallState, bool> wanted, Func<LedBar, bool> listed)
        {
            if (listed == null) return new List<LedBar>();
            return Picked(census, reachable, wanted).Where(entry => !listed(entry.Key)).Select(entry => entry.Key).ToList();
        }

        private static IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> Picked(IEnumerable<KeyValuePair<LedBar, FlagBoxPlan>> census, bool reachable, Func<LedBar, FlagBoxInstallState, bool> wanted)
        {
            if (!reachable || wanted == null) return Enumerable.Empty<KeyValuePair<LedBar, FlagBoxPlan>>();
            return (census ?? Enumerable.Empty<KeyValuePair<LedBar, FlagBoxPlan>>())
                .Where(entry => entry.Key != null && entry.Value != null && wanted(entry.Key, entry.Value.State))
                .ToList();
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
        /// OpenDash does not take for them (voice.md), then the light profiles (<see cref="LightsSaid"/>). No
        /// dashboards clause where there was no dashboard to write.
        /// </summary>
        /// <param name="replaced">Dashboards written.</param>
        /// <param name="held">Dashboards left alone because they were edited and nobody said to replace them.</param>
        /// <param name="wroteFonts">Whether a font went into DashFonts, which only a restart shows.</param>
        /// <param name="lights">What the run did to each light profile it wrote.</param>
        /// <param name="flagBoxName">The flag box profile's name in SimHub.</param>
        /// <param name="matrices">The rig's matrix numbers, which the flag box's select step names.</param>
        public static string ReinstallSummary(int replaced, int held, bool wroteFonts, UpdatesLightsTally lights, string flagBoxName, IList<int> matrices)
        {
            var line = new List<string>();
            if (replaced > 0)
            {
                line.Add(replaced == 1 ? "Reinstalled 1 dashboard." : "Reinstalled " + replaced + " dashboards.");
                if (held == 1) line.Add("The one you edited was left alone.");
                else if (held > 1) line.Add("The " + held + " you edited were left alone.");
                // Straight after the dashboards, so what it points at is what the line has just named.
                line.Add(ToSee(replaced, wroteFonts));
            }
            else if (held == 1) line.Add("The dashboard you edited was left alone.");
            else if (held > 1) line.Add("The " + held + " dashboards you edited were left alone.");
            var profiles = LightsSaid(lights, flagBoxName, matrices);
            if (profiles != null) line.Add(profiles);
            return line.Count == 0 ? NothingToReinstall : string.Join(" ", line);
        }

        /// <summary>
        /// What a press that wrote light profiles says, or null when it wrote none and left none out: each
        /// profile by the name SimHub lists it under, in the LEDs and Matrix pages' words ("Updated Rim's
        /// profile.", "Installed OpenDash Flag box."), then what an install reported about the device, then the
        /// select step, since the note has to be acted on before the step can be taken (voice.md: the steps
        /// OpenDash does not take, in the order they are done), then what could not be written and where to
        /// look, and last each strip left out for want of a device. A light row's Update says it too.
        /// </summary>
        public static string LightsSaid(UpdatesLightsTally lights, string flagBoxName, IList<int> matrices)
        {
            if (lights == null) return null;
            var name = string.IsNullOrWhiteSpace(flagBoxName) ? FlagBoxProfile.ProfileName : flagBoxName.Trim();
            var flagBoxOk = lights.FlagBoxAfter == FlagBoxInstallState.UpToDate;
            var line = new List<string>();
            Profiles(line, "Updated", lights.Updated);
            Profiles(line, "Installed", lights.Installed.Select(pair => pair.Key).ToList());
            if (flagBoxOk) line.Add((lights.FlagBoxWasOlder ? "Updated " : "Installed ") + name + ".");
            var step = SelectIt(lights.Installed.ToList());
            var flagBoxStep = flagBoxOk && !lights.FlagBoxWasOlder ? FlagBoxSelect(name, matrices) : null;
            if (lights.Note != null) line.Add(NoteSaid(lights, flagBoxStep != null));
            if (step != null) line.Add(step);
            if (flagBoxStep != null) line.Add(flagBoxStep);
            Profiles(line, "Could not update", lights.NotUpdated);
            Profiles(line, "Could not install", lights.NotInstalled);
            if (lights.FlagBoxAfter.HasValue && !flagBoxOk) line.Add((lights.FlagBoxWasOlder ? "Could not update " : "Could not install ") + name + ".");
            if (lights.Failed) line.Add(SeeLog);
            var noDevice = NoDeviceSaid(lights.NoDevice);
            if (noDevice != null) line.Add(noDevice);
            return line.Count == 0 ? null : string.Join(" ", line);
        }

        /// <summary>
        /// What an install reported about its device, said so that it points at a device the line names. The
        /// LEDs and Matrix pages install one profile per press, so FlagBoxInstallPlan.BuiltInModeNote's "your
        /// device" is clear there; a run of this page's can name several devices in its select steps, and then
        /// the note names the one it is about (<see cref="BuiltInOn"/>). Said as reported when the line names
        /// one device or none, when the note is another, or when SimHub gives no name for a device it is about.
        /// </summary>
        /// <param name="flagBoxSelected">Whether the line gives the flag box's select step, which names the
        /// matrix's device.</param>
        public static string NoteSaid(UpdatesLightsTally lights, bool flagBoxSelected)
        {
            if (lights == null || lights.Note == null) return null;
            var named = lights.Installed.Select(pair => pair.Value).Where(device => !string.IsNullOrWhiteSpace(device))
                .Select(device => device.Trim()).Distinct(StringComparer.Ordinal).Count() + (flagBoxSelected ? 1 : 0);
            if (named <= 1 || !string.Equals(lights.Note, FlagBoxInstallPlan.BuiltInModeNote, StringComparison.Ordinal)) return lights.Note;
            if (lights.NotedOn.Any(string.IsNullOrWhiteSpace)) return lights.Note;
            var devices = lights.NotedOn.Select(device => device.Trim()).Distinct(StringComparer.Ordinal).ToList();
            if (lights.FlagBoxNoted) devices.Add(MatrixDeviceName);
            return devices.Count == 0 ? lights.Note : BuiltInOn(devices);
        }

        /// <summary>FlagBoxInstallPlan.BuiltInModeNote with its device named: "Turn off built-in profiles on
        /// Moza, or OpenDash's profiles will not be listed."</summary>
        public static string BuiltInOn(IList<string> devices)
        {
            return "Turn off built-in profiles on " + And(devices ?? new string[0]) + ", or OpenDash's profiles will not be listed.";
        }

        /// <summary>The matrix's device in a sentence that already says where it is.</summary>
        public const string MatrixDeviceName = "your matrix's device";

        /// <summary>"Updated Rim's profile.", "Updated the profiles of Rim and Brow.": the LEDs page's form for
        /// one, and its names for several.</summary>
        private static void Profiles(List<string> line, string verb, IEnumerable<string> names)
        {
            var list = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (list.Count == 1) line.Add(verb + " " + list[0] + "'s profile.");
            else if (list.Count > 1) line.Add(verb + " the profiles of " + And(list) + ".");
        }

        /// <summary>The strips a press left out because SimHub does not list the device each names, and the
        /// step, as the LEDs page says it (<see cref="DeviceNotListed"/>).</summary>
        public static string NoDeviceSaid(IEnumerable<string> names)
        {
            var list = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (list.Count == 0) return null;
            return list.Count == 1
                ? list[0] + "'s device is not in SimHub. Choose one under SimHub device on the LEDs page."
                : "The devices of " + And(list) + " are not in SimHub. Choose them under SimHub device on the LEDs page.";
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

        /// <summary>The kept card's heading, a noun as voice.md has a heading (#524, ruling 4): "Kept copy", or
        /// "Kept copies" for more than one.</summary>
        public static string KeptHeading(int count)
        {
            return count > 1 ? "Kept copies" : "Kept copy";
        }

        /// <summary>The kept card's first line: "Your edited Rim was kept.", "Your edited Rim and Pit wall were
        /// kept." (#524, ruling 4).</summary>
        public static string KeptClause(IList<string> names)
        {
            var list = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (list.Count == 0) return "Your edited dashboard was kept.";
            return "Your edited " + And(list) + (list.Count == 1 ? " was kept." : " were kept.");
        }

        /// <summary>The words under the kept card's heading: <see cref="KeptClause"/>, then <see cref="KeptCaption"/>.</summary>
        public static string KeptLine(IList<string> names)
        {
            var list = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            return KeptClause(list) + " " + KeptCaption(Math.Max(1, list.Count));
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
        /// Whether a folder is drawn on the kept card: whenever a copy kept from edited work is there to put
        /// back, as the artboard draws it (#524, ruling 5), whether or not the folder in SimHub has been edited
        /// since. The ordinary one-deep backup is not anybody's work and never shows.
        /// </summary>
        /// <remarks>
        /// The card used to hide a folder the driver had edited again, which is also what kept Put mine back from
        /// overwriting those edits. The card no longer does, so Put mine back asks the installer itself and
        /// leaves such a folder as it is (<see cref="PutsBack"/>), saying so by name (<see cref="PutBack"/>).
        /// </remarks>
        /// <param name="copies">PackageExtractor.KeptCopies for the folder.</param>
        public static bool ShowsKept(IEnumerable<string> copies)
        {
            return (copies ?? Enumerable.Empty<string>()).Any(path => path != null && path.Contains(PackageExtractor.EditedSuffix));
        }

        /// <summary>Whether Put mine back writes a folder the card shows: only one the installer does not find
        /// edited, since PackageExtractor.Restore keeps no copy of what it replaces and nothing has asked.</summary>
        /// <param name="edited">Whether the installer finds the folder in SimHub edited (PackageStatus.Edited).</param>
        public static bool PutsBack(bool edited)
        {
            return !edited;
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
        /// <param name="held">The names of the dashboards left as they are because they were edited since their
        /// copy was kept (<see cref="PutsBack"/>).</param>
        public static string PutBack(int restored, IList<string> failed = null, IList<string> held = null)
        {
            var names = (failed ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var kept = (held ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var line = new List<string>();
            if (restored > 0) line.Add("Put back " + (restored == 1 ? "1 dashboard" : restored + " dashboards") + ". " + ToSee(restored, false));
            if (kept.Count > 0) line.Add("Did not put back " + And(kept) + ", which you have edited since.");
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
        /// The presses that carry the NEW tag, drawn for one release. The artboard tags Copy a support report;
        /// the one-release rule marks every press unreleased since the last cut (rc.7), as Delta precision and
        /// Clock are, and two more on this page are: Open the log, since no press opened SimHub's log before
        /// this page, and the offer card's Every release on GitHub, since no card linked to the releases
        /// before it. Report an issue and Read the guide are rc.7's footer links under the page's words.
        /// </summary>
        public static readonly IReadOnlyList<string> NewTagged = new[] { CopyReport, OpenLog, EveryRelease };

        public static bool IsNew(string label)
        {
            return NewTagged.Contains(label);
        }

        public const double SupportButtonGap = 8;

        /// <summary>The gap between a Support press's words and its NEW tag.</summary>
        public const double NewTagGap = 8;
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
            // "(UTC)" labels a time, so a rig that has never checked, whose line carries none, goes without it.
            text.AppendLine("Update check: " + (input.ChecksOn ? "on" : "off") + ". " + LastChecked(input.LastCheckedTicks, input.GeneratedUtc, TimeZoneInfo.Utc) + (input.LastCheckedTicks > 0 ? " (UTC)." : "."));
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
