// UpdatePlanTests.cs: what an update fetches, and what it refuses. The digest check is the one that keeps bytes
// nobody published out of DashTemplates, and the plan is what keeps an update from adding screens nobody asked for.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class UpdatePlanTests
    {
        private static ReleaseInfo ReleaseWith(params string[] folders) => new ReleaseInfo
        {
            Tag = "v0.2.0",
            Assets = folders.Select(f => new ReleaseAsset
            {
                Name = f.Replace(' ', '.') + ".simhubdash",
                DownloadUrl = "https://example.invalid/" + f.Replace(' ', '.'),
                Size = 1,
            }).ToArray(),
        };

        private static PackageStatus Installed(string folder, string version = "0.1.0") =>
            new PackageStatus { FolderName = folder, InstalledVersion = version };

        [Fact]
        public void Only_what_is_installed_here_is_fetched()
        {
            // The release carries more than this machine has. Adding a screen size nobody asked for belongs to the
            // dashboard manager, not to an update.
            var release = ReleaseWith("openDash", "openDash 1280x480", "openDash 800 round");
            var plan = UpdatePlan.For(new[] { Installed("openDash"), Installed("openDash 800 round") }, release);

            Assert.Equal(new[] { "openDash", "openDash 800 round" }, plan.Items.Select(i => i.FolderName));
            Assert.Empty(plan.NotCarried);
            Assert.False(plan.IsEmpty);
        }

        [Fact]
        public void A_folder_the_release_does_not_carry_is_reported_and_left_alone()
        {
            // Real rather than hypothetical: the build makes twenty-two packages and rc.2 published fourteen.
            var plan = UpdatePlan.For(new[] { Installed("openDash"), Installed("openDash zones 1920x480") }, ReleaseWith("openDash"));

            Assert.Equal(new[] { "openDash" }, plan.Items.Select(i => i.FolderName));
            Assert.Equal(new[] { "openDash zones 1920x480" }, plan.NotCarried);
        }

        [Fact]
        public void A_package_that_is_not_installed_is_not_an_update()
        {
            var plan = UpdatePlan.For(new[] { new PackageStatus { FolderName = "openDash", InstalledVersion = null } }, ReleaseWith("openDash"));
            Assert.True(plan.IsEmpty);
            Assert.Empty(plan.NotCarried);
        }

        [Fact]
        public void Nothing_to_go_on_is_an_empty_plan_rather_than_a_throw()
        {
            Assert.True(UpdatePlan.For(null, ReleaseWith("openDash")).IsEmpty);
            Assert.True(UpdatePlan.For(new[] { Installed("openDash") }, null).IsEmpty);
        }

        [Fact]
        public void The_asset_is_found_by_the_name_GitHub_published()
        {
            var plan = UpdatePlan.For(new[] { Installed("openDash Pit wall portrait") }, ReleaseWith("openDash Pit wall portrait"));
            Assert.Equal("openDash.Pit.wall.portrait.simhubdash", plan.Items.Single().Asset.Name);
        }

        // The digest

        [Fact]
        public void Bytes_that_are_not_the_ones_published_are_refused()
        {
            var content = Encoding.UTF8.GetBytes("the package");
            var published = "sha256:" + System.BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(content)).Replace("-", "").ToLowerInvariant();

            Assert.True(Digest.Matches(content, published));
            Assert.True(Digest.Matches(content, published.ToUpperInvariant()));
            Assert.False(Digest.Matches(Encoding.UTF8.GetBytes("something else"), published));
            Assert.False(Digest.Matches(null, published));
        }

        [Fact]
        public void An_asset_published_without_a_digest_is_not_refused_for_it()
        {
            // Releases cut before GitHub reported digests carry none, and the package is still read and checked
            // before anything under DashTemplates is touched.
            var content = Encoding.UTF8.GetBytes("the package");
            Assert.True(Digest.Matches(content, null));
            Assert.True(Digest.Matches(content, "   "));
            Assert.True(Digest.Matches(content, "md5:whatever"));
        }

        // The source an installer reads downloaded bytes from

        [Fact]
        public void A_downloaded_package_reads_back_as_many_times_as_it_is_opened()
        {
            var package = SyntheticPackage.Zip("openDash", "0.2.0").ToArray();
            var source = new DownloadedPackageSource().Add("openDash.simhubdash", package);

            Assert.Equal(new[] { "openDash.simhubdash" }, source.Names);
            for (var i = 0; i < 3; i++)
            {
                using (var stream = source.Open("openDash.simhubdash"))
                {
                    Assert.Equal("0.2.0", PackageExtractor.ReadPackageVersion(stream, out var folder));
                    Assert.Equal("openDash", folder);
                }
            }
            Assert.Throws<FileNotFoundException>(() => source.Open("absent.simhubdash"));
        }

        [Fact]
        public void A_downloaded_package_installs_through_the_ordinary_installer()
        {
            // Which is the point: the same comparison, the same backup, the same font copy and the same font refresh
            // an embedded package gets.
            var root = Path.Combine(Path.GetTempPath(), "opendash-tests", System.Guid.NewGuid().ToString("N"));
            try
            {
                var source = new DownloadedPackageSource().Add("openDash.simhubdash", SyntheticPackage.Zip("openDash", "0.2.0").ToArray());
                var installer = new DashboardInstaller(root, null, source, new MemoryFolderRecord());
                installer.EnsureInstalled(false);

                Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
                Assert.True(installer.Packages.Single().Extracted);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
