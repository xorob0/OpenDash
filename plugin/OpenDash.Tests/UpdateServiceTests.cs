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
            public string Listing { get; set; }
            public string ListingFailure { get; set; }
            public Dictionary<string, byte[]> Assets { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            public List<string> Requested { get; } = new List<string>();

            public FetchResult GetString(string url)
            {
                Requested.Add(url);
                if (ListingFailure != null) return FetchResult.Failed(ListingFailure);
                return new FetchResult { Ok = true, Body = Listing };
            }

            public FetchResult GetBytes(string url)
            {
                Requested.Add(url);
                return Assets.TryGetValue(url, out var bytes)
                    ? new FetchResult { Ok = true, Bytes = bytes }
                    : FetchResult.Failed("nothing at " + url);
            }
        }

        private static string ListingFor(string tag, params string[] folders)
        {
            var assets = folders.Select(f => "{\"name\":\"" + f.Replace(' ', '.') + ".simhubdash\",\"browser_download_url\":\"https://example.invalid/" + f.Replace(' ', '.') + "\",\"size\":1}");
            return "[{\"tag_name\":\"" + tag + "\",\"prerelease\":false,\"draft\":false,\"body\":\"notes\",\"assets\":[" + string.Join(",", assets) + "]}]";
        }

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

        // Applying

        private DashboardInstaller Installed(string folder, string version, IFolderRecord record)
        {
            var source = new DownloadedPackageSource().Add(folder + ".simhubdash", SyntheticPackage.Zip(folder, version).ToArray());
            var installer = new DashboardInstaller(root, null, source, record);
            installer.EnsureInstalled(false);
            return installer;
        }

        [Fact]
        public void An_update_replaces_what_is_installed_and_says_to_reopen()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("openDash", "0.1.0", record);

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "openDash" }, outcome.Updated);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Contains("Close the dashboard and start it again", outcome.Line);
            Assert.DoesNotContain("restart SimHub", outcome.Line);
        }

        [Fact]
        public void Bytes_that_are_not_what_GitHub_published_install_nothing()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("openDash", "0.1.0", record);

            var fetcher = new Fetcher
            {
                Listing = "[{\"tag_name\":\"v0.2.0\",\"assets\":[{\"name\":\"openDash.simhubdash\",\"browser_download_url\":\"https://example.invalid/openDash\",\"digest\":\"sha256:"
                    + new string('0', 64) + "\"}]}]",
            };
            fetcher.Assets["https://example.invalid/openDash"] = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Contains("did not arrive as GitHub published it", outcome.Reason);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
        }

        [Fact]
        public void A_download_that_fails_leaves_the_machine_as_it_was()
        {
            var installer = Installed("openDash", "0.1.0", new MemoryFolderRecord());
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "openDash") }; // no asset bytes registered
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Contains("could not be downloaded", outcome.Reason);
        }

        [Fact]
        public void A_dashboard_somebody_edited_is_left_alone_and_counted()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("openDash", "0.1.0", record);
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
            Assert.Contains("every dashboard has been edited", outcome.Line);
        }

        /// <summary>
        /// A run that finished is not a run that worked. Reporting Ok because nothing threw, with the reason in a
        /// field the wording ignored, told a user their dashboards were updated when one of them had not been.
        /// </summary>
        [Fact]
        public void A_package_that_could_not_be_installed_makes_the_whole_update_a_failure()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("openDash", "0.1.0", record);

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
            var done = new System.Threading.ManualResetEventSlim();
            UpdateService.InBackground(() => { try { throw new InvalidOperationException("boom"); } finally { done.Set(); } }, log);

            Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
            System.Threading.Thread.Sleep(50);
            Assert.Contains(log.Lines, line => line.Contains("failed in the background"));
        }
    }
}
