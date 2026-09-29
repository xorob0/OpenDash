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
            var release = ReleaseWith("OpenDash", "OpenDash 1280x480", "OpenDash 800 round");
            var plan = UpdatePlan.For(new[] { Installed("OpenDash"), Installed("OpenDash 800 round") }, release);

            Assert.Equal(new[] { "OpenDash", "OpenDash 800 round" }, plan.Items.Select(i => i.FolderName));
            Assert.Empty(plan.NotCarried);
            Assert.False(plan.IsEmpty);
        }

        [Fact]
        public void A_folder_the_release_does_not_carry_is_reported_and_left_alone()
        {
            // Real rather than hypothetical: the build makes twenty-two packages and rc.2 published fourteen.
            var plan = UpdatePlan.For(new[] { Installed("OpenDash"), Installed("OpenDash zones 1920x480") }, ReleaseWith("OpenDash"));

            Assert.Equal(new[] { "OpenDash" }, plan.Items.Select(i => i.FolderName));
            Assert.Equal(new[] { "OpenDash zones 1920x480" }, plan.NotCarried);
        }

        /// <summary>
        /// A folder outside the rig is installed here and still not the update's, since nothing writes it (#468):
        /// fetched, it was written by the installer the update builds, which has no rig to ask; not carried, it was
        /// counted among the dashboards said to follow the plugin.
        /// </summary>
        [Fact]
        public void A_folder_outside_the_rig_is_neither_fetched_nor_counted()
        {
            var leftover = new PackageStatus { FolderName = "OpenDash Companion", InstalledVersion = "0.1.0", OutsideRig = true };

            var carried = UpdatePlan.For(new[] { Installed("OpenDash 850x480"), leftover }, ReleaseWith("OpenDash 850x480", "OpenDash Companion"));
            Assert.Equal(new[] { "OpenDash 850x480" }, carried.Items.Select(i => i.FolderName));
            Assert.Empty(carried.NotCarried);

            var notCarried = UpdatePlan.For(new[] { Installed("OpenDash 850x480"), leftover }, ReleaseWith());
            Assert.True(notCarried.IsEmpty);
            Assert.Equal(new[] { "OpenDash 850x480" }, notCarried.NotCarried);
        }

        [Fact]
        public void A_package_that_is_not_installed_is_not_an_update()
        {
            var plan = UpdatePlan.For(new[] { new PackageStatus { FolderName = "OpenDash", InstalledVersion = null } }, ReleaseWith("OpenDash"));
            Assert.True(plan.IsEmpty);
            Assert.Empty(plan.NotCarried);
        }

        [Fact]
        public void Nothing_to_go_on_is_an_empty_plan_rather_than_a_throw()
        {
            Assert.True(UpdatePlan.For(null, ReleaseWith("OpenDash")).IsEmpty);
            Assert.True(UpdatePlan.For(new[] { Installed("OpenDash") }, null).IsEmpty);
        }

        [Fact]
        public void The_asset_is_found_by_the_name_GitHub_published()
        {
            var plan = UpdatePlan.For(new[] { Installed("OpenDash Pit wall portrait") }, ReleaseWith("OpenDash Pit wall portrait"));
            Assert.Equal("OpenDash.Pit.wall.portrait.simhubdash", plan.Items.Single().Asset.Name);
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
            var package = SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray();
            var source = new DownloadedPackageSource().Add("OpenDash.simhubdash", package);

            Assert.Equal(new[] { "OpenDash.simhubdash" }, source.Names);
            for (var i = 0; i < 3; i++)
            {
                using (var stream = source.Open("OpenDash.simhubdash"))
                {
                    Assert.Equal("0.2.0", PackageExtractor.ReadPackageVersion(stream, out var folder));
                    Assert.Equal("OpenDash", folder);
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
                var source = new DownloadedPackageSource().Add("OpenDash.simhubdash", SyntheticPackage.Zip("OpenDash", "0.2.0").ToArray());
                var installer = new DashboardInstaller(root, null, source, new MemoryFolderRecord());
                installer.EnsureInstalled(false);

                Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
                Assert.True(installer.Packages.Single().Extracted);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        // Carrying a yes across the restart (#438)

        [Fact]
        public void A_yes_is_spent_by_the_plugin_it_was_given_for_and_by_no_other()
        {
            Assert.True(EditedConsent.AppliesNow("0.3.0", "0.3.0"));
            Assert.True(EditedConsent.AppliesNow("v0.3.0", "0.3.0"));
            // The old plugin, started again before the swap happened, must not spend it on its own dashboards.
            Assert.False(EditedConsent.AppliesNow("0.3.0", "0.3.0-rc.7"));
            // Nor may a later version inherit it.
            Assert.False(EditedConsent.AppliesNow("0.3.0", "0.3.1"));
            Assert.False(EditedConsent.AppliesNow(null, "0.3.0"));
            Assert.False(EditedConsent.AppliesNow("0.3.0", null));
        }

        [Fact]
        public void A_yes_is_kept_only_while_the_swap_that_would_bring_its_version_is_waiting()
        {
            // Spent.
            Assert.True(EditedConsent.Forget("0.3.0", "0.3.0", swapPending: false));
            // The old plugin, the swap still to come: kept for the start after it.
            Assert.False(EditedConsent.Forget("0.3.0", "0.3.0-rc.7", swapPending: true));
            // The old plugin, and nothing waiting: the swap did not happen and will not, so the answer is stale.
            Assert.True(EditedConsent.Forget("0.3.0", "0.3.0-rc.7", swapPending: false));
            // Nothing to forget.
            Assert.False(EditedConsent.Forget(null, "0.3.0", swapPending: false));
        }
    }
}
