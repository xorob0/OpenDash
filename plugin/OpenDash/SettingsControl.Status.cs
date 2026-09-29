// SettingsControl.Status.cs: what Home says needs fixing, asked of SimHub -- the collector that fills
// PanelAttention's input and keeps its answer for the sidebar's dots.
//
// Every SimHub call is guarded, and a fact that could not be read stays null, which PanelAttention turns into
// no issue: a panel that guessed would put a warning on a rig that is fine. It runs on Go and on "Check
// again", never on the tick, because it reads SimHub's device settings and the disk.
//
// Two facts are read that nothing read before #503. Whether a dashboard folder was written after SimHub
// started is its write time against the process's start: SimHub reads its template list once at startup
// (docs/dev-loop.md), so a folder written later is one SimHub has not loaded. And whether a strip's profile is
// selected on its device is SimHub's own CurrentProfile on that device's LED settings, which is public. Which
// matrix device shows which content slot is not reachable, so that rule never fires yet.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>What needs fixing, as of the last Go or "Check again". Never null.</summary>
        private IList<PanelIssue> issues = new List<PanelIssue>();

        /// <summary>
        /// How long after SimHub starts a folder may still be written and be read by SimHub itself.
        /// </summary>
        /// <remarks>
        /// The plugin's Init writes the rig's folders while SimHub is starting, and whether SimHub reads its
        /// templates before or after that is not something the panel can see. Anything written in the first
        /// two minutes is therefore taken as loaded, which errs on the side of saying nothing; a screen added
        /// from the panel is added long after.
        /// </remarks>
        private static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(2);

        /// <summary>Asks SimHub again and keeps the answer. Cheap enough for every Go; never on the tick.</summary>
        private void RefreshAttention()
        {
            try
            {
                issues = PanelAttention.Find(Attention());
            }
            catch (Exception ex)
            {
                Log.Warn("Working out what needs fixing failed: " + ex.Message);
                issues = new List<PanelIssue>();
            }
        }

        /// <summary>"Check again": asks again and draws the page again, installing nothing.</summary>
        private void CheckAgain()
        {
            Redraw();
        }

        /// <summary>Everything Home is told, each fact as read or as unknown.</summary>
        private AttentionInput Attention()
        {
            var input = new AttentionInput();
            var since = LoadedBefore();

            foreach (var screen in Settings.RigScreens())
            {
                if (screen == null) continue;
                input.Screens.Add(new AttentionScreen
                {
                    Name = screen.Name,
                    Namespace = screen.Namespace,
                    Installed = FolderInstalled(screen),
                    WrittenSinceStart = WrittenAfter(screen, since),
                    Unclaimed = screen.Unclaimed == true,
                });
            }

            var embedded = EmbeddedJsonByShape();
            bool reachable;
            var census = BarCensus(embedded, out reachable);
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

        /// <summary>The moment after which a folder written is one SimHub has not read, or null when the
        /// process's start cannot be read.</summary>
        private static DateTime? LoadedBefore()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    return process.StartTime.ToUniversalTime() + StartupGrace;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read when SimHub started: " + ex.Message);
                return null;
            }
        }

        /// <summary>Whether the screen's folder or its dashboard was written after that moment.</summary>
        private bool? WrittenAfter(ScreenInstance screen, DateTime? since)
        {
            if (since == null || screen.Folder == null) return null;
            try
            {
                var root = plugin.Installer.SimHubRoot;
                if (!PackageExtractor.IsInstalled(root, screen.Folder)) return null;
                var folder = PackageExtractor.InstalledFolder(root, screen.Folder);
                var written = Directory.GetLastWriteTimeUtc(folder);
                var file = PackageExtractor.InstalledDashboard(root, screen.Folder);
                if (file != null && File.Exists(file))
                {
                    var fileTime = File.GetLastWriteTimeUtc(file);
                    if (fileTime > written) written = fileTime;
                }
                return written > since.Value;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell when " + screen.Folder + " was written: " + ex.Message);
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
