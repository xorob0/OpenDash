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

        /// <summary>Folders that were meant to be replaced and could not be.</summary>
        public IReadOnlyList<string> Failed { get; set; } = new string[0];

        public string Reason { get; set; }

        /// <summary>What to tell the user afterwards, including the reopen sentence when anything changed.</summary>
        public string Line
        {
            get
            {
                if (!Ok)
                {
                    var failure = "The update did not finish: " + (Reason ?? "no reason given") + ".";
                    // What did land still has to be said, or a person cannot tell what state they are in.
                    return Updated.Count == 0 ? failure : failure + " " + Updated.Count + " of them were replaced before it stopped. " + UpdateWording.Reopen;
                }
                if (Updated.Count == 0 && HeldBack.Count > 0) return "Nothing was replaced, because every dashboard has been edited since OpenDash wrote it.";
                if (Updated.Count == 0) return "There was nothing to replace.";
                var line = Updated.Count == 1 ? "Updated 1 dashboard. " : "Updated " + Updated.Count + " dashboards. ";
                if (HeldBack.Count > 0) line += (HeldBack.Count == 1 ? "1 was left alone because it has been edited. " : HeldBack.Count + " were left alone because they have been edited. ");
                if (NotCarried.Count > 0) line += (NotCarried.Count == 1 ? "1 is not in this release and was not touched. " : NotCarried.Count + " are not in this release and were not touched. ");
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

            var collected = new List<ReleaseInfo>();
            var settled = false;
            for (var page = 1; page <= UpdateCheck.MaxPages && !settled; page++)
            {
                var fetched = source.GetString(UpdateCheck.ReleasesPage(page));
                if (!fetched.Ok)
                {
                    log.Info("Update check: " + fetched.Reason);
                    break;
                }
                // Past the last page GitHub answers "[]", which the parser reports as nothing to act on exactly as
                // it reports GitHub's error shape, and the two cannot be told apart. Both therefore end the reading
                // unsettled: calling an empty page the end of the listing would mean reporting up to date on an
                // answer nothing verified, whereas the reverse costs at worst one "could not check", and only to a
                // user whose stable version no release of this repository ever carried.
                if (!ReleaseFeed.TryParse(fetched.Body, out var onThisPage))
                {
                    log.Info("Update check: the answer was not a release listing this can act on.");
                    break;
                }
                collected.AddRange(onThisPage);
                // A short page is the last one, so the listing is exhausted and nothing further can be hidden.
                settled = UpdateCheck.Settles(collected, installedVersion) || onThisPage.Count < UpdateCheck.PageSize;
            }
            if (!settled)
            {
                // Everything that lands here read either nothing or a run of releases this user may not be offered,
                // and in both cases what exists beyond is unknown. Never up to date on the strength of that.
                return new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = installedVersion, Manual = manual };
            }

            LastReleases = collected;
            // Only a real answer moves the clock, so a day of failures does not silence tomorrow's check.
            lastCheckTicks = nowUtc.Ticks;
            var status = UpdateCheck.Conclude(installedVersion, collected, manual);
            log.Info("Update check: " + UpdateWording.Line(status));
            return status;
        }

        /// <summary>
        /// Downloads what the release carries for the dashboards installed here and installs it.
        /// </summary>
        /// <param name="replaceEdited">
        /// Whether to replace a dashboard somebody has edited since OpenDash wrote it. False unless a person has
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

            // A package that failed to install is a failure, whatever the others did. Reporting Ok because the run
            // finished, and putting the reason in a field the wording ignored, told a user their dashboards were
            // updated when one of them had not been.
            // A package that could not even be read has no folder name, so the name it was fetched under stands in:
            // telling somebody that "" could not be installed is no better than telling them nothing.
            var failed = target.Packages
                .Where(p => p.Status == InstallStatus.Failed)
                .Select(p => p.FolderName ?? FolderOf(plan, p.Name) ?? p.Name)
                .ToList();
            return new UpdateOutcome
            {
                Ok = failed.Count == 0,
                Updated = target.Packages.Where(p => p.Extracted).Select(p => p.FolderName).ToList(),
                HeldBack = target.Packages.Where(p => p.HeldBack).Select(p => p.FolderName).ToList(),
                Failed = failed,
                NotCarried = plan.NotCarried,
                Reason = failed.Count == 0 ? null : (target.LastError ?? string.Join(", ", failed) + " could not be installed"),
            };
        }

        /// <summary>The folder an asset was fetched for, when the package itself could not be read.</summary>
        private static string FolderOf(UpdatePlan plan, string assetName) =>
            plan.Items.FirstOrDefault(i => i.Asset != null && i.Asset.Name == assetName)?.FolderName;

        private static readonly object WorkGate = new object();
        private static readonly System.Threading.ManualResetEventSlim Idle = new System.Threading.ManualResetEventSlim(true);
        private static int mustFinishCount;

        /// <summary>
        /// Runs work off the caller's thread, swallowing everything.
        /// </summary>
        /// <param name="mustFinish">
        /// True for work that is rewriting DashTemplates. A thread-pool thread is a background thread, so the CLR
        /// terminates it at process exit without unwinding and no finally runs: abandoning an install between the
        /// delete and the move leaves a dashboard folder that is simply gone, and abandoning it at all leaves the
        /// staging folder behind. Such work is counted, and WaitForIdle gives it a bounded chance to finish.
        /// False for a check, which is a read and may be abandoned freely.
        /// </param>
        /// <remarks>
        /// Nothing may run on the synchronous path of Init, and nothing may block on the thread SimHub calls it on.
        /// An unobserved exception on a thread-pool thread terminates the process on .NET Framework, so SimHub would
        /// vanish without a dialog; hence the catch that looks like it catches too much and does not.
        /// </remarks>
        public static void InBackground(Action work, IInstallLog log = null, bool mustFinish = false)
        {
            if (mustFinish)
            {
                lock (WorkGate)
                {
                    if (mustFinishCount++ == 0) Idle.Reset();
                }
            }
            // Counted before the work is queued, so Busy is true the moment the caller returns rather than once
            // the pool gets round to it.
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
                finally
                {
                    if (mustFinish)
                    {
                        lock (WorkGate)
                        {
                            if (--mustFinishCount == 0) Idle.Set();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Waits for work that is rewriting DashTemplates, and reports whether it finished.
        /// </summary>
        /// <remarks>
        /// Called from the plugin's End, which SimHub runs on shutdown. Waiting there is the opposite of the usual
        /// advice and is right here: the alternative is the process exiting between a DeleteDirectory and the
        /// Directory.Move that replaces what it deleted. A check is never waited for, since abandoning a read costs
        /// nothing and a socket that never answers would hold SimHub's shutdown for its whole timeout.
        /// </remarks>
        public static bool WaitForIdle(TimeSpan timeout) => Idle.Wait(timeout);

        /// <summary>True while an install is in flight, which is what the panel refuses to start a second one on.</summary>
        public static bool Busy
        {
            get { lock (WorkGate) { return mustFinishCount > 0; } }
        }
    }
}
