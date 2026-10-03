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
        private const string SmallFolder = "OpenDash 1280x480";

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

            /// <summary>When set, every download waits for it to be set, as a slow connection would.</summary>
            public System.Threading.ManualResetEventSlim Gate { get; set; }

            /// <summary>Set once a download is waiting at <see cref="Gate"/>.</summary>
            public System.Threading.ManualResetEventSlim Waiting { get; } = new System.Threading.ManualResetEventSlim();

            /// <summary>A download that arrives whatever the cancel says, as one whose last byte landed as SimHub closed.</summary>
            public bool IgnoresCancel { get; set; }

            public FetchResult GetBytes(string url, Action<double> progress = null, System.Threading.CancellationToken cancel = default)
            {
                Requested.Add(url);
                if (Gate != null)
                {
                    Waiting.Set();
                    try
                    {
                        if (!Gate.Wait(TimeSpan.FromSeconds(10), IgnoresCancel ? System.Threading.CancellationToken.None : cancel)) return FetchResult.Failed("the gate was never opened");
                    }
                    catch (OperationCanceledException)
                    {
                        return FetchResult.Failed(ReleaseClient.Cancelled);
                    }
                }
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

        private sealed class ThrowingSource : IReleaseSource
        {
            public FetchResult GetString(string url) => throw new InvalidOperationException("boom");
            public FetchResult GetBytes(string url, Action<double> progress = null, System.Threading.CancellationToken cancel = default) => throw new InvalidOperationException("boom");
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
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
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
            var installer = Installed("0.1.0", record, "OpenDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "OpenDash" }, outcome.Updated);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Contains("Close and reopen the dashboard", outcome.Line);
            Assert.DoesNotContain("restart SimHub", outcome.Line);
            Assert.False(outcome.FontsWritten);
        }

        /// <summary>
        /// An update whose dashboard brings a face the rig does not have says to restart, because reopening does not
        /// draw it.
        /// </summary>
        /// <remarks>
        /// This is #441's run A in miniature: rc.6 installed with two faces, rc.7 adding openDashDisplay-Light.ttf,
        /// written into a running SimHub. The face was copied and the dashboard reopened, and the wordmark still drew
        /// in a heavier face, since SimHub reads DashFonts once per run. Saying "close and reopen" there was the
        /// false half of the sentence.
        /// </remarks>
        [Fact]
        public void An_update_that_brings_a_new_face_says_to_restart_rather_than_reopen()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "OpenDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
            fetcher.Assets["https://example.invalid/OpenDash"] =
                SyntheticPackage.Zip("OpenDash", "0.2.0", ("OpenDash/_SHFonts/openDashDisplay-Light.ttf", "font-light")).ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.True(outcome.FontsWritten);
            Assert.True(File.Exists(Path.Combine(root, PackageExtractor.DashFonts, "openDashDisplay-Light.ttf")));
            Assert.EndsWith(UpdateWording.RestartToSee, outcome.Line);
            Assert.DoesNotContain("reopen", outcome.Line);
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
            var installer = Installed("0.1.0", record, "OpenDash", SmallFolder);

            var fetcher = new Fetcher
            {
                // The first asset publishes no digest, which is accepted; the second publishes one nothing matches.
                Listing = "[{\"tag_name\":\"v0.2.0\",\"assets\":["
                    + "{\"name\":\"OpenDash.simhubdash\",\"browser_download_url\":\"https://example.invalid/OpenDash\"},"
                    + "{\"name\":\"OpenDash.1280x480.simhubdash\",\"browser_download_url\":\"https://example.invalid/OpenDash.1280x480\",\"digest\":\"sha256:"
                    + new string('0', 64) + "\"}]}]",
            };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            fetcher.Assets["https://example.invalid/OpenDash.1280x480"] = SyntheticPackage.Zip(SmallFolder, "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Contains("did not download correctly", outcome.Reason);
            Assert.Empty(outcome.Updated);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));
        }

        /// <summary>
        /// The same shape as the digest test, with the second asset absent rather than corrupt: the first download
        /// succeeds, so a first folder still at 0.1.0 is the whole of what atomicity means here.
        /// </summary>
        [Fact]
        public void A_download_that_fails_leaves_the_machine_as_it_was()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "OpenDash", SmallFolder);
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash", SmallFolder) };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            // Nothing is registered for the second, so its download is the one that fails.
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Contains("could not be downloaded", outcome.Reason);
            Assert.Empty(outcome.Updated);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));
        }

        [Fact]
        public void A_dashboard_somebody_edited_is_left_alone_and_counted()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "OpenDash");
            File.WriteAllText(Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson"), "{\"mine\":true}");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            installer.Refresh();
            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.Empty(outcome.Updated);
            Assert.Equal(new[] { "OpenDash" }, outcome.HeldBack);
            Assert.Equal("{\"mine\":true}", File.ReadAllText(Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson")));
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
            var installer = Installed("0.1.0", record, "OpenDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
            // Bytes that are not a package at all: the install of this one fails while the run completes.
            fetcher.Assets["https://example.invalid/OpenDash"] = Encoding.UTF8.GetBytes("not a zip");
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.Equal(new[] { "OpenDash" }, outcome.Failed);
            Assert.Empty(outcome.Updated);
            Assert.Contains("did not finish", outcome.Line);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        [Fact]
        public void A_dashboard_the_release_does_not_carry_is_mentioned_rather_than_computed_and_dropped()
        {
            // Two packages the plugin carries, of which the release publishes one. The build makes twenty-two and
            // v0.1.0-rc.2 published fourteen, so this is the ordinary case rather than a contrived one.
            var source = new DownloadedPackageSource()
                .Add("OpenDash.simhubdash", SyntheticPackage.Zip("OpenDash", "0.1.0").ToArray())
                .Add("OpenDash zones 1920x480.simhubdash", SyntheticPackage.Zip("OpenDash zones 1920x480", "0.1.0").ToArray());
            var installer = new DashboardInstaller(root, null, source, new MemoryFolderRecord());
            installer.EnsureInstalled(false);

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);
            Assert.True(outcome.Ok);
            Assert.Contains("not in this release", outcome.Line);
        }

        // A release that carries the plugin and nothing else (#438)

        private const string PluginUrl = "https://example.invalid/OpenDash-plugin.zip";

        private static string PluginOnlyListing(string tag) =>
            "[{\"tag_name\":\"" + tag + "\",\"prerelease\":false,\"draft\":false,\"body\":\"notes\",\"assets\":["
            + "{\"name\":\"" + PluginUpdate.AssetName + "\",\"browser_download_url\":\"" + PluginUrl + "\",\"size\":1}]}]";

        private static byte[] PluginZip(params string[] entries)
        {
            using (var buffer = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var name in entries)
                    {
                        using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("bytes of " + name);
                    }
                }
                return buffer.ToArray();
            }
        }

        /// <summary>
        /// What every update to a release cut since #438 is: the plugin is staged, no dashboard is written by this
        /// run, and the sentence says the dashboards come with the plugin. It used to say they were already up to
        /// date and not in this release, both false, since the new plugin's first start writes them.
        /// </summary>
        [Fact]
        public void A_release_carrying_only_the_plugin_says_the_dashboards_come_with_it()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "OpenDash", SmallFolder);
            var fetcher = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            fetcher.Assets[PluginUrl] = PluginZip("INSTALL.md", PluginUpdate.DllName, "OFL.txt");
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.True(outcome.Ok);
            Assert.True(outcome.PluginStaged);
            Assert.Empty(outcome.Updated);
            Assert.Empty(outcome.NotCarried);
            Assert.Equal(new[] { "OpenDash", SmallFolder }, outcome.FollowPlugin);
            Assert.False(outcome.ReplaceEditedOnRestart);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Equal(UpdateWording.RestartWithDashboards(2, 0, false), outcome.Line);
            Assert.DoesNotContain("not in this release", outcome.Line);
            Assert.DoesNotContain("already up to date", outcome.Line);
            Assert.True(PluginUpdate.Pending(root));
        }

        /// <summary>
        /// The yes to "replace your version" is given here and spent by the next start, which is what writes the
        /// dashboards. The run replaces nothing, and says what the answer will do and when.
        /// </summary>
        [Fact]
        public void A_yes_to_replacing_an_edited_dashboard_is_handed_to_the_start_that_writes_it()
        {
            var record = new MemoryFolderRecord();
            var installer = Installed("0.1.0", record, "OpenDash", SmallFolder);
            var mine = Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson");
            File.WriteAllText(mine, "{\"mine\":true}");
            installer.Refresh();

            var fetcher = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            fetcher.Assets[PluginUrl] = PluginZip(PluginUpdate.DllName);
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var yes = service.Apply(installer, service.LastReleases[0], replaceEdited: true);

            Assert.True(yes.Ok);
            Assert.Equal(new[] { "OpenDash" }, yes.EditedFollowing);
            Assert.True(yes.ReplaceEditedOnRestart);
            Assert.Equal("{\"mine\":true}", File.ReadAllText(mine));
            Assert.Contains("replaces the one you edited", yes.Line);

            var no = service.Apply(installer, service.LastReleases[0], replaceEdited: false);
            Assert.False(no.ReplaceEditedOnRestart);
            Assert.Contains("The one you edited is left alone", no.Line);
        }

        /// <summary>
        /// A second screen of a size follows the plugin like the first (#455). No release publishes its folder on
        /// its own, and it used to be left out of the count altogether, because the installer the plan is read from
        /// had no entry for it; the start that followed then left it on the old dashboard as well.
        /// </summary>
        [Fact]
        public void A_second_screen_of_a_size_is_among_the_dashboards_that_follow_the_plugin()
        {
            const string package = "OpenDash 850x480.simhubdash";
            var wheel = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Face850x480", Name = "Tim wheel", Folder = "OpenDash 850x480", Package = package };
            var rim = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Rim2", Name = "Rim (2)", Folder = "OpenDash Rim (2)", Package = package };
            var source = new DownloadedPackageSource().Add(package, SyntheticPackage.Instanceable("OpenDash 850x480", "Face850x480", "0.1.0").ToArray());
            var installer = new DashboardInstaller(root, null, source, new MemoryFolderRecord()) { Rig = () => new[] { wheel, rim } };
            installer.EnsureInstalled(false);
            File.WriteAllText(Path.Combine(root, "DashTemplates", "OpenDash Rim (2)", "OpenDash Rim (2).djson"), "{\"mine\":true}");
            installer.Refresh();

            var fetcher = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            fetcher.Assets[PluginUrl] = PluginZip(PluginUpdate.DllName);
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: true);

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "OpenDash 850x480", "OpenDash Rim (2)" }, outcome.FollowPlugin);
            // And the yes to replacing it is carried across the restart for it as well.
            Assert.Equal(new[] { "OpenDash Rim (2)" }, outcome.EditedFollowing);
            Assert.True(outcome.ReplaceEditedOnRestart);
            Assert.Equal(UpdateWording.RestartWithDashboards(2, 1, true), outcome.Line);
        }

        /// <summary>
        /// A leftover folder somebody edited, which no screen on the rig is written from, is asked about by nothing and
        /// counted by nothing: the start that follows writes the rig's folders and leaves it as it is (#468).
        /// </summary>
        /// <remarks>
        /// The plan was read from every entry the installer had, so the Companion left over from before ADR 0017 was
        /// among the dashboards said to come with the plugin and among the edited ones a yes was carried across the
        /// restart for: "3 dashboards, 2 of them yours" for a start that wrote two and replaced one.
        /// </remarks>
        [Fact]
        public void A_leftover_folder_outside_the_rig_neither_follows_the_plugin_nor_is_asked_about()
        {
            const string package = "OpenDash 850x480.simhubdash";
            var wheel = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Face850x480", Name = "Tim wheel", Folder = "OpenDash 850x480", Package = package };
            var rim = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Rim2", Name = "Rim (2)", Folder = "OpenDash Rim (2)", Package = package };
            var source = new DownloadedPackageSource()
                .Add(package, SyntheticPackage.Instanceable("OpenDash 850x480", "Face850x480", "0.1.0").ToArray())
                .Add("OpenDash Companion.simhubdash", SyntheticPackage.Zip("OpenDash Companion", "0.1.0").ToArray());
            var record = new MemoryFolderRecord();
            // Written by a plugin from before ADR 0017, which wrote every package it carried.
            new DashboardInstaller(root, null, source, record).EnsureInstalled(false);
            var installer = new DashboardInstaller(root, null, source, record) { Rig = () => new[] { wheel, rim } };
            installer.EnsureInstalled(false);
            File.WriteAllText(Path.Combine(root, "DashTemplates", "OpenDash Companion", "OpenDash Companion.djson"), "{\"mine\":true}");
            File.WriteAllText(Path.Combine(root, "DashTemplates", "OpenDash Rim (2)", "OpenDash Rim (2).djson"), "{\"rim\":true}");
            installer.Refresh();

            // The question the Update button asks before it downloads anything.
            Assert.Equal(new[] { "OpenDash Rim (2)" }, installer.EditedFolders);

            var fetcher = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            fetcher.Assets[PluginUrl] = PluginZip(PluginUpdate.DllName);
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: true);

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "OpenDash 850x480", "OpenDash Rim (2)" }, outcome.FollowPlugin);
            Assert.Equal(new[] { "OpenDash Rim (2)" }, outcome.EditedFollowing);
            Assert.Empty(outcome.NotCarried);
            Assert.Equal(UpdateWording.RestartWithDashboards(2, 1, true), outcome.Line);
        }

        /// <summary>
        /// A plugin-only release whose plugin could not be staged has done nothing at all, which is a failure to
        /// report rather than "There was nothing to replace".
        /// </summary>
        [Fact]
        public void A_plugin_only_release_whose_plugin_cannot_be_staged_is_a_failure()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "OpenDash");
            var fetcher = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            fetcher.Assets[PluginUrl] = PluginZip("INSTALL.md", "OFL.txt");
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.False(outcome.PluginStaged);
            Assert.Empty(outcome.FollowPlugin);
            Assert.Contains("did not finish", outcome.Line);
            Assert.Contains("OpenDash itself could not be put in place", outcome.Line);
            Assert.False(PluginUpdate.Pending(root));
        }

        /// <summary>
        /// A release that publishes its packages beside the plugin, whose plugin downloads but cannot be written to
        /// disk, installs none of them (#616): new dashboards on the old plugin is the mismatch the run exists to
        /// avoid, and it is no less one for the plugin having failed after the download rather than during it.
        /// </summary>
        [Fact]
        public void A_plugin_that_cannot_be_staged_stops_the_packages_it_was_published_with()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "OpenDash");
            var listing = ListingFor("v0.2.0", "OpenDash");
            var fetcher = new Fetcher
            {
                Listing = listing.Insert(listing.LastIndexOf("]}]", StringComparison.Ordinal),
                    ",{\"name\":\"" + PluginUpdate.AssetName + "\",\"browser_download_url\":\"" + PluginUrl + "\",\"size\":1}"),
            };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            fetcher.Assets[PluginUrl] = PluginZip(PluginUpdate.DllName);
            // A sound download that cannot be written: a folder stands where the assembly is written before it is
            // moved into place, which is the disk refusing a write and not the release being wrong.
            Directory.CreateDirectory(PluginUpdate.StagedPath(root) + ".part");
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false);

            Assert.False(outcome.Ok);
            Assert.False(outcome.PluginStaged);
            Assert.Empty(outcome.Updated);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Contains("OpenDash itself could not be put in place", outcome.Line);
            Assert.False(PluginUpdate.Pending(root));
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
            var installer = Installed("0.1.0", record, "OpenDash", SmallFolder);

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash", SmallFolder) };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            fetcher.Assets["https://example.invalid/OpenDash.1280x480"] = SyntheticPackage.Zip(SmallFolder, "0.2.0").ToArray();
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
            var installer = Installed("0.1.0", record, "OpenDash");

            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash") };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);

            // The settings page can close while the download is in flight, which makes Dispatcher.Invoke throw. The
            // update is what the user asked for and the bar is decoration, so the one may not take the other down.
            var outcome = service.Apply(installer, service.LastReleases[0], replaceEdited: false,
                progress: _ => throw new InvalidOperationException("the panel has gone"));

            Assert.True(outcome.Ok);
            Assert.Equal(new[] { "OpenDash" }, outcome.Updated);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        // A run in the background, and SimHub closing during it (#613)

        /// <summary>A run of a release that publishes the OpenDash package, applied the way the panel applies it,
        /// with every download held at the fetcher's gate until the test opens it.</summary>
        private (UpdateService service, Fetcher fetcher, DashboardInstaller installer) HeldRun(IFolderRecord record = null)
        {
            var installer = Installed("0.1.0", record ?? new MemoryFolderRecord(), "OpenDash");
            var fetcher = new Fetcher { Listing = ListingFor("v0.2.0", "OpenDash"), Gate = new System.Threading.ManualResetEventSlim() };
            fetcher.Assets["https://example.invalid/OpenDash"] = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);
            return (service, fetcher, installer);
        }

        /// <summary>
        /// Only the part of a run that writes is waited for at shutdown. A thread-pool thread is a background thread
        /// the CLR terminates at process exit without unwinding, so abandoning an install between the delete of a
        /// dashboard folder and the move that replaces it leaves the folder gone, and End waits for that part. A
        /// download has written nothing, and holding SimHub's close for twenty seconds over one was the bug.
        /// </summary>
        [Fact]
        public void Busy_is_false_while_a_run_is_only_downloading_and_true_while_it_writes()
        {
            Assert.False(UpdateService.Busy);
            var (service, fetcher, installer) = HeldRun();
            var whileWriting = new List<(bool busy, bool idle)>();
            Action<double> progress = fraction =>
            {
                // The second half of the bar is the install.
                if (fraction > 0.5) lock (whileWriting) whileWriting.Add((UpdateService.Busy, UpdateService.WaitForIdle(TimeSpan.Zero)));
            };
            UpdateOutcome outcome = null;
            var finished = new System.Threading.ManualResetEventSlim();

            service.ApplyInBackground(installer, service.LastReleases[0], false, null, progress, o => { outcome = o; finished.Set(); });

            Assert.True(fetcher.Waiting.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(UpdateService.Busy);
            // Shutdown would not wait here: nothing on disk has been touched.
            Assert.True(UpdateService.WaitForIdle(TimeSpan.Zero));

            fetcher.Gate.Set();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(outcome.Ok, outcome.Reason);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            // And it would have waited while the dashboards were being written.
            Assert.NotEmpty(whileWriting);
            Assert.All(whileWriting, seen => Assert.Equal((true, false), seen));
            Assert.False(UpdateService.Busy);
        }

        /// <summary>
        /// SimHub closing stops a download rather than waiting for it, and the run writes nothing afterwards.
        /// </summary>
        [Fact]
        public void Closing_SimHub_stops_a_download_and_the_run_writes_nothing()
        {
            var (service, fetcher, installer) = HeldRun();
            UpdateOutcome outcome = null;
            var finished = new System.Threading.ManualResetEventSlim();
            service.ApplyInBackground(installer, service.LastReleases[0], false, null, null, o => { outcome = o; finished.Set(); });
            Assert.True(fetcher.Waiting.Wait(TimeSpan.FromSeconds(5)));

            // What End does: stop the downloads, then wait only if a run is writing.
            service.StopDownloads();
            Assert.False(UpdateService.Busy);

            Assert.True(finished.Wait(TimeSpan.FromSeconds(5)), "the download was stopped rather than left to its gate");
            Assert.False(outcome.Ok);
            Assert.Equal(UpdateService.StoppedReason, outcome.Reason);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        /// <summary>
        /// A download whose last byte lands as SimHub closes does not start writing after End has decided there was
        /// nothing to wait for, which would be the abandoned install the wait exists to prevent.
        /// </summary>
        [Fact]
        public void A_download_that_arrives_after_SimHub_closed_writes_nothing()
        {
            var (service, fetcher, installer) = HeldRun();
            fetcher.IgnoresCancel = true;
            UpdateOutcome outcome = null;
            var finished = new System.Threading.ManualResetEventSlim();
            service.ApplyInBackground(installer, service.LastReleases[0], false, null, null, o => { outcome = o; finished.Set(); });
            Assert.True(fetcher.Waiting.Wait(TimeSpan.FromSeconds(5)));

            service.StopDownloads();
            Assert.False(UpdateService.Busy);
            fetcher.Gate.Set();

            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(UpdateService.StoppedReason, outcome.Reason);
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.False(PluginUpdate.Pending(root));
        }

        /// <summary>
        /// The yes to replacing edited dashboards on restart is in the settings when the run says it has finished,
        /// with nothing on the panel's side having run. It used to be recorded only by the panel's completion on the
        /// interface thread, which a closed page or a closing SimHub never runs, and the restarted plugin then held
        /// every edited folder back.
        /// </summary>
        [Fact]
        public void ReplaceEditedOnRestart_reaches_the_settings_without_the_panel_s_callback()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "OpenDash", SmallFolder);
            File.WriteAllText(Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson"), "{\"mine\":true}");
            installer.Refresh();
            var fetcher = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            fetcher.Assets[PluginUrl] = PluginZip(PluginUpdate.DllName);
            long ticks = 0;
            var service = new UpdateService(fetcher);
            service.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);
            var settings = new OpenDashSettings();
            string recorded = "the run never finished";
            UpdateOutcome outcome = null;
            var finished = new System.Threading.ManualResetEventSlim();

            // The panel's part is the callback, and this one does nothing but look.
            service.ApplyInBackground(installer, service.LastReleases[0], true, settings, null, o =>
            {
                outcome = o;
                recorded = settings.ReplaceEditedFor;
                finished.Set();
            });

            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(outcome.ReplaceEditedOnRestart);
            Assert.Equal("0.2.0", recorded);
        }

        /// <summary>
        /// The names the driver gave their screens go back over the packages' own titles by the run itself, and the
        /// record is taken again of what that wrote, so the folder still reads as OpenDash's.
        /// </summary>
        [Fact]
        public void The_run_puts_the_screens_names_back_without_the_panel()
        {
            var record = new MemoryFolderRecord();
            var (service, fetcher, installer) = HeldRun(record);
            fetcher.Gate.Set();
            var settings = new OpenDashSettings
            {
                Rig = new List<ScreenInstance> { new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = "OpenDash", Name = "Main dash" } },
            };
            var finished = new System.Threading.ManualResetEventSlim();

            service.ApplyInBackground(installer, service.LastReleases[0], false, settings, null, o => finished.Set());

            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            var folder = PackageExtractor.InstalledFolder(root, "OpenDash");
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Contains("\"Title\":\"Main dash\"", File.ReadAllText(Path.Combine(folder, "OpenDash.djson.metadata")));
            Assert.Equal(FolderFingerprint.Of(folder), record.Get("OpenDash"));
        }

        /// <summary>
        /// The plugin is what the two halves above are wired to, and its file needs SimHub to compile, so the wiring is
        /// read rather than run: End stops the downloads before it reads Busy, and the run the panel starts hands the
        /// service the settings and posts the save itself rather than leaving it to the page.
        /// </summary>
        [Fact]
        public void The_plugin_stops_downloads_at_shutdown_and_owns_the_run_s_save()
        {
            var code = System.Text.RegularExpressions.Regex.Replace(
                RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs")), @"\s+", " ");
            var end = code.Substring(code.IndexOf("public void End(PluginManager pluginManager)", StringComparison.Ordinal));
            var stop = end.IndexOf("updates?.StopDownloads();", StringComparison.Ordinal);
            var busy = end.IndexOf("if (UpdateService.Busy)", StringComparison.Ordinal);
            var save = end.IndexOf("SaveSettings();", StringComparison.Ordinal);
            Assert.True(stop >= 0 && busy > stop && save > busy, "End stops the downloads, then waits for a run that writes, then saves");

            Assert.Contains("Updates.ApplyInBackground(Installer, release, replaceEdited, Settings, progress, outcome => { OnInterfaceThread(SaveSettings); applied?.Invoke(outcome); });", code);
        }

        /// <summary>A run that throws still says it has finished, with no outcome, so the page can stop showing it.</summary>
        [Fact]
        public void A_run_that_throws_still_finishes_and_says_so()
        {
            var installer = Installed("0.1.0", new MemoryFolderRecord(), "OpenDash");
            var lister = new Fetcher { Listing = PluginOnlyListing("v0.2.0") };
            long ticks = 0;
            var asked = new UpdateService(lister);
            asked.Check("0.1.0", true, ref ticks, DateTime.UtcNow, manual: true);
            var log = new ListLog();
            // A source that throws stands in for anything Apply did not expect.
            var service = new UpdateService(new ThrowingSource(), log);
            UpdateOutcome outcome = new UpdateOutcome();
            var finished = new System.Threading.ManualResetEventSlim();

            service.ApplyInBackground(installer, asked.LastReleases[0], false, null, null, o => { outcome = o; finished.Set(); });

            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(outcome);
            Assert.Contains(log.Lines, line => line.Contains("Applying 0.2.0 failed"));
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

        /// <summary>
        /// A press on the panel that writes DashTemplates runs off the interface thread and is counted as writing from
        /// the moment it is handed over until it has finished, so a SimHub closing meanwhile waits for it and #606 can
        /// refuse a second writer on Busy; it says it has finished only once it no longer counts (#611).
        /// </summary>
        [Fact]
        public void A_panel_write_runs_off_the_caller_s_thread_and_counts_as_writing_until_it_is_done()
        {
            var service = new UpdateService(new Fetcher());
            var caller = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var release = new System.Threading.ManualResetEventSlim();
            var running = new System.Threading.ManualResetEventSlim();
            var finished = new System.Threading.ManualResetEventSlim();
            var ranOn = 0;
            var busyAtFinish = true;
            Exception handed = new Exception("not called");

            Assert.True(service.WriteInBackground(() =>
            {
                ranOn = System.Threading.Thread.CurrentThread.ManagedThreadId;
                running.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            }, failure =>
            {
                handed = failure;
                busyAtFinish = UpdateService.Busy;
                finished.Set();
            }));

            // Counted before the work has even started: the press is a writer from the moment it returns.
            Assert.True(UpdateService.Busy);
            Assert.True(running.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotEqual(caller, ranOn);
            Assert.False(UpdateService.WaitForIdle(TimeSpan.Zero));
            release.Set();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(handed);
            Assert.False(busyAtFinish);
            Assert.True(UpdateService.WaitForIdle(TimeSpan.Zero));
        }

        /// <summary>Two writers never overlap: a second press waits for the first, as it did when both ran on the click.</summary>
        [Fact]
        public void A_second_panel_write_waits_for_the_first()
        {
            var service = new UpdateService(new Fetcher());
            var release = new System.Threading.ManualResetEventSlim();
            var firstRunning = new System.Threading.ManualResetEventSlim();
            var bothDone = new System.Threading.CountdownEvent(2);
            var order = new List<string>();

            service.WriteInBackground(() =>
            {
                lock (order) order.Add("first starts");
                firstRunning.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                lock (order) order.Add("first ends");
            }, failure => bothDone.Signal());
            Assert.True(firstRunning.Wait(TimeSpan.FromSeconds(5)));
            service.WriteInBackground(() => { lock (order) order.Add("second"); }, failure => bothDone.Signal());

            // Long enough for the second to have run had nothing held it.
            System.Threading.Thread.Sleep(200);
            lock (order) Assert.Equal(new[] { "first starts" }, order);
            release.Set();
            Assert.True(bothDone.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { "first starts", "first ends", "second" }, order);
            Assert.False(UpdateService.Busy);
        }

        /// <summary>A write that throws is logged and handed to its ending, which still runs, so the page stops showing it.</summary>
        [Fact]
        public void A_panel_write_that_throws_is_logged_and_still_finishes()
        {
            var log = new ListLog();
            var service = new UpdateService(new Fetcher(), log);
            var finished = new System.Threading.ManualResetEventSlim();
            Exception handed = null;

            Assert.True(service.WriteInBackground(() => { throw new IOException("disk full"); }, failure => { handed = failure; finished.Set(); }));

            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsType<IOException>(handed);
            Assert.Contains(log.Lines, line => line.Contains("failed in the background") && line.Contains("disk full"));
            Assert.False(UpdateService.Busy);
        }

        /// <summary>
        /// Every press on the panel that writes, puts back or removes a dashboard folder does it inside WriteThen, which
        /// hands it to <see cref="UpdateService.WriteInBackground"/>: none runs on the click any more, where SimHub's
        /// whole window stopped answering for as long as it took (#611). The panel is WPF and is read rather than run.
        /// </summary>
        [Fact]
        public void The_panel_writes_dashboards_only_off_the_interface_thread()
        {
            var writes = new[] { "Installer.Write(", ".EnsureInstalled(", "PackageExtractor.Restore(", "ScreenInstaller.Remove(" };
            var method = new System.Text.RegularExpressions.Regex(@"(private|public|internal|protected)[^;{}=]*\(");
            var onTheClick = new List<string>();
            var seen = 0;
            foreach (var path in RepoPaths.SettingsControlSources())
            {
                var code = RepoPaths.Code(path);
                foreach (var write in writes)
                {
                    for (var at = code.IndexOf(write, StringComparison.Ordinal); at >= 0; at = code.IndexOf(write, at + 1, StringComparison.Ordinal))
                    {
                        seen++;
                        var before = code.Substring(0, at);
                        var declared = method.Matches(before).Cast<System.Text.RegularExpressions.Match>().Select(m => m.Index).DefaultIfEmpty(-1).Max();
                        var handed = Math.Max(before.LastIndexOf("WriteThen(", StringComparison.Ordinal), before.LastIndexOf("WriteScreenThen(", StringComparison.Ordinal));
                        if (handed < declared) onTheClick.Add(Path.GetFileName(path) + ": " + code.Substring(at, Math.Min(60, code.Length - at)).Split('\n')[0]);
                    }
                }
            }
            // Reinstall everything, Put mine back, and the Screens page's add, duplicate, rename, resize, reinstall and
            // remove, with Home's install again.
            Assert.True(seen >= 9, "found only " + seen + " writes; the patterns no longer match the panel");
            Assert.True(onTheClick.Count == 0, "Written on the interface thread:\n" + string.Join("\n", onTheClick));
        }

        /// <summary>Once SimHub's close has stopped the service a press writes nothing: shutdown has stopped waiting for writers.</summary>
        [Fact]
        public void A_panel_write_after_shutdown_has_begun_is_turned_away()
        {
            var service = new UpdateService(new Fetcher());
            service.StopDownloads();
            var ran = false;
            var ended = false;

            Assert.False(service.WriteInBackground(() => ran = true, failure => ended = true));

            Assert.False(UpdateService.Busy);
            System.Threading.Thread.Sleep(100);
            Assert.False(ran);
            Assert.False(ended);
        }
    }
}
