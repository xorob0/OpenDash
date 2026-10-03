// UpdateService.cs: asking, and applying what the answer offers.
//
// The two public entry points are synchronous and return what happened, so that everything they decide is tested.
// The threads are at the bottom, because a background thread is the part that can take SimHub down with it: an
// unobserved exception on the thread pool terminates the process on .NET Framework, so nothing there is allowed to
// throw. What a run owes the settings is done on the run's own thread, inside the part shutdown waits for, so that
// it never depends on a settings page that may have closed (#613).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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

        /// <summary>
        /// Folders the release does not publish on their own but carries inside the plugin it staged, which the new
        /// plugin brings up to date as SimHub starts again.
        /// </summary>
        /// <remarks>
        /// Every folder, for a release cut since #438: a release publishes OpenDash-plugin.zip and nothing else, so
        /// the dashboards travel inside the assembly and are written by its first start rather than by this run.
        /// Counting them as "not in this release" told a driver the opposite of what was about to happen.
        /// </remarks>
        public IReadOnlyList<string> FollowPlugin { get; set; } = new string[0];

        /// <summary>Of <see cref="FollowPlugin"/>, the folders somebody has edited since OpenDash wrote them.</summary>
        public IReadOnlyList<string> EditedFollowing { get; set; } = new string[0];

        /// <summary>
        /// Whether a person said yes to replacing the edited ones, which the caller has to carry across the restart
        /// (<see cref="EditedConsent"/>): this process is not the one that writes them.
        /// </summary>
        public bool ReplaceEditedOnRestart { get; set; }

        /// <summary>Folders that were meant to be replaced and could not be.</summary>
        public IReadOnlyList<string> Failed { get; set; } = new string[0];

        public string Reason { get; set; }

        /// <summary>Whether the new plugin is staged and will be put in place when SimHub closes.</summary>
        public bool PluginStaged { get; set; }

        /// <summary>Whether the dashboards this run wrote brought a font into DashFonts, which the running SimHub
        /// cannot draw until it restarts (<see cref="UpdateWording.RestartToSee"/>).</summary>
        public bool FontsWritten { get; set; }

        /// <summary>What to tell the user afterwards, including the reopen sentence when anything changed.</summary>
        public string Line
        {
            get
            {
                if (!Ok)
                {
                    var failure = "The update did not finish: " + (Reason ?? "no reason given") + ".";
                    // What did land still has to be said, or a person cannot tell what state they are in.
                    return Updated.Count == 0 ? failure : failure + " " + Updated.Count + " of them were replaced before it stopped. " + UpdateWording.ToSee(FontsWritten);
                }
                var line = Updated.Count == 1 ? "Updated 1 dashboard. " : Updated.Count > 1 ? "Updated " + Updated.Count + " dashboards. " : string.Empty;
                if (Updated.Count == 0 && HeldBack.Count > 0) line = "No dashboard was replaced: you have edited all of them. ";
                else if (Updated.Count == 0 && !PluginStaged) return "There was nothing to replace.";
                else if (Updated.Count == 0 && FollowPlugin.Count == 0) line = "The dashboards were already up to date. ";
                if (Updated.Count > 0 && HeldBack.Count > 0) line += (HeldBack.Count == 1 ? "1 was left alone: you have edited it. " : HeldBack.Count + " were left alone: you have edited them. ");
                if (NotCarried.Count > 0) line += (NotCarried.Count == 1 ? "1 is not in this release and was not touched. " : NotCarried.Count + " are not in this release and were not touched. ");
                // The restart sentence replaces the reopen one rather than joining it: a plugin that is
                // about to be swapped makes "SimHub does not need restarting" false, and of the two
                // instructions the restart is the one that also reopens the dashboard.
                if (PluginStaged)
                {
                    return line + (FollowPlugin.Count == 0
                        ? UpdateWording.Restart
                        : UpdateWording.RestartWithDashboards(FollowPlugin.Count, EditedFollowing.Count, ReplaceEditedOnRestart));
                }
                return line + UpdateWording.ToSee(FontsWritten);
            }
        }
    }

    public sealed class UpdateService
    {
        private readonly IReleaseSource source;
        private readonly IInstallLog log;

        /// <summary>Cancelled by <see cref="StopDownloads"/>, and handed to every download so that it stops.</summary>
        private readonly CancellationTokenSource stopping = new CancellationTokenSource();

        /// <summary>Whether <see cref="StopDownloads"/> has run. Read and written under <see cref="WorkGate"/>, so that
        /// a run cannot start writing after shutdown has stopped waiting for it.</summary>
        private bool stopped;

        /// <summary>Why a run that SimHub's close stopped did not finish. Nothing on disk had been touched.</summary>
        public const string StoppedReason = "SimHub closed before the download finished";

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
        /// <remarks>
        /// Since #438 a release publishes the plugin and nothing else, so what this does for a current release is
        /// stage the plugin: the dashboards are inside it and are written as it starts, and the outcome says so
        /// through <see cref="UpdateOutcome.FollowPlugin"/>. The per-dashboard half stays for a release that
        /// publishes its packages, which every release before #438 did.
        /// </remarks>
        /// <param name="replaceEdited">
        /// Whether to replace a dashboard somebody has edited since OpenDash wrote it. False unless a person has
        /// been shown what that means and said yes.
        /// </param>
        /// <param name="progress">
        /// How far through the whole run this is, from 0 to 1, or null for a caller with nothing to draw. The
        /// downloads take the first half and the install the second, so that the bar crosses the panel once rather
        /// than reaching the end and starting again, which would say the run had finished twice.
        /// </param>
        /// <param name="settings">
        /// The settings to record what the run owes them in (<see cref="Settle"/>), or null for a caller that keeps
        /// none. Recorded before the run stops counting as work shutdown waits for, so End's save keeps it.
        /// </param>
        public UpdateOutcome Apply(DashboardInstaller installer, ReleaseInfo release, bool replaceEdited, Action<double> progress = null, OpenDashSettings settings = null)
        {
            if (installer == null || release == null) return new UpdateOutcome { Reason = "there is nothing to apply" };

            var plan = UpdatePlan.For(installer.Packages, release);
            // The plugin is fetched with the packages and not instead of them. A release that moves a
            // property name moves the dashboard that reads it in the same release, so updating one half
            // and not the other is the mismatch this whole path exists to avoid -- and it fails silently,
            // as a field drawing its fallback for ever with nothing in any log.
            var pluginAsset = release.PluginAsset();
            if (plan.IsEmpty && pluginAsset == null)
            {
                return new UpdateOutcome { Ok = true, NotCarried = plan.NotCarried };
            }

            var downloaded = new DownloadedPackageSource();
            // Each download owns one slice of the first half, and reports inside its own slice as its bytes arrive.
            // A package is a few megabytes over a home connection, so a bar that moved only between packages would
            // stand still for the part of the run that actually takes the time.
            var downloads = plan.Items.Count + (pluginAsset == null ? 0 : 1);
            var slice = 0.5 / downloads;
            var fetchedSoFar = 0;
            // Nothing is counted as work shutdown waits for until every byte is in hand. A download has written
            // nothing, so SimHub closing during one stops it (StopDownloads) rather than waiting out the grace.
            var cancel = stopping.Token;

            // The plugin first, and a failure to get it stops the run before anything on disk is touched.
            // That is the same rule the packages already follow, applied to the half that cannot be
            // rolled back by reinstalling: staging it after the dashboards had gone in would leave a
            // machine whose plugin is a release behind its packages with no way back but a manual
            // download.
            byte[] pluginBytes = null;
            if (pluginAsset != null)
            {
                var fetched = source.GetBytes(pluginAsset.DownloadUrl, within => progress?.Invoke(within * slice), cancel);
                if (cancel.IsCancellationRequested) return new UpdateOutcome { Reason = StoppedReason };
                if (!fetched.Ok) return new UpdateOutcome { Reason = "OpenDash itself could not be downloaded (" + fetched.Reason + ")" };
                if (!Digest.Matches(fetched.Bytes, pluginAsset.Digest))
                {
                    return new UpdateOutcome { Reason = "OpenDash itself did not download correctly" };
                }
                pluginBytes = fetched.Bytes;
                fetchedSoFar++;
            }

            foreach (var item in plan.Items)
            {
                var from = fetchedSoFar * slice;
                var fetched = source.GetBytes(item.Asset.DownloadUrl, within => progress?.Invoke(from + within * slice), cancel);
                if (cancel.IsCancellationRequested) return new UpdateOutcome { Reason = StoppedReason };
                if (!fetched.Ok) return new UpdateOutcome { Reason = item.FolderName + " could not be downloaded (" + fetched.Reason + ")" };
                if (!Digest.Matches(fetched.Bytes, item.Asset.Digest))
                {
                    return new UpdateOutcome { Reason = item.FolderName + " did not download correctly" };
                }
                downloaded.Add(item.Asset.Name, fetched.Bytes);
                fetchedSoFar++;
            }

            // From here the run writes, so it is counted and a SimHub closing now waits for it (WaitForIdle). It is let
            // in only if shutdown has not already been: End stops the downloads before it reads Busy, under the same
            // lock, so a run let in here is one End waits for and a run turned away here has written nothing.
            if (!StartWriting()) return new UpdateOutcome { Reason = StoppedReason };
            try
            {
                // One writer in DashTemplates at a time: a press on the panel that writes a folder waits for this run
                // rather than deleting a folder it is extracting into, and the run waits for the press (WriteLock).
                lock (WriteLock)
                {
                    var outcome = StageAndInstall(installer, replaceEdited, progress, plan, pluginBytes, downloaded);
                    if (settings != null) Settle(outcome, release, installer, settings, log);
                    return outcome;
                }
            }
            finally
            {
                FinishWriting();
            }
        }

        /// <summary>The half of <see cref="Apply"/> that writes: the plugin staged, then the packages installed.</summary>
        private UpdateOutcome StageAndInstall(DashboardInstaller installer, bool replaceEdited, Action<double> progress, UpdatePlan plan, byte[] pluginBytes, DownloadedPackageSource downloaded)
        {
            // Staged before the dashboards are written, because staging is a file written beside the one
            // in use and changes nothing until SimHub exits; the swap itself is PluginUpdate's, out of
            // this process entirely.
            var staged = pluginBytes == null ? null : PluginUpdate.Stage(pluginBytes, installer.SimHubRoot, log);
            // Armed here rather than at shutdown, for the reason PluginUpdate spells out: End is not
            // reached on a shutdown that times out or a process that is killed, and the waiter has to
            // outlive both.
            if (staged != null && staged.Ok) PluginUpdate.Launch(installer.SimHubRoot, log);
            // A plugin that could not be staged stops the run here, before any package is written, for the reason a
            // failed download does: the release's dashboards on the old plugin are the mismatch this path exists to
            // avoid (#616). For a release that carries the plugin and nothing else, which is every release since
            // #438, nothing has been done at all, and that is a failure too rather than "nothing to replace".
            if (staged != null && !staged.Ok)
            {
                return new UpdateOutcome { Reason = "OpenDash itself could not be put in place (" + staged.Error + ")", NotCarried = plan.NotCarried };
            }

            // Everything is in hand before anything on disk is touched, so a download that fails half way through
            // leaves the machine as it was rather than half updated.
            var target = new DashboardInstaller(installer.SimHubRoot, log, downloaded, installer.Record);
            var writing = DateTime.UtcNow;
            if (plan.IsEmpty) progress?.Invoke(1);
            else target.EnsureInstalled(force: true, replaceEdited: replaceEdited, progress: within => progress?.Invoke(0.5 + within * 0.5));

            // A package that failed to install is a failure, whatever the others did. Reporting Ok because the run
            // finished, and putting the reason in a field the wording ignored, told a user their dashboards were
            // updated when one of them had not been.
            // A package that could not even be read has no folder name, so the name it was fetched under stands in:
            // telling somebody that "" could not be installed is no better than telling them nothing.
            // What the release does not publish on its own it carries inside the plugin, once that is staged: the new
            // assembly embeds every package, and its first start writes each folder whose version it does not
            // match. Only then, though -- a release that carries no plugin brings nothing, and those folders really
            // are not in this release. (One whose plugin could not be staged has already stopped above.)
            var pluginStaged = staged != null && staged.Ok;
            var following = pluginStaged ? plan.NotCarried : new string[0];
            var editedFollowing = installer.Packages
                .Where(p => p.Edited && following.Contains(p.FolderName, StringComparer.OrdinalIgnoreCase))
                .Select(p => p.FolderName)
                .ToList();
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
                NotCarried = pluginStaged ? new string[0] : plan.NotCarried,
                FollowPlugin = following,
                EditedFollowing = editedFollowing,
                ReplaceEditedOnRestart = replaceEdited && editedFollowing.Count > 0,
                Reason = failed.Count == 0 ? null : (target.LastError ?? string.Join(", ", failed) + " could not be installed"),
                PluginStaged = pluginStaged,
                FontsWritten = PackageExtractor.FacesWrittenSince(installer.SimHubRoot, writing) > 0,
            };
        }

        /// <summary>
        /// What a run owes the settings, done on the run's own thread: the yes to replacing edited dashboards on
        /// restart, and the names the driver gave their screens put back over the packages' own titles.
        /// </summary>
        /// <remarks>
        /// Both used to be the panel's, in a completion that crossed to the interface thread (#613). That thread is
        /// the one End runs on, a SimHub that is closing never gets round to the completion, and a call into a
        /// settings page that has closed can throw (DashboardInstaller's progress remarks): the consent was lost and
        /// the restarted plugin held every edited folder back. Done here, they are in memory before the run stops
        /// counting as work shutdown waits for, so End's save keeps them; the caller saves them otherwise.
        ///
        /// A settings write off the interface thread, an exception to OpenDash.cs's rule that the interface thread is
        /// the only writer, and of the kind the installer's folder record already is. The screens are copied before
        /// they are walked, so a screen the panel adds meanwhile cannot break the walk.
        /// </remarks>
        public static void Settle(UpdateOutcome outcome, ReleaseInfo release, DashboardInstaller installer, OpenDashSettings settings, IInstallLog log = null)
        {
            if (outcome == null || release == null || installer == null || settings == null) return;
            log = log ?? NullInstallLog.Instance;
            // Spent by the next start, which is what writes the dashboards when they come inside the plugin.
            if (outcome.ReplaceEditedOnRestart) settings.ReplaceEditedFor = release.Version;
            try
            {
                // An update writes the stock folders from their packages, so it brings the packages' own titles
                // with it. Nothing is rewritten where the title already reads that way.
                foreach (var screen in settings.RigScreens().ToList()) ScreenInstaller.Retitle(screen, installer.SimHubRoot, installer.Record, log);
            }
            catch (Exception ex)
            {
                log.Warn("The screens' names could not be put back after the update: " + ex.Message);
            }
        }

        /// <summary>The folder an asset was fetched for, when the package itself could not be read.</summary>
        private static string FolderOf(UpdatePlan plan, string assetName) =>
            plan.Items.FirstOrDefault(i => i.Asset != null && i.Asset.Name == assetName)?.FolderName;

        private static readonly object WorkGate = new object();

        /// <summary>
        /// Held by whatever is writing DashTemplates off the interface thread, an update's install or a panel press
        /// (<see cref="WriteInBackground"/>), so that two of them never write at once.
        /// </summary>
        /// <remarks>
        /// Two writers over the same folders is the one combination that can delete a folder the other is extracting
        /// into. While the presses ran on the interface thread they could not overlap each other; off it, a second
        /// press waits here for the first, in the order they were pressed, which is the order they used to run in.
        /// Whether a press should be refused while another run is busy, rather than queued, is #606's, and it reads
        /// <see cref="Busy"/>, which counts every run that takes this lock.
        /// </remarks>
        private static readonly object WriteLock = new object();
        private static readonly ManualResetEventSlim Idle = new ManualResetEventSlim(true);
        private static int writing;

        /// <summary>
        /// Counts a run that is about to write DashTemplates, unless SimHub's close has already stopped this service.
        /// </summary>
        /// <remarks>
        /// A thread-pool thread is a background thread, so the CLR terminates it at process exit without unwinding
        /// and no finally runs: abandoning an install between the delete and the move leaves a dashboard folder that
        /// is simply gone, and abandoning it at all leaves the staging folder behind. Such work is counted, and
        /// WaitForIdle gives it a bounded chance to finish. Only that part is: the downloads before it have written
        /// nothing and are stopped instead, which used to be counted too and held SimHub's close for the whole grace
        /// over a download that could simply have been dropped (#613).
        /// </remarks>
        private bool StartWriting()
        {
            lock (WorkGate)
            {
                if (stopped) return false;
                if (writing++ == 0) Idle.Reset();
                return true;
            }
        }

        private static void FinishWriting()
        {
            lock (WorkGate)
            {
                if (--writing == 0) Idle.Set();
            }
        }

        /// <summary>
        /// Stops every download this service has in flight or will start, and turns away a run that has not begun
        /// writing yet. Called from End before it reads <see cref="Busy"/>.
        /// </summary>
        /// <remarks>
        /// Under the lock StartWriting takes, so the two cannot interleave: a run counted before this is one Busy
        /// reports and End waits for, and a run that reaches StartWriting after it writes nothing. The cancel is
        /// outside the lock, because it runs the downloads' own callbacks, which abort their requests.
        /// </remarks>
        public void StopDownloads()
        {
            lock (WorkGate)
            {
                stopped = true;
            }
            try
            {
                stopping.Cancel();
            }
            catch (Exception ex)
            {
                log.Warn("Stopping the update's downloads failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Applies an update off the caller's thread, and says how it went when it has.
        /// </summary>
        /// <remarks>
        /// The whole of a run the panel starts, so that nothing a run owes the rig waits on the interface thread:
        /// the settings are settled inside <see cref="Apply"/>, and <paramref name="finished"/> is called on the
        /// run's own thread, for the caller to save and to redraw however it likes. Nothing here is counted as work
        /// shutdown waits for; Apply counts its own writing.
        /// </remarks>
        /// <param name="finished">Called on the run's thread with the outcome, or with null when Apply threw, the
        /// exception having been logged.</param>
        public void ApplyInBackground(DashboardInstaller installer, ReleaseInfo release, bool replaceEdited, OpenDashSettings settings, Action<double> progress, Action<UpdateOutcome> finished)
        {
            InBackground(() =>
            {
                UpdateOutcome outcome = null;
                try
                {
                    outcome = Apply(installer, release, replaceEdited, progress, settings);
                }
                catch (Exception ex)
                {
                    log.Error("Applying " + (release == null ? "the update" : release.Version) + " failed: " + ex);
                }
                finished?.Invoke(outcome);
            }, log);
        }

        /// <summary>
        /// Runs a panel press that writes DashTemplates -- Reinstall everything, Put mine back, a screen written,
        /// resized or removed -- off the interface thread, and says when it has.
        /// </summary>
        /// <remarks>
        /// Each of those used to run on the click, and SimHub's whole window stopped answering while a package was
        /// extracted, a folder hashed and a backup zipped: two and a half seconds for Reinstall everything on a rig of
        /// five screens (#611). The press is counted as writing from the moment this returns, so <see cref="Busy"/>
        /// says so before the work has even started and a SimHub closing meanwhile waits for it (WaitForIdle), as it
        /// waits for an update's install. It is counted no longer once the work is done, and only then is
        /// <paramref name="finished"/> called, so that a page redrawn from it reads Busy as false.
        ///
        /// Work queued behind another writer waits for it (<see cref="WriteLock"/>). What the work does to SimHub's
        /// own objects is the caller's to keep off this thread: a light profile's install touches settings SimHub's
        /// interface is bound to, and stays on the interface thread.
        /// </remarks>
        /// <param name="write">The disk work. What it throws is logged and handed to <paramref name="finished"/>.</param>
        /// <param name="finished">Called on the work's thread with null, or with what the work threw.</param>
        /// <returns>False, with nothing run and <paramref name="finished"/> never called, when SimHub's close has
        /// already stopped this service: the press would write after shutdown had stopped waiting for writers.</returns>
        public bool WriteInBackground(Action write, Action<Exception> finished)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (!StartWriting()) return false;
            InBackground(() =>
            {
                Exception failure = null;
                try
                {
                    lock (WriteLock)
                    {
                        write();
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    log.Error("A write to SimHub's dashboards failed in the background: " + ex);
                }
                finally
                {
                    FinishWriting();
                }
                finished?.Invoke(failure);
            }, log);
            return true;
        }

        /// <summary>
        /// Runs work off the caller's thread, swallowing everything.
        /// </summary>
        /// <remarks>
        /// Nothing that waits on the network may run on the synchronous path of Init, and nothing may block on the
        /// thread SimHub calls it on. Init's own install of the rig's dashboards is the one write that stays there, and
        /// says why (OpenDash.Init).
        /// An unobserved exception on a thread-pool thread terminates the process on .NET Framework, so SimHub would
        /// vanish without a dialog; hence the catch that looks like it catches too much and does not. Nothing run
        /// here is waited for at shutdown: work that rewrites DashTemplates counts itself, as Apply does.
        /// </remarks>
        public static void InBackground(Action work, IInstallLog log = null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
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

        /// <summary>
        /// Waits for a run that is rewriting DashTemplates, and reports whether it finished.
        /// </summary>
        /// <remarks>
        /// Called from the plugin's End, which SimHub runs on shutdown. Waiting there is the opposite of the usual
        /// advice and is right here: the alternative is the process exiting between a DeleteDirectory and the
        /// Directory.Move that replaces what it deleted. A check or a download is never waited for, since abandoning
        /// a read costs nothing and a socket that never answers would hold SimHub's shutdown for its whole timeout.
        /// </remarks>
        public static bool WaitForIdle(TimeSpan timeout) => Idle.Wait(timeout);

        /// <summary>True while a run is staging the plugin or installing dashboards, or a panel press is writing them
        /// (<see cref="WriteInBackground"/>), which is what End waits for. False while an update is only downloading,
        /// since nothing on disk has been touched yet.</summary>
        public static bool Busy
        {
            get { lock (WorkGate) { return writing > 0; } }
        }
    }
}
