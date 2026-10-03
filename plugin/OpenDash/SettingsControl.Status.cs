// SettingsControl.Status.cs: what Home says needs fixing, asked of SimHub -- the collector that fills
// PanelAttention's input and keeps its answer for the sidebar's dots.
//
// Every SimHub call is guarded, and a fact that could not be read stays null, which PanelAttention turns into
// no issue: a panel that guessed would put a warning on a rig that is fine. It runs on Go and on "Check
// again", never on the tick, because it reads SimHub's device settings and the disk.
//
// Two facts are read that nothing read before #791. Whether a screen's dashboard waits for a restart is
// whether its folder is in DashTemplates now and was not in the list the plugin took when Init finished
// (OpenDash.TemplatesAtStart): SimHub reads its template list once at startup (docs/dev-loop.md), so a folder
// this session created is one SimHub has not loaded, and one it loaded stays loaded however often Dash Studio,
// Reinstall or Edit rewrites it. And whether a strip's profile is
// selected on its device is SimHub's own CurrentProfile on that device's LED settings, which is public. Which
// matrix device shows which content slot is not reachable, so that rule never fires yet.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>What needs fixing, as of the last Go or "Check again". Never null.</summary>
        private IList<PanelIssue> issues = new List<PanelIssue>();

        /// <summary>The facts that answer was worked out from, kept so a page can say each device's state
        /// without asking SimHub again: read them through ScreenFacts, StripFacts and MatrixFacts. Never
        /// null; a fact nobody could read is null inside it.</summary>
        private AttentionInput attentionFacts = new AttentionInput();

        /// <summary>Asks SimHub again and keeps the answer. Run on every Go, so it opens only the profiles of the rig's own strips; never on the tick.</summary>
        private void RefreshAttention()
        {
            try
            {
                attentionFacts = Attention();
                issues = PanelAttention.Find(attentionFacts);
            }
            catch (Exception ex)
            {
                Log.Warn("Working out what needs fixing failed: " + ex.Message);
                attentionFacts = new AttentionInput();
                issues = new List<PanelIssue>();
            }
        }

        /// <summary>What was last read about one screen -- Installed, AddedSinceStart, Unclaimed -- or null
        /// when the rig has no such screen. Read while building, never on the tick.</summary>
        private AttentionScreen ScreenFacts(string ns)
        {
            return attentionFacts.Screens.FirstOrDefault(screen => screen != null && string.Equals(screen.Namespace, ns, StringComparison.Ordinal));
        }

        /// <summary>What was last read about one strip -- its profile's state in SimHub (Profile) and whether
        /// its device has it selected (Selected), so a card can say Showing -- or null.</summary>
        private AttentionStrip StripFacts(string ns)
        {
            return attentionFacts.Strips.FirstOrDefault(strip => strip != null && string.Equals(strip.Namespace, ns, StringComparison.Ordinal));
        }

        /// <summary>What was last read about one matrix slot, or null.</summary>
        private AttentionMatrix MatrixFacts(int slot)
        {
            return attentionFacts.Matrices.FirstOrDefault(matrix => matrix != null && matrix.Slot == slot);
        }

        /// <summary>"Check again": asks again and draws the page again, installing nothing.</summary>
        private void CheckAgain()
        {
            Redraw();
        }

        /// <summary>
        /// The "Check again" press on an issue's fix, wherever the issue is drawn (Home's list, the LEDs page's fix
        /// box): asks again, then says what SimHub answered the second time, so the press is never silent where
        /// nothing on screen moved. Said after the redraw, which clears the lines.
        /// </summary>
        private void CheckAgainAndSay(PanelIssue issue)
        {
            CheckAgain();
            Say(PanelHome.CheckedAgain(issue, issues, StripFacts(issue.Subject)));
        }

        /// <summary>Everything Home is told, each fact as read or as unknown.</summary>
        private AttentionInput Attention()
        {
            var input = new AttentionInput();
            var atStart = plugin.TemplatesAtStart;

            foreach (var screen in Settings.RigScreens())
            {
                if (screen == null) continue;
                var installed = FolderInstalled(screen);
                input.Screens.Add(new AttentionScreen
                {
                    Name = screen.Name,
                    Namespace = screen.Namespace,
                    Installed = installed,
                    AddedSinceStart = PackageExtractor.WaitsForRestart(atStart, screen.Folder, installed),
                    Unclaimed = screen.Unclaimed == true,
                });
            }

            // Only the rig's own strips' profiles, and no census at all without a strip: this runs on every Go.
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            var reachable = false;
            IList<KeyValuePair<LedBar, FlagBoxPlan>> census = new List<KeyValuePair<LedBar, FlagBoxPlan>>();
            if (bars.Count > 0) census = BarCensus(EmbeddedJsonFor(bars.Select(bar => bar.ProfileShapeId)), out reachable);
            // One walk of SimHub's devices for every strip, rather than one each.
            IList<LedTarget> targets = new List<LedTarget>();
            if (census.Count > 0)
            {
                try
                {
                    targets = LedTargets.All();
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not list SimHub's LED devices: " + ex.Message);
                }
            }
            foreach (var entry in census)
            {
                var bar = entry.Key;
                var wanted = LedBar.NormaliseDevice(bar.Device);
                var target = targets.FirstOrDefault(t => string.Equals(t.Id, wanted, StringComparison.Ordinal));
                input.Strips.Add(new AttentionStrip
                {
                    Name = bar.Name,
                    Namespace = bar.Namespace,
                    DeviceName = target == null ? null : target.Name,
                    Profile = reachable ? entry.Value.State : (FlagBoxInstallState?)null,
                    Selected = ProfileSelected(target, bar),
                });
            }

            foreach (var matrix in Settings.MatrixPanels())
            {
                // Which content slot a matrix device shows is not on SimHub's public surface, so Shown stays
                // unknown and the rule is silent rather than wrong.
                input.Matrices.Add(new AttentionMatrix { Slot = matrix, Name = Settings.MatrixName(matrix), Shown = null });
            }
            if (input.Matrices.Count > 0 && plugin.FlagBoxJson != null)
            {
                var plan = SafePlan();
                input.FlagBox = plan.State == FlagBoxInstallState.Unavailable ? (FlagBoxInstallState?)null : plan.State;
                input.FlagBoxName = FlagBoxName();
            }

            // Read once at start, before the first save (OpenDash.LoadSettings): it stands for the session.
            var rescue = plugin.Rescue;
            if (rescue != null && rescue.Unreadable)
            {
                input.SettingsUnreadable = true;
                input.SettingsCopy = rescue.CopyPath;
                input.SettingsBackups = rescue.BackupsPath;
            }

            input.RestartPending = PendingRestart();
            input.UpdateAvailable = updateStatus.State == UpdateState.UpdateAvailable;
            input.OfferedVersion = updateStatus.LatestVersion;
            return input;
        }

        /// <summary>Whether the screen's folder is in DashTemplates, or null when the disk could not be asked.</summary>
        private bool? FolderInstalled(ScreenInstance screen)
        {
            if (screen.Folder == null) return false;
            try
            {
                return PackageExtractor.IsInstalled(plugin.Installer.SimHubRoot, screen.Folder);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether " + screen.Folder + " is installed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Whether the strip's device has the strip's profile selected, or null when that cannot be read.
        /// </summary>
        /// <remarks>
        /// SimHub's LedsSettings carries CurrentProfile, which is public; the strip's profile is the one
        /// LedBarProfile.IdFor gives it. Automatic profile switching can move the selection when the car or
        /// the game changes, which is why the fix names that setting too.
        /// </remarks>
        private static bool? ProfileSelected(LedTarget target, LedBar bar)
        {
            if (target == null || target.Settings == null || bar == null) return null;
            try
            {
                var current = target.Settings.CurrentProfile;
                if (current == null) return false;
                return current.ProfileId == LedBarProfile.IdFor(bar.Namespace);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read which profile " + target.Name + " has selected: " + ex.Message);
                return null;
            }
        }
    }
}
