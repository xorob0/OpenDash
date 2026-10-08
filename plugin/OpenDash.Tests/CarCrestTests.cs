// CarCrestTests: the Porsche crest fetched into the user's own folder (#714), and every way that can fail
// ending in the same place -- an empty OpenDash.PorscheCrest, which the badge draws as the empty shield.
//
// No crest is in these tests. The bytes are a PNG signature and a few zeros, which is enough for the checks
// the plugin makes and is not the mark; the default address's own file is refused here for that reason.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class CarCrestTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "opendash-crest-" + Guid.NewGuid().ToString("N"));

        private const string Mine = "https://example.org/my-crest.png";

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

        private sealed class Source : IReleaseSource
        {
            public byte[] Bytes { get; set; } = Png;
            public string Failure { get; set; }
            public List<string> Requested { get; } = new List<string>();

            public FetchResult GetString(string url) { throw new NotSupportedException("the crest is fetched as bytes"); }

            public FetchResult GetBytes(string url, Action<double> progress = null, System.Threading.CancellationToken cancel = default)
            {
                Requested.Add(url);
                if (Failure != null) return FetchResult.Failed(Failure);
                return new FetchResult { Ok = true, Bytes = Bytes };
            }
        }

        private static ScreenInstance Screen(string theme, int width, int height)
        {
            return new ScreenInstance { Theme = theme, Width = width, Height = height, Kind = "face" };
        }

        [Fact]
        public void The_default_is_an_https_address_and_a_sha256_beside_it()
        {
            Assert.StartsWith("https://", CarCrestLibrary.DefaultUrl);
            Assert.Matches(new Regex("^[0-9a-f]{64}$"), CarCrestLibrary.DefaultSha256);
            Assert.Equal(CarCrestLibrary.DefaultSha256, CarCrestLibrary.ExpectedSha256(CarCrestLibrary.DefaultUrl));
            // An address the driver typed is theirs to vouch for.
            Assert.Null(CarCrestLibrary.ExpectedSha256(Mine));
            Assert.Equal(CarCrestLibrary.DefaultUrl, new OpenDashSettings().PorscheCrestUrl);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("   ", "")]
        [InlineData("not an address", "")]
        [InlineData("ftp://example.org/x.png", "")]
        [InlineData("file:///C:/crest.png", "")]
        [InlineData("  https://example.org/a.png ", "https://example.org/a.png")]
        public void An_address_is_kept_only_when_it_is_http_or_https(string given, string kept)
        {
            Assert.Equal(kept, CarCrestLibrary.NormaliseUrl(given));
        }

        [Fact]
        public void A_cleared_address_stays_cleared_and_a_missing_one_reads_the_default()
        {
            var cleared = new OpenDashSettings { PorscheCrestUrl = "" };
            cleared.Normalise();
            Assert.Equal("", cleared.PorscheCrestUrl);
            var old = new OpenDashSettings { PorscheCrestUrl = null };
            old.Normalise();
            Assert.Equal(CarCrestLibrary.DefaultUrl, old.PorscheCrestUrl);
        }

        [Theory]
        // The faces that keep the settings column keep the badge (#205); the squarer and smaller ones shed it,
        // the portrait has none, and no other theme draws this crest.
        [InlineData("porsche", 1280, 480, true)]
        [InlineData("porsche", 1280, 400, true)]
        [InlineData("porsche", 1280, 720, true)]
        [InlineData("porsche", 1920, 480, true)]
        [InlineData("porsche", 850, 480, false)]
        [InlineData("porsche", 800, 480, false)]
        [InlineData("porsche", 800, 286, false)]
        [InlineData("porsche", 600, 686, false)]
        [InlineData(null, 1280, 480, false)]
        [InlineData("default", 1280, 480, false)]
        public void Only_a_Porsche_face_that_keeps_the_badge_draws_it(string theme, int width, int height, bool draws)
        {
            Assert.Equal(draws, CarCrestLibrary.DrawsBadge(theme, width, height));
            Assert.Equal(draws, CarCrestLibrary.Wanted(new[] { Screen(theme, width, height) }));
        }

        [Fact]
        public void A_rig_with_no_Porsche_face_that_draws_the_badge_asks_for_nothing()
        {
            var source = new Source();
            var service = new CarCrestService(source, root);
            Assert.Equal(CarCrestState.Loading, service.State);
            Assert.Equal(CarCrestState.NotNeeded, service.Refresh(CarCrestLibrary.DefaultUrl, CarCrestLibrary.Wanted(new[] { Screen("porsche", 800, 286), Screen(null, 1280, 480) })));
            Assert.Empty(source.Requested);
            Assert.Equal("", service.Path);
        }

        [Fact]
        public void A_fetch_writes_the_file_and_publishes_its_full_path()
        {
            var source = new Source();
            var service = new CarCrestService(source, root);
            Assert.Equal(CarCrestState.Present, service.Refresh(Mine, true));
            Assert.Equal(new[] { Mine }, source.Requested);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, CarCrestLibrary.FileName)), service.Path);
            Assert.Equal(Png, File.ReadAllBytes(service.Path));
            Assert.False(File.Exists(service.Path + ".part"));
        }

        [Fact]
        public void A_crest_already_on_disk_is_drawn_with_no_network_and_no_request()
        {
            new CarCrestService(new Source(), root).Refresh(Mine, true);
            var offline = new Source { Failure = "no network" };
            var service = new CarCrestService(offline, root);
            Assert.Equal(CarCrestState.Present, service.Refresh(Mine, true));
            Assert.Empty(offline.Requested);
            Assert.NotEqual("", service.Path);
            // Even on a rig whose Porsche face has gone: the copy is read, not fetched.
            Assert.Equal(CarCrestState.Present, new CarCrestService(offline, root).Refresh(Mine, false));
        }

        [Fact]
        public void A_changed_address_is_a_missing_crest_until_its_own_file_arrives()
        {
            new CarCrestService(new Source(), root).Refresh(Mine, true);
            Assert.Equal("", CarCrestLibrary.OnDisk(root, "https://example.org/another.png"));
            var offline = new Source { Failure = "no network" };
            var service = new CarCrestService(offline, root);
            Assert.Equal(CarCrestState.Failed, service.Refresh("https://example.org/another.png", true));
            Assert.Equal("", service.Path);
            Assert.Equal("no network", service.Reason);
        }

        [Fact]
        public void Clearing_the_address_removes_the_copy_and_asks_for_nothing()
        {
            new CarCrestService(new Source(), root).Refresh(Mine, true);
            var source = new Source();
            var service = new CarCrestService(source, root);
            Assert.Equal(CarCrestState.Cleared, service.Refresh("", true));
            Assert.Empty(source.Requested);
            Assert.Equal("", service.Path);
            Assert.False(File.Exists(Path.Combine(root, CarCrestLibrary.FileName)));
        }

        [Fact]
        public void A_file_that_is_not_the_one_the_default_names_is_refused()
        {
            // The default address answering with anything else -- here, bytes that are a PNG and not the crest --
            // is a new upload or somebody else's file, and is not drawn.
            var service = new CarCrestService(new Source(), root);
            Assert.Equal(CarCrestState.Failed, service.Refresh(CarCrestLibrary.DefaultUrl, true));
            Assert.Equal("", service.Path);
            Assert.Contains("not the one expected", service.Reason);
            Assert.False(File.Exists(Path.Combine(root, CarCrestLibrary.FileName)));
        }

        [Fact]
        public void An_answer_that_is_not_an_image_is_refused()
        {
            var html = System.Text.Encoding.UTF8.GetBytes("<!doctype html><html>captive portal</html>");
            var service = new CarCrestService(new Source { Bytes = html }, root);
            Assert.Equal(CarCrestState.Failed, service.Refresh(Mine, true));
            Assert.Equal("", service.Path);
        }

        [Fact]
        public void A_file_too_large_to_be_a_crest_is_refused()
        {
            var big = new byte[CarCrestLibrary.MaxBytes + 1];
            Array.Copy(Png, big, Png.Length);
            Assert.False(CarCrestLibrary.Fetch(new Source { Bytes = big }, Mine, root).Ok);
        }

        [Fact]
        public void A_failure_leaves_the_copy_on_disk_alone()
        {
            new CarCrestService(new Source(), root).Refresh(Mine, true);
            var fetch = CarCrestLibrary.Fetch(new Source { Failure = "timed out" }, Mine, root);
            Assert.False(fetch.Ok);
            Assert.Equal(Png, File.ReadAllBytes(Path.Combine(root, CarCrestLibrary.FileName)));
        }

        [Fact]
        public void The_sha256_is_lower_case_hex()
        {
            // The SHA-256 of the empty input, which every implementation agrees on.
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", CarCrestLibrary.Sha256(new byte[0]));
        }

        [Fact]
        public void The_panel_says_each_state_in_one_line_and_offers_a_retry_only_after_a_failure()
        {
            Assert.Equal(PanelCrest.Present, PanelCrest.Line(CarCrestState.Present));
            Assert.Equal(PanelCrest.Downloading, PanelCrest.Line(CarCrestState.Fetching));
            Assert.Equal(PanelCrest.Cleared, PanelCrest.Line(CarCrestState.Cleared));
            Assert.Equal(PanelCrest.Failed, PanelCrest.Line(CarCrestState.Failed));
            Assert.Equal(PanelCrest.Loading, PanelCrest.Line(CarCrestState.Loading));
            Assert.Equal(PanelCrest.Loading, PanelCrest.Line(CarCrestState.NotNeeded));
            foreach (CarCrestState state in Enum.GetValues(typeof(CarCrestState)))
            {
                Assert.Equal(state == CarCrestState.Failed, PanelCrest.OffersRetry(state));
            }
            Assert.True(PanelCrest.Shown(Screen("porsche", 1280, 480)));
            Assert.False(PanelCrest.Shown(Screen("porsche", 800, 286)));
            Assert.False(PanelCrest.Shown(null));
        }

        [Fact]
        public void The_plugin_publishes_the_path_and_looks_at_the_crest_on_a_start()
        {
            // OpenDash.cs holds SimHub's PluginManager and cannot be compiled here, so this reads it.
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
            Assert.Contains("this.AttachDelegate(Contract.PorscheCrest, () => Crest.Path);", source);
            Assert.Contains("RefreshCrest();", source);
        }
    }
}
