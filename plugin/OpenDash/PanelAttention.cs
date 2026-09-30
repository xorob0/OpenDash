// PanelAttention.cs: what Home says needs fixing, and in what order.
//
// Home leads with the things a driver cannot see from the seat: a dashboard written into SimHub that SimHub
// has not loaded, a folder that is gone, a strip whose profile is installed and not selected on its device,
// a matrix slot no device shows, an update waiting for a restart (#503). Each is said in the driver's terms
// and, where the fix is in SimHub, with the steps there.
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
    }

    public static class PanelAttention
    {
        /// <summary>The menus a strip's profile is selected under, between the device's own name.</summary>
        public const string DevicesCrumb = "Devices";
        public const string TelemetryLedsCrumb = "Telemetry LEDs";

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
                    "Restart SimHub, then assign \"" + screen.Name + "\" to this display in Dash Studio.",
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
                    name + " is dark",
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
                // Filed where the Update press is, which the LEDs page says (PanelLeds.StripUpdateRoute):
                // Updates' lights section until the LEDs header carries the press, then LEDs. An issue whose
                // press lands where there is nothing to press is worse than one that sends the driver a page
                // further, and the item that wears the dot follows the issue's page (PanelNav.Warns and
                // UpdatesWarns), so neither this nor PanelNav changes when the press moves.
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

        public const string InstallAgain = "Install it again";
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
                new[] { DevicesCrumb, string.IsNullOrWhiteSpace(deviceName) ? "your LED device" : deviceName, TelemetryLedsCrumb },
                new[] { "Select \"" + stripName + "\"" },
                new[] { "Set automatic profile switching to Disabled" },
            };
        }

        private static bool IsInstalled(FlagBoxInstallState? state)
        {
            return state == FlagBoxInstallState.UpToDate || state == FlagBoxInstallState.Outdated;
        }
    }
}
