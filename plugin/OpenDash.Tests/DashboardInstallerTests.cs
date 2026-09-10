// DashboardInstallerTests.cs: the pure part of the installer, DashboardInstaller.InstalledVersionFrom, and what
// Versioning.Decide makes of it: an absent folder is not installed, a folder without a usable sidecar is reinstalled.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class DashboardInstallerTests
    {
        private const string Sidecar = "{\"Title\":\"openDash\",\"DashboardVersion\":\"0.1.0\"}";

        [Theory]
        [InlineData(null)]
        [InlineData(Sidecar)]
        public void Folder_absent_is_not_installed_whatever_the_sidecar_says(string sidecar)
        {
            Assert.Null(DashboardInstaller.InstalledVersionFrom(false, sidecar));
            Assert.Equal(InstallStatus.NotInstalled, Versioning.Decide(DashboardInstaller.InstalledVersionFrom(false, sidecar), "0.1.0"));
        }

        [Fact]
        public void Sidecar_with_a_version_is_the_installed_version()
        {
            Assert.Equal("0.1.0", DashboardInstaller.InstalledVersionFrom(true, Sidecar));
            Assert.Equal(InstallStatus.UpToDate, Versioning.Decide(DashboardInstaller.InstalledVersionFrom(true, Sidecar), "0.1.0"));
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(DashboardInstaller.InstalledVersionFrom(true, Sidecar), "0.2.0"));
        }

        [Theory]
        [InlineData(null)] // sidecar file missing
        [InlineData("")]
        [InlineData("{\"Title\":\"openDash\",\"Width\":1920}")] // no DashboardVersion
        [InlineData("{\"DashboardVersion\":\"\"}")]
        [InlineData("{ this is not json")]
        [InlineData("{\"DashboardVersion\": 0.1.0}")] // unquoted: not the shape the generator writes
        [InlineData("\0ÿ binary junk")]
        public void Sidecar_without_a_usable_version_is_older_than_any_embedded_version(string sidecar)
        {
            var installed = DashboardInstaller.InstalledVersionFrom(true, sidecar);
            Assert.Equal(Versioning.UnknownVersion, installed);
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(installed, "0.1.0"));
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(installed, "0.0.0"));
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(installed, "0.0.0-rc1"));
            Assert.True(Versioning.NeedsInstall(Versioning.Decide(installed, "0.1.0")));
        }

        [Fact]
        public void Unknown_installed_version_with_nothing_embedded_is_left_alone()
        {
            var installed = DashboardInstaller.InstalledVersionFrom(true, null);
            Assert.Equal(InstallStatus.UpToDate, Versioning.Decide(installed, null));
        }
    }
}
