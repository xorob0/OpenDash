// UpdateServiceTests.cs: asking and applying, against a fetcher that answers whatever the test needs. What is
// pinned here is the behaviour a user notices when something goes wrong: nothing is fetched when the setting is
// off, a failed answer does not move the clock, and a download that goes wrong leaves the machine as it was.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class UpdateServiceTests : IDisposable
    {
        /// <summary>A second dashboard, so that a plan can carry two items and a failure can be put on the later one.</summary>
        private const string SmallFolder = "openDash 1280x480";

        private readonly string root;

        public UpdateServiceTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        /// <summary>A fetcher that answers from a script and counts what it was asked for.</summary>
        private sealed class Fetcher : IReleaseSource
        {
            /// <summary>The whole listing, for a test whose feed is one page.</summary>
            public string Listing { set { Pages.Clear(); Pages.Add(value); } }

            /// <summary>One body per page, in the order GitHub would answer them.</summary>
            public List<string> Pages { get; } = new List<string>();

            public string ListingFailure { get; set; }

            /// <summary>The page number beyond which the fetch fails, for a feed that goes unreachable part way.</summary>
            public int FailFromPage { get; set; } = int.MaxValue;

            public Dictionary<string, byte[]> Assets { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            public List<string> Requested { get; } = new List<string>();

            public FetchResult GetString(string url)
            {
                Requested.Add(url);
                if (ListingFailure != null) return FetchResult.Failed(ListingFailure);
                var page = PageOf(url);
                if (page >= FailFromPage) return FetchResult.Failed("NameResolutionFailure");
                // Past the last page GitHub answers the empty listing, so the script does too.
                return new FetchResult { Ok = true, Body = page <= Pages.Count ? Pages[page - 1] : "[]" };
            }

            private static int PageOf(string url)
            {
                var marker = url.IndexOf("&page=", StringComparison.Ordinal);
                return marker < 0 ? 1 : int.Parse(url.Substring(marker + "&page=".Length));
            }

            public FetchResult GetBytes(string url, Action<double> progress = null)
            {
                Requested.Add(url);
                if (!Assets.TryGetValue(url, out var bytes)) return FetchResult.Failed("nothing at " + url);
                // The real client reports as the bytes arrive, once per whole percent. Three reports is enough for a
                // test to see the shape of what the caller does with them, and a fetch that fails reports nothing.
                // Guarded as the interface says every implementation must guard, so that a test which throws from
                // the callback measures what the caller does rather than what this fake forgot to do.
                foreach (var fraction in new[] { 0d, 0.5d, 1d })
                {
                    if (progress == null) break;
                    try { progress(fraction); } catch { }
                }
                return new FetchResult { Ok = true, Bytes = bytes };
            }
        }

        private static string ListingFor(string tag, params string[] folders)
        {
            var assets = folders.Select(f => "{\"name\":\"" + f.Replace(' ', '.') + ".simhubdash\",\"browser_download_url\":\"https://example.invalid/" + f.Replace(' ', '.') + "\",\"size\":1}");
            return "[{\"tag_name\":\"" + tag + "\",\"prerelease\":false,\"draft\":false,\"body\":\"notes\",\"assets\":[" + string.Join(",", assets) + "]}]";
        }

        /// <summary>A page of releases carrying no assets, in the order GitHub answers them. A hyphen in the tag
        /// makes it a pre-release, which is the rule release.yml applies when it cuts one.</summary>
        private static string PageOf(IEnumerable<string> tags) =>
            "[" + string.Join(",", tags.Select(t =>
                "{\"tag_name\":\"" + t + "\",\"prerelease\":" + (t.Contains("-") ? "true" : "false") + ",\"draft\":false,\"body\":\"notes\",\"assets\":[]}")) + "]";

        /// <summary>Candidate tags for one version, newest first.</summary>
        private static IEnumerable<string> Candidates(string version, int newest, int count) =>
            Enumerable.Range(0, count).Select(i => "v" + version + "-rc." + (newest - i));

        // Asking

        [Fact]
        public void Nothing_is_fetched_when_the_setting_is_off()
        {
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0") };
            long ticks = 0;
            var status = new UpdateService(fetcher).Check("0.1.0", enabled: false, lastCheckTicks: ref ticks, nowUtc: DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.Disabled, status.State);
            Assert.Empty(fetcher.Requested);
            Assert.Equal(0, ticks);
        }

        [Fact]
        public void A_check_that_is_not_due_asks_nothing_and_changes_nothing()
        {
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0") };
            var now = DateTime.UtcNow;
            var ticks = now.AddHours(-1).Ticks;
            var before = ticks;

            Assert.Null(new UpdateService(fetcher).Check("0.1.0", true, ref ticks, now, manual: false));
            Assert.Empty(fetcher.Requested);
            Assert.Equal(before, ticks);
        }

        [Fact]
        public void An_answer_that_did_not_arrive_does_not_move_the_clock()
        {
            // Otherwise a day with no network silences the check for the day after it as well.
            var fetcher = new Fetcher { ListingFailure = "NameResolutionFailure" };
            long ticks = 0;
            var status = new UpdateService(fetcher).Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.Unreachable, status.State);
            Assert.Equal(0, ticks);
        }

        [Fact]
        public void An_error_body_is_unreachable_rather_than_up_to_date()
        {
            var fetcher = new Fetcher { Listing = "{\"message\":\"Not Found\"}" };
            long ticks = 0;
            var status = new UpdateService(fetcher).Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.Unreachable, status.State);
            Assert.Equal(0, ticks);
        }

        [Fact]
        public void A_real_answer_concludes_and_moves_the_clock()
        {
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            long ticks = 0;
            var now = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
            var service = new UpdateService(fetcher);
            var status = service.Check("0.1.0", true, ref ticks, now, manual: false);

            Assert.Equal(UpdateState.UpdateAvailable, status.State);
            Assert.Equal("0.2.0", status.LatestVersion);
            Assert.Equal(now.Ticks, ticks);
            Assert.Single(service.LastReleases);
            Assert.Equal(UpdateCheck.ReleasesUrl, fetcher.Requested.Single());
        }

        // Reading past the first page

        [Fact]
        public void A_stable_release_behind_a_page_of_candidates_is_still_offered()
        {
            // Eleven candidates were cut after 0.3.0, so the first page of ten holds candidates and nothing else,
            // every one of which is discarded for a user on a stable version. Concluding on that page alone would
            // tell them they have the newest release while the one they should be offered sits on the next page.
            var fetcher = new Fetcher();
            fetcher.Pages.Add(PageOf(Candidates("0.4.0", newest: 11, count: 10)));
            fetcher.Pages.Add(PageOf(new[] { "v0.4.0-rc.1", "v0.3.0", "v0.2.0" }));
            long ticks = 0;
            var now = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
            var service = new UpdateService(fetcher);

            var status = service.Check("0.2.0", true, ref ticks, now, manual: true);

            Assert.Equal(UpdateState.UpdateAvailable, status.State);
            Assert.Equal("0.3.0", status.LatestVersion);
            Assert.Equal(now.Ticks, ticks);
            // Both pages are kept, since applying reads the release back out of them by version.
            Assert.Equal(13, service.LastReleases.Count);
            Assert.Equal(new[] { UpdateCheck.ReleasesUrl, UpdateCheck.ReleasesPage(2) }, fetcher.Requested);
        }

        [Fact]
        public void A_user_on_a_candidate_is_answered_by_the_first_page_alone()
        {
            var fetcher = new Fetcher();
            fetcher.Pages.Add(PageOf(Candidates("0.4.0", newest: 11, count: 10)));
            fetcher.Pages.Add(PageOf(new[] { "v0.4.0-rc.1", "v0.3.0", "v0.2.0" }));
            long ticks = 0;
            var service = new UpdateService(fetcher);

            var status = service.Check("0.4.0-rc.1", true, ref ticks, DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.UpdateAvailable, status.State);
            Assert.Equal("0.4.0-rc.11", status.LatestVersion);
            Assert.Equal(UpdateCheck.ReleasesUrl, fetcher.Requested.Single());
        }

        [Fact]
        public void A_listing_that_runs_out_is_up_to_date_and_costs_one_request()
        {
            // Every release this repository has cut is a candidate, and a page shorter than ten is the last one,
            // so a user on a stable version is answered by it rather than walking pages that do not exist.
            var fetcher = new Fetcher { Listing = PageOf(Candidates("0.1.0", newest: 3, count: 3)) };
            long ticks = 0;
            var status = new UpdateService(fetcher).Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.UpToDate, status.State);
            Assert.Equal(UpdateCheck.ReleasesUrl, fetcher.Requested.Single());
        }

        [Fact]
        public void A_page_that_did_not_arrive_is_unreachable_rather_than_up_to_date()
        {
            // The first page arrived and settled nothing, so what the second one would have held is unknown, and
            // reporting up to date on the strength of the first is the wrong answer this whole path avoids.
            var fetcher = new Fetcher { FailFromPage = 2 };
            fetcher.Pages.Add(PageOf(Candidates("0.4.0", newest: 11, count: 10)));
            long ticks = 0;
            var service = new UpdateService(fetcher);

            var status = service.Check("0.2.0", true, ref ticks, DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.Unreachable, status.State);
            Assert.Equal(0, ticks);
            Assert.Empty(service.LastReleases);
        }

        [Fact]
        public void A_run_of_candidates_longer_than_the_bound_stops_rather_than_walking_the_history()
        {
            var fetcher = new Fetcher();
            for (var page = 0; page < UpdateCheck.MaxPages + 2; page++)
            {
                fetcher.Pages.Add(PageOf(Candidates("0.4.0", newest: 200 - page * UpdateCheck.PageSize, count: UpdateCheck.PageSize)));
            }
            long ticks = 0;
            var status = new UpdateService(fetcher).Check("0.2.0", true, ref ticks, DateTime.UtcNow, manual: true);

            Assert.Equal(UpdateState.Unreachable, status.State);
            Assert.Equal(UpdateCheck.MaxPages, fetcher.Requested.Count);
            Assert.Equal(0, ticks);
        }

        // Applying

        /// <summary>Dashboards installed at one version. The order given is kept as far as the update plan, so a test
        /// that needs the failure on a later item can decide which folder that is.</summary>
        private DashboardInstaller Installed(string version, IFolderRecord record, params string[] folders)
        {
            var source = new DownloadedPackageSource();
            foreach (var folder in folders) source.Add(folder + ".simhubdash", SyntheticPackage.Zip(folder, version).ToArray());
            var installer = new DashboardInstaller(root, null, source, record);
            installer.EnsureInstalled(false);
            return installer;
        }

        [Fact]
        public void An_update_replaces_what_is_installed_and_says_to_reopen()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "openDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "openDash" }, outcome.Updated);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Contains("Close and reopen the dashboard", outcome.Line);
            Assert.DoesNotContain("restart SimHub", outcome.Line);
        }

        /// <summary>
        /// Two dashboards, of which the second fails its digest after the first has been fetched whole. One package
        /// would measure nothing: the only item would be the one that fails, so the test would stay green under an
        /// implementation that installed each package as it arrived and left the machine half updated.
        /// </summary>
        [Fact]
        public void Bytes_that_are_not_what_GitHub_published_install_nothing()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "openDash", SmallFolder);

            var fetcher = new Fetcher
            {
                // The first asset publishes no digest, which is accepted; the second publishes one nothing matches.
                Listing = "[{\"tag_name\":\"v0.2.0\",\"assets\":["
                    + "{\"name\":\"openDash.simhubdash\",\"browser_download_url\":\"https://example.invalid/openDash\"},"
                    + "{\"name\":\"openDash.1280x480.simhubdash\",\"browser_download_url\":\"https://example.invalid/openDash.1280x480\",\"digest\":\"sha256:"
                    + new string('0', 64) + "\"}]}]",
            };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            fetcher.Assets["https://example.invalid/openDash.1280x480"] = SyntheticPackage.Zip(SmallFolder, "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Contains("did not download correctly", outcome.Reason);
            Assert.Empty(outcome.Updated);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));
        }

        /// <summary>
        /// The same shape as the digest test, with the second asset absent rather than corrupt: the first download
        /// succeeds, so a first folder still at 0.1.0 is the whole of what atomicity means here.
        /// </summary>
        [Fact]
        public void A_download_that_fails_leaves_the_machine_as_it_was()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "openDash", SmallFolder);
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash", SmallFolder) };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            // Nothing is registered for the second, so its download is the one that fails.
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Contains("could not be downloaded", outcome.Reason);
            Assert.Empty(outcome.Updated);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));
        }

        [Fact]
        public void A_dashboard_somebody_edited_is_left_alone_and_counted()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "openDash");
            File.WriteAllText(Path.Combine(root, "DashTemplates", "openDash", "openDash.djson"), "{\"mine\":true}");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            installer.Refresh();
            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.Empty(outcome.Updated);
            Assert.Equal(new[] { "openDash" }, outcome.HeldBack);
            Assert.Equal("{\"mine\":true}", File.ReadAllText(Path.Combine(root, "DashTemplates", "openDash", "openDash.djson")));
            Assert.Contains("you have edited all of them", outcome.Line);
        }

        /// <summary>
        /// A run that finished is not a run that worked. Reporting Ok because nothing threw, with the reason in a
        /// field the wording ignored, told a user their dashboards were updated when one of them had not been.
        /// </summary>
        [Fact]
        public void A_package_that_could_not_be_installed_makes_the_whole_update_a_failure()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "openDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            // Bytes that are not a package at all: the install of this one fails while the run completes.
            fetcher.Assets["https://example.invalid/openDash"] = Encoding.UTF8.GetBytes("not a zip");
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Equal(new[] { "openDash" }, outcome.Failed);
            Assert.Empty(outcome.Updated);
            Assert.Contains("did not finish", outcome.Line);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
        }

        [Fact]
        public void A_dashboard_the_release_does_not_carry_is_mentioned_rather_than_computed_and_dropped()
        {
            // Two packages the plugin carries, of which the release publishes one. The build makes twenty-two and
            // v0.1.0-rc.2 published fourteen, so this is the ordinary case rather than a contrived one.
            var source = new DownloadedPackageSource()
                .Add("openDash.simhubdash", SyntheticPackage.Zip("openDash", "0.1.0").ToArray())
                .Add("openDash zones 1920x480.simhubdash", SyntheticPackage.Zip("openDash zones 1920x480", "0.1.0").ToArray());
            var installer = new DashboardInstaller(root, null, source, new MemoryFolderRecord());
            installer.EnsureInstalled(false);

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);
            Assert.True(outcome.Ok);
            Assert.Contains("not in this release", outcome.Line);
        }

        // What the bar is told

        /// <summary>
        /// The four promises the panel draws a bar on: it begins at nothing, it never goes backwards, it reaches the
        /// end exactly once, and it crosses the halfway mark when the downloading stops and the writing starts.
        /// </summary>
        [Fact]
        public void Applying_reports_a_run_that_starts_at_nothing_and_ends_at_everything()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "openDash", SmallFolder);

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash", SmallFolder) };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            fetcher.Assets["https://example.invalid/openDash.1280x480"] = SyntheticPackage.Zip(SmallFolder, "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var reported = new List<double>();
            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false, progress: reported.Add);

            Assert.True(outcome.Ok);
            Assert.Equal(0, reported.First());
            Assert.Equal(1, reported.Last());
            Assert.Equal(reported.OrderBy(f => f), reported);
            Assert.All(reported, f => Assert.InRange(f, 0, 1));

            // Half the bar is the two downloads and half is the two installs, so the two packages are each reported
            // as written: a run whose bar jumped from the last byte to the end would pass every assertion above.
            Assert.Contains(0.75, reported);
        }

        [Fact]
        public void A_run_the_bar_is_not_watched_for_is_applied_exactly_as_one_that_is()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "openDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            // The settings page can close while the download is in flight, which makes Dispatcher.Invoke throw. The
            // update is what the user asked for and the bar is decoration, so the one may not take the other down.
            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false,
                progress: _ => throw new InvalidOperationException("the panel has gone"));

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "openDash" }, outcome.Updated);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
        }

        /// <summary>
        /// Work that is rewriting DashTemplates must be waitable, because a thread-pool thread is a background
        /// thread the CLR terminates at process exit without unwinding: abandoning an install between the delete of
        /// a dashboard folder and the move that replaces it leaves the folder gone.
        /// </summary>
        [Fact]
        public void An_install_in_flight_is_waited_for_and_a_check_is_not()
        {
            Assert.False(UpdateService.Busy);
            Assert.True(UpdateService.WaitForIdle(TimeSpan.Zero));

            var release = new System.Threading.ManualResetEventSlim();
            var started = new System.Threading.ManualResetEventSlim();
            UpdateService.InBackground(() => { started.Set(); release.Wait(TimeSpan.FromSeconds(10)); }, null, mustFinish: true);

            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(UpdateService.Busy);
            // Shutdown would block here rather than killing the thread mid-install.
            Assert.False(UpdateService.WaitForIdle(TimeSpan.FromMilliseconds(50)));

            release.Set();
            Assert.True(UpdateService.WaitForIdle(TimeSpan.FromSeconds(10)));
            Assert.False(UpdateService.Busy);
        }

        [Fact]
        public void A_check_is_never_waited_for_because_abandoning_a_read_costs_nothing()
        {
            var release = new System.Threading.ManualResetEventSlim();
            var started = new System.Threading.ManualResetEventSlim();
            UpdateService.InBackground(() => { started.Set(); release.Wait(TimeSpan.FromSeconds(10)); });

            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            // A socket that never answers must not hold SimHub's shutdown for its whole timeout.
            Assert.False(UpdateService.Busy);
            Assert.True(UpdateService.WaitForIdle(TimeSpan.Zero));
            release.Set();
        }

        [Fact]
        public void Background_work_that_throws_does_not_take_the_process_with_it()
        {
            // On .NET Framework an unobserved exception on a thread-pool thread ends the process, which for a user
            // is SimHub vanishing without a dialog.
            var log = new ListLog();
            UpdateService.InBackground(() => { throw new InvalidOperationException("boom"); }, log);

            // Waiting on the log rather than on the work: an event the work item sets is signalled before the catch
            // that writes the line has run, so it orders nothing and a sleep is the only thing left to paper over it.
            Assert.True(log.Written.Wait(TimeSpan.FromSeconds(5)));
            Assert.Contains(log.Lines, line => line.Contains("failed in the background"));
        }
    }
}
