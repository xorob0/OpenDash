// PanelAttention.cs: what Home says needs fixing, and in what order.
//
// Home leads with the things a driver cannot see from the seat: a dashboard written into SimHub that SimHub
// has not loaded, a folder that is gone, a strip whose profile is installed and not selected on its device,
// a matrix slot no device shows, an update waiting for a restart (#503), a settings file SimHub could not read
// (#643). Each is said in the driver's terms and, where the fix is in SimHub, with the steps there.
//
// The rules are pure and take plain facts, because the facts come from SimHub and a SimHub call can fail.
// SettingsControl.Status.cs asks SimHub and fills an AttentionInput; a fact it could not read stays null,
// and a null fact produces no issue. A panel that guessed would put a warning on a rig that is fine, which
// is worse than saying nothing about one that is not.
//
// Pure: no WPF, no SimHub. PanelAttentionTests holds every rule, the order, and the silence of the unknown.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What an issue's button does.</summary>
    public enum PanelIssueAction
    {
        /// <summary>Opens the page the issue is about, with the thing it names selected.</summary>
        Navigate,

        /// <summary>Asks SimHub again and redraws. It never installs anything.</summary>
        CheckAgain,

        /// <summary>Writes the missing thing back: the screen's dashboard.</summary>
        Reinstall,

        /// <summary>Shows the file or folder the issue's subject names in Explorer: the settings SimHub could not
        /// read, where they were set aside (#643).</summary>
        OpenFolder,
    }

    /// <summary>One thing to fix.</summary>
    public sealed class PanelIssue
    {
        public PanelIssue(string id, PanelPage page, string subject, string title, string detail, IList<string[]> steps, string actionLabel, PanelIssueAction action, string anchor = null)
        {
            Id = id;
            Page = page;
            Subject = subject;
            Title = title;
            Detail = detail;
            Steps = steps ?? new List<string[]>();
            ActionLabel = actionLabel;
            Action = action;
            Anchor = anchor;
        }

        /// <summary>Stable across redraws: the rule and what it is about, "screen-missing:Face1280x480".</summary>
        public string Id { get; private set; }

        /// <summary>The page the thing lives on, which is where the button goes and which sidebar item wears
        /// the dot.</summary>
        public PanelPage Page { get; private set; }

        /// <summary>What to select on that page: a screen's namespace, a strip's namespace, a matrix's slot
        /// number as text. Null for an issue about the page as a whole.</summary>
        public string Subject { get; private set; }

        /// <summary>The row on that page to scroll to, or null.</summary>
        public string Anchor { get; private set; }

        public string Title { get; private set; }

        public string Detail { get; private set; }

        /// <summary>
        /// The steps in SimHub, in order. A step of one string is a sentence; a step of several is a path
        /// through SimHub's own menus, drawn as crumbs.
        /// </summary>
        public IList<string[]> Steps { get; private set; }

        public string ActionLabel { get; private set; }

        public PanelIssueAction Action { get; private set; }

        public PanelRoute Route { get { return new PanelRoute(Page, Anchor); } }
    }

    /// <summary>A screen as Home needs it. Null facts are facts nobody could read.</summary>
    public sealed class AttentionScreen
    {
        public string Name { get; set; }
        public string Namespace { get; set; }

        /// <summary>Whether its dashboard folder is in DashTemplates.</summary>
        public bool? Installed { get; set; }

        /// <summary>Whether that folder was created in this session, after SimHub loaded its templates, so
        /// SimHub has not read it (PackageExtractor.WaitsForRestart).</summary>
        public bool? AddedSinceStart { get; set; }

        /// <summary>Whether it is a screen a migration made that nobody has kept or removed.</summary>
        public bool Unclaimed { get; set; }
    }

    /// <summary>A strip as Home needs it.</summary>
    public sealed class AttentionStrip
    {
        public string Name { get; set; }
        public string Namespace { get; set; }

        /// <summary>The SimHub device the strip's profile goes to, as SimHub names it.</summary>
        public string DeviceName { get; set; }

        /// <summary>What SimHub holds for the strip's profile, or null when it could not be asked.</summary>
        public FlagBoxInstallState? Profile { get; set; }

        /// <summary>Whether that device has the strip's profile selected, or null when it could not be read.</summary>
        public bool? Selected { get; set; }
    }

    /// <summary>A matrix panel as Home needs it.</summary>
    public sealed class AttentionMatrix
    {
        public int Slot { get; set; }
        public string Name { get; set; }

        /// <summary>Whether some matrix device in SimHub shows this slot's content, or null when unknown.</summary>
        public bool? Shown { get; set; }
    }

    /// <summary>Everything Home is told, each fact as known or unknown.</summary>
    public sealed class AttentionInput
    {
        public AttentionInput()
        {
            Screens = new List<AttentionScreen>();
            Strips = new List<AttentionStrip>();
            Matrices = new List<AttentionMatrix>();
        }

        public IList<AttentionScreen> Screens { get; set; }
        public IList<AttentionStrip> Strips { get; set; }
        public IList<AttentionMatrix> Matrices { get; set; }

        /// <summary>What SimHub holds for the flag box profile, asked only when the rig has a matrix.</summary>
        public FlagBoxInstallState? FlagBox { get; set; }

        /// <summary>The name SimHub lists the flag box profile under.</summary>
        public string FlagBoxName { get; set; }

        /// <summary>Whether a plugin update is staged and waits for SimHub to close.</summary>
        public bool? RestartPending { get; set; }

        /// <summary>Whether the last check found a newer release, and which.</summary>
        public bool? UpdateAvailable { get; set; }
        public string OfferedVersion { get; set; }

        /// <summary>Whether the settings file was there and SimHub could not read it, so OpenDash started on
        /// defaults (SettingsRescue.Unreadable).</summary>
        public bool? SettingsUnreadable { get; set; }

        /// <summary>Where the unreadable file was set aside, or null when no copy holds it.</summary>
        public string SettingsCopy { get; set; }

        /// <summary>SimHub's _Backups folder beside the settings file, which holds the file when no copy does.</summary>
        public string SettingsBackups { get; set; }
    }

    public static class PanelAttention
    {
        /// <summary>The menus a strip's profile is selected under, between the device's own name.</summary>
        public const string DevicesCrumb = "Devices";
        public const string TelemetryLedsCrumb = "Telemetry LEDs";

        /// <summary>The device crumb when the panel does not know the strip's device: the device as SimHub lists
        /// it, in the words PanelLeds.SelectIt and the LEDs page's SimHub device row use, rather than "your LED
        /// device", which named no device a driver could find.</summary>
        public const string UnnamedDeviceCrumb = "the strip's device in SimHub";

        /// <summary>
        /// Every issue the facts show, most urgent first.
        /// </summary>
        /// <remarks>
        /// The order is the order a driver has to act in. A folder that is gone comes before one SimHub has
        /// not loaded, because restarting SimHub will not bring the first back; both come before the lights,
        /// because a screen is what most rigs have; an out-of-date profile and a waiting update come last,
        /// because the rig works while they wait.
        /// </remarks>
        public static IList<PanelIssue> Find(AttentionInput input)
        {
            var issues = new List<PanelIssue>();
            if (input == null) return issues;
            var screens = (input.Screens ?? new List<AttentionScreen>()).Where(s => s != null).ToList();
            var strips = (input.Strips ?? new List<AttentionStrip>()).Where(s => s != null).ToList();
            var matrices = (input.Matrices ?? new List<AttentionMatrix>()).Where(m => m != null).ToList();

            // First: the rig every other rule reads is the defaults OpenDash fell back to, and the driver's own
            // is in a file that only they can mend. Filed on Home, which wears the dot, since the fix is in no
            // page of the panel but in the folder the press opens.
            if (input.SettingsUnreadable == true)
            {
                var copy = string.IsNullOrWhiteSpace(input.SettingsCopy) ? null : input.SettingsCopy;
                issues.Add(new PanelIssue(
                    SettingsUnreadable, PanelPage.Home, copy ?? input.SettingsBackups,
                    SettingsUnreadableTitle,
                    SettingsUnreadableDetail(copy, input.SettingsBackups),
                    SettingsUnreadableSteps(copy != null), OpenFolder, PanelIssueAction.OpenFolder));
            }

            foreach (var screen in screens.Where(s => s.Installed == false))
            {
                issues.Add(new PanelIssue(
                    ScreenMissing + screen.Namespace, PanelPage.Screens, screen.Namespace,
                    screen.Name + "'s dashboard is missing from SimHub",
                    MissingDetail,
                    null, InstallAgain, PanelIssueAction.Reinstall));
            }

            foreach (var screen in screens.Where(s => s.Installed != false && s.AddedSinceStart == true))
            {
                issues.Add(new PanelIssue(
                    ScreenRestart + screen.Namespace, PanelPage.Screens, screen.Namespace,
                    screen.Name + " is not in SimHub yet",
                    ScreenRestartDetail(screen.Name),
                    null, Open(screen.Name), PanelIssueAction.Navigate));
            }

            foreach (var strip in strips.Where(s => s.Selected == false && IsInstalled(s.Profile)))
            {
                issues.Add(new PanelIssue(
                    StripUnselected + strip.Namespace, PanelPage.Leds, strip.Namespace,
                    strip.Name + "'s profile is not selected",
                    UnselectedDetail,
                    SelectSteps(strip.DeviceName, strip.Name), CheckAgain, PanelIssueAction.CheckAgain));
            }

            foreach (var matrix in matrices.Where(m => m.Shown == false))
            {
                var name = string.IsNullOrWhiteSpace(matrix.Name) ? "Matrix " + matrix.Slot : matrix.Name;
                issues.Add(new PanelIssue(
                    MatrixDark + matrix.Slot, PanelPage.Matrix, matrix.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    NotShownTitle(name),
                    "No matrix device in SimHub is set to matrix " + matrix.Slot + ".",
                    null, Open(name), PanelIssueAction.Navigate));
            }

            var unclaimed = screens.Count(s => s.Unclaimed);
            if (unclaimed > 0)
            {
                issues.Add(new PanelIssue(
                    ScreensUnclaimed, PanelPage.Screens, null,
                    UnclaimedTitle(unclaimed),
                    UnclaimedDetail,
                    null, Open(PanelScreens.Title), PanelIssueAction.Navigate));
            }

            foreach (var strip in strips.Where(s => s.Profile == FlagBoxInstallState.Outdated))
            {
                // Filed where the Update press is, which the LEDs page says (PanelLeds.StripUpdateRoute): the
                // strip's own header on LEDs. The item that wears the dot follows the issue's page (PanelNav.Warns
                // and UpdatesWarns), so neither this nor PanelNav changes when the press moves.
                var route = PanelLeds.StripUpdateRoute;
                issues.Add(new PanelIssue(
                    StripOutdated + strip.Namespace, route.Page, strip.Namespace,
                    strip.Name + "'s profile has an update",
                    null,
                    null, Open(PanelNav.Label(route.Page)), PanelIssueAction.Navigate, route.Anchor));
            }
            if (matrices.Count > 0 && input.FlagBox == FlagBoxInstallState.Outdated)
            {
                var name = string.IsNullOrWhiteSpace(input.FlagBoxName) ? FlagBoxProfile.ProfileName : input.FlagBoxName;
                issues.Add(new PanelIssue(
                    FlagBoxOutdated, PanelPage.Matrix, null,
                    name + " has an update",
                    null,
                    null, Open(PanelMatrix.Title), PanelIssueAction.Navigate));
            }

            if (input.RestartPending == true)
            {
                issues.Add(new PanelIssue(
                    UpdateRestart, PanelPage.Updates, null,
                    "Restart SimHub to finish updating",
                    RestartDetail,
                    null, Open(PanelUpdates.Title), PanelIssueAction.Navigate));
            }
            else if (input.UpdateAvailable == true && !string.IsNullOrWhiteSpace(input.OfferedVersion))
            {
                issues.Add(new PanelIssue(
                    UpdateAvailable, PanelPage.Updates, null,
                    "OpenDash " + input.OfferedVersion.Trim() + " is available",
                    null,
                    null, Open(PanelUpdates.Title), PanelIssueAction.Navigate));
            }

            return issues;
        }

        /// <summary>The page's title: "Nothing to fix", "1 thing to fix", "3 things to fix".</summary>
        public static string Headline(int count)
        {
            if (count <= 0) return "Nothing to fix";
            return count == 1 ? "1 thing to fix" : count + " things to fix";
        }

        // The ids' rules, each followed by what the issue is about where it has a subject. A page that asks
        // whether an issue stands for its card reads these rather than typing the prefix again.
        public const string ScreenMissing = "screen-missing:";
        public const string ScreenRestart = "screen-restart:";
        public const string StripUnselected = "strip-unselected:";
        public const string MatrixDark = "matrix-dark:";
        public const string ScreensUnclaimed = "screens-unclaimed";
        public const string StripOutdated = "strip-outdated:";
        public const string FlagBoxOutdated = "flagbox-outdated";
        public const string UpdateRestart = "update-restart";
        public const string UpdateAvailable = "update-available";
        public const string SettingsUnreadable = "settings-unreadable";

        /// <summary>Whether the issues hold one of that rule about that subject: ("screen-restart:", "Rim").</summary>
        public static bool Has(IEnumerable<PanelIssue> issues, string rule, string subject = null)
        {
            var id = rule + (subject ?? string.Empty);
            return issues != null && issues.Any(issue => issue != null && string.Equals(issue.Id, id, StringComparison.Ordinal));
        }

        /// <summary>The one issue of that rule about that subject, or null.</summary>
        public static PanelIssue Of(IEnumerable<PanelIssue> issues, string rule, string subject = null)
        {
            var id = rule + (subject ?? string.Empty);
            return issues == null ? null : issues.FirstOrDefault(issue => issue != null && string.Equals(issue.Id, id, StringComparison.Ordinal));
        }

        /// <summary>
        /// A matrix no device shows, titled by its name and the Matrix card's state for it, one phrase for one state:
        /// "Left pillar is not shown in SimHub". The matrix number is in the detail under it.
        /// </summary>
        public static string NotShownTitle(string name)
        {
            var state = PanelMatrix.NotShown;
            return name + " is " + char.ToLowerInvariant(state[0]) + state.Substring(1);
        }

        public const string InstallAgain = "Install it again";

        // --- The settings SimHub could not read (#643) ----------------------------------------------------------

        public const string SettingsUnreadableTitle = "OpenDash could not read its settings";

        /// <summary>The file SimHub reads them from, as a driver finds it in PluginsData\Common.</summary>
        public const string SettingsFileName = "OpenDash.GeneralSettings.json";

        /// <summary>The press: Explorer, on the copy where there is one and on _Backups otherwise.</summary>
        public const string OpenFolder = "Open the folder";

        /// <summary>
        /// What happened and where the file is: "It started on defaults and kept the file as C:\...\
        /// OpenDash.GeneralSettings.unreadable.json." When no copy holds it, SimHub's _Backups does, where the
        /// save this start made moved it.
        /// </summary>
        public static string SettingsUnreadableDetail(string copy, string backups)
        {
            if (!string.IsNullOrWhiteSpace(copy)) return "It started on defaults and kept the file as " + copy + ".";
            return "It started on defaults. SimHub keeps the file in " + (string.IsNullOrWhiteSpace(backups) ? "its _Backups folder" : backups) + ".";
        }

        /// <summary>
        /// The steps to have the rig back, in the order they have to be taken.
        /// </summary>
        /// <remarks>
        /// SimHub is closed first, because OpenDash saves its settings when SimHub closes: a file restored while
        /// it runs is written over with the defaults on the way out. The kept file is the one SimHub could not
        /// read, so it is corrected rather than put back as it is; _Backups holds earlier versions as
        /// OpenDash.GeneralSettings_b1.json and on, any of which is a whole rig.
        /// </remarks>
        public static IList<string[]> SettingsUnreadableSteps(bool kept)
        {
            return new List<string[]>
            {
                new[] { "Close SimHub" },
                new[] { kept
                    ? "Correct the kept file, or take an earlier one from _Backups, and save it as " + SettingsFileName
                    : "Take an earlier file from _Backups and save it as " + SettingsFileName },
                new[] { "Start SimHub" },
            };
        }

        /// <summary>The folder press could not open Explorer there, named rather than the exception's words.</summary>
        public static string FolderFailed(string path)
        {
            return "Could not open " + path + ".";
        }
        public const string CheckAgain = "Check again";
        // An outdated profile's issue has no detail: its title says there is an update and its press says
        // where the Update is. "Update it to the version this OpenDash carries." described the mechanism, a
        // newer profile inside the plugin, which 8a410ed took out of DuplicateFailed for the same reason.

        /// <summary>
        /// Under a screen whose dashboard is gone, on Home and in the Screens page's fix box alike. Only what
        /// the press beside it does not already say: its label is Install it again.
        /// </summary>
        public const string MissingDetail = "Its settings are kept.";

        public const string UnselectedDetail = "Installed, but not selected in SimHub.";

        public const string RestartDetail = "Until then you are running the old version.";

        /// <summary>
        /// Under a screen SimHub has not read yet: the panel's one phrase for that state as the step
        /// (PanelCopy.RestartToLoad), then the step after it in the Screens fix box's words. The
        /// title names the screen, so a list of issues is read by what each is about.
        /// </summary>
        public static string ScreenRestartDetail(string name)
        {
            return PanelCopy.RestartToLoad + ". " + PanelScreens.RestartDetail(name);
        }

        /// <summary>"1 screen came with an older OpenDash", "2 screens ...".</summary>
        public static string UnclaimedTitle(int count)
        {
            return (count == 1 ? "1 screen" : count + " screens") + " came with an older OpenDash";
        }

        /// <summary>What Home says under the screens an older OpenDash made, in the noun and the verbs the
        /// Screens page's note uses for them (PanelScreens.UnclaimedNote): screens, each kept or removed.</summary>
        public const string UnclaimedDetail = "Keep or remove each one on the Screens page.";

        public static string Open(string name) { return "Open " + name; }

        /// <summary>
        /// The three steps that select a strip's profile on its device, the way SimHub's menus spell them.
        /// </summary>
        /// <remarks>
        /// "Select" and not "pick", and the strip's own name in quotes: LedBarProfile installs the profile
        /// under the name its driver gave the strip, which is the row SimHub's list shows.
        /// </remarks>
        public static IList<string[]> SelectSteps(string deviceName, string stripName)
        {
            return new List<string[]>
            {
                new[] { DevicesCrumb, string.IsNullOrWhiteSpace(deviceName) ? UnnamedDeviceCrumb : deviceName, TelemetryLedsCrumb },
                new[] { "Select \"" + stripName + "\"" },
                new[] { "Set automatic profile switching to Disabled" },
            };
        }

        private static bool IsInstalled(FlagBoxInstallState? state)
        {
            return state.HasValue && FlagBoxInstallPlan.InSimHub(state.Value);
        }
    }
}
