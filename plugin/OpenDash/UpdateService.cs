// UpdateService.cs: asking, and applying what the answer offers.
//
// The two public entry points are synchronous and return what happened, so that everything they decide is tested.
// Threading is the caller's business and is one method at the bottom, because a background thread is the part that
// can take SimHub down with it: an unobserved exception on the thread pool terminates the process on .NET Framework,
// so nothing here is allowed to throw.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What applying an update did.</summary>
    public sealed class UpdateOutcome
    {
        public bool Ok { get; set; }

        /// <summary>Folders actually replaced.</summary>
        public IReadOnlyList<string> Updated { get; set; } = new string[0];

        /// <summary>Folders left alone because somebody had edited them.</summary>
        public IReadOnlyList<string> HeldBack { get; set; } = new string[0];

        /// <summary>Folders the release does not publish.</summary>
        public IReadOnlyList<string> NotCarried { get; set; } = new string[0];

        public string Reason { get; set; }

        /// <summary>What to tell the user afterwards, including the reopen sentence when anything changed.</summary>
        public string Line
        {
            get
            {
                if (!Ok) return "The update did not finish: " + (Reason ?? "no reason given") + ".";
                if (Updated.Count == 0 && HeldBack.Count > 0) return "Nothing was replaced, because every dashboard has been edited since openDash wrote it.";
                if (Updated.Count == 0) return "There was nothing to replace.";
                var line = Updated.Count == 1 ? "Updated 1 dashboard. " : "Updated " + Updated.Count + " dashboards. ";
                if (HeldBack.Count > 0) line += (HeldBack.Count == 1 ? "1 was left alone because it has been edited. " : HeldBack.Count + " were left alone because they have been edited. ");
                return line + UpdateWording.Reopen;
            }
        }
    }

    public sealed class UpdateService
    {
        private readonly IReleaseSource source;
        private readonly IInstallLog log;

        public UpdateService(IReleaseSource source, IInstallLog log = null)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.log = log ?? NullInstallLog.Instance;
        }

        /// <summary>The releases this check found, kept so that applying does not have to ask again.</summary>
        public IReadOnlyList<ReleaseInfo> LastReleases { get; private set; } = new ReleaseInfo[0];

        /// <summary>
        /// Asks, and says what the answer was. Returns Disabled without fetching when the setting is off, which is
        /// the commitment that switching it off means nothing is fetched rather than fetched and discarded.
        /// </summary>
        public UpdateStatus Check(string installedVersion, bool enabled, ref long lastCheckTicks, DateTime nowUtc, bool manual)
        {
            if (!enabled)
            {
                LastReleases = new ReleaseInfo[0];
                return new UpdateStatus { State = UpdateState.Disabled, InstalledVersion = installedVersion, Manual = manual };
            }
            if (!UpdateCheck.ShouldCheck(true, lastCheckTicks, nowUtc, manual))
            {
                // Not due. Whatever the last check concluded still stands, and nothing is fetched.
                return null;
            }

            var fetched = source.GetString(UpdateCheck.ReleasesUrl);
            if (!fetched.Ok)
            {
                log.Info("Update check: " + fetched.Reason);
                return new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = installedVersion, Manual = manual };
            }
            if (!ReleaseFeed.TryParse(fetched.Body, out var releases))
            {
                log.Info("Update check: the answer was not a release listing this can act on.");
                return new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = installedVersion, Manual = manual };
            }

            LastReleases = releases;
            // Only a real answer moves the clock, so a day of failures does not silence tomorrow's check.
            lastCheckTicks = nowUtc.Ticks;
            var status = UpdateCheck.Conclude(installedVersion, releases, manual);
            log.Info("Update check: " + UpdateWording.Line(status));
            return status;
        }

        /// <summary>
        /// Downloads what the release carries for the dashboards installed here and installs it.
        /// </summary>
        /// <param name="replaceEdited">
        /// Whether to replace a dashboard somebody has edited since openDash wrote it. False unless a person has
        /// been shown what that means and said yes.
        /// </param>
        public UpdateOutcome Apply(DashboardInstaller installer, ReleaseInfo release, bool replaceEdited)
        {
            if (installer == null || release == null) return new UpdateOutcome { Reason = "there is nothing to apply" };

            var plan = UpdatePlan.For(installer.Packages, release);
            if (plan.IsEmpty)
            {
                return new UpdateOutcome { Ok = true, NotCarried = plan.NotCarried };
            }

            var downloaded = new DownloadedPackageSource();
            foreach (var item in plan.Items)
            {
                var fetched = source.GetBytes(item.Asset.DownloadUrl);
                if (!fetched.Ok) return new UpdateOutcome { Reason = item.FolderName + " could not be downloaded (" + fetched.Reason + ")" };
                if (!Digest.Matches(fetched.Bytes, item.Asset.Digest))
                {
                    return new UpdateOutcome { Reason = item.FolderName + " did not arrive as GitHub published it, so nothing was installed" };
                }
                downloaded.Add(item.Asset.Name, fetched.Bytes);
            }

            // Everything is in hand before anything on disk is touched, so a download that fails half way through
            // leaves the machine as it was rather than half updated.
            var target = new DashboardInstaller(installer.SimHubRoot, log, downloaded, installer.Record);
            target.EnsureInstalled(force: true, replaceEdited: replaceEdited);

            return new UpdateOutcome
            {
                Ok = true,
                Updated = target.Packages.Where(p => p.Extracted).Select(p => p.FolderName).ToList(),
                HeldBack = target.Packages.Where(p => p.HeldBack).Select(p => p.FolderName).ToList(),
                NotCarried = plan.NotCarried,
                Reason = target.LastError,
            };
        }

        /// <summary>
        /// Runs work off the caller's thread and hands the result back, swallowing everything.
        /// </summary>
        /// <remarks>
        /// Nothing may run on the synchronous path of Init, and nothing may block on the thread SimHub calls it on.
        /// An unobserved exception on a thread-pool thread terminates the process on .NET Framework, so SimHub would
        /// vanish without a dialog; hence the catch that looks like it catches too much and does not.
        /// </remarks>
        public static void InBackground(Action work, IInstallLog log = null)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    (log ?? NullInstallLog.Instance).Error("An update check failed in the background: " + ex);
                }
            });
        }
    }
}
