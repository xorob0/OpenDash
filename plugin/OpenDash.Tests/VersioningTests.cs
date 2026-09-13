// VersioningTests.cs: VersionCompare, the sidecar parser and the install decision.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class VersioningTests
    {
        [Theory]
        [InlineData("0.1.0", "0.1.0", 0)]
        [InlineData("0.2.0", "0.1.9", 1)]
        [InlineData("0.1.9", "0.2.0", -1)]
        [InlineData("1.0", "1.0.0", 0)]
        [InlineData("1.0.1", "1.0", 1)]
        [InlineData("0.10.0", "0.9.0", 1)]
        [InlineData("v0.1.0", "0.1.0", 0)]
        [InlineData("0.1.0rc1", "0.1.0", 0)]
        [InlineData(null, "0.0.1", -1)]
        [InlineData("", "0.0.1", -1)]
        [InlineData(null, "0.0.0", -1)]
        [InlineData(null, null, 0)]
        [InlineData("", null, 0)]
        [InlineData("garbage", "0.0.0", 0)]
        public void VersionCompare_orders_numeric_segments(string a, string b, int expectedSign)
        {
            Assert.Equal(expectedSign, System.Math.Sign(Versioning.VersionCompare(a, b)));
        }

        [Theory]
        [InlineData("0.1.0-beta", "0.1.0", -1)]
        [InlineData("0.2.0-rc1", "0.2.0", -1)]
        [InlineData("0.2.0", "0.2.0-rc1", 1)]
        [InlineData("0.2.0-rc1", "0.2.0-rc1", 0)]
        [InlineData("0.2.0-rc1", "0.2.0-rc2", -1)]
        [InlineData("0.2.0-beta", "0.2.0-rc1", -1)]
        [InlineData("0.2.0-alpha", "0.2.0-beta", -1)]
        [InlineData("0.2.0-rc.2", "0.2.0-rc.10", -1)]
        [InlineData("0.2.0-rc2", "0.2.0-rc10", -1)]
        [InlineData("0.2.0-rc9", "0.2.0-rc10", -1)]
        [InlineData("0.2.0-rc10", "0.2.0-rc10", 0)]
        [InlineData("0.2.0-rc.1", "0.2.0-rc.alpha", -1)]
        [InlineData("0.2.0-rc", "0.2.0-rc.1", -1)]
        [InlineData("0.2.0-rc1", "0.1.9", 1)]
        [InlineData("0.2.0-rc1", "0.2.1-rc1", -1)]
        [InlineData("v0.2.0-rc1", "0.2.0-rc1", 0)]
        [InlineData("0.2.0-", "0.2.0", 0)]
        [InlineData(null, "0.0.0-rc1", -1)]
        public void VersionCompare_sorts_pre_releases_below_the_release(string a, string b, int expectedSign)
        {
            Assert.Equal(expectedSign, System.Math.Sign(Versioning.VersionCompare(a, b)));
        }

        [Theory]
        [InlineData("0.1.0+build.7", "0.1.0", 0)]
        [InlineData("0.1.0+build.7", "0.1.0+build.8", 0)]
        [InlineData("0.2.0-rc1+build.7", "0.2.0-rc1", 0)]
        [InlineData("0.2.0-rc1+build.7", "0.2.0", -1)]
        [InlineData("0.2.0+build.7", "0.2.0-rc1", 1)]
        public void VersionCompare_ignores_build_metadata(string a, string b, int expectedSign)
        {
            Assert.Equal(expectedSign, System.Math.Sign(Versioning.VersionCompare(a, b)));
        }

        [Theory]
        [InlineData("0.3.1", "0.3.0")]
        [InlineData("0.2.0", "0.2.0-rc1")]
        [InlineData("0.2.0-rc2", "0.2.0-rc1")]
        [InlineData("0.2.0-rc.10", "0.2.0-rc.2")]
        [InlineData("0.2.0-rc10", "0.2.0-rc2")]
        [InlineData("0.0.1", null)]
        public void VersionCompare_is_antisymmetric(string newer, string older)
        {
            Assert.True(Versioning.VersionCompare(newer, older) > 0);
            Assert.True(Versioning.VersionCompare(older, newer) < 0);
        }

        [Theory]
        [InlineData("{\"SimHubVersion\":\"9.12.6\",\"DashboardVersion\":\"0.1.0\"}", "0.1.0")]
        [InlineData("{\n  \"Title\": \"openDash\",\n  \"DashboardVersion\" : \"0.4.0\" ,\n  \"Width\": 1920\n}", "0.4.0")]
        [InlineData("{\"DashboardVersion\":\"\"}", null)]
        [InlineData("{\"Title\":\"openDash\"}", null)]
        [InlineData("{ this is not json", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseDashboardVersion_reads_the_sidecar(string json, string expected)
        {
            Assert.Equal(expected, Versioning.ParseDashboardVersion(json));
        }

        [Theory]
        [InlineData(null, "0.1.0", InstallStatus.NotInstalled)]
        [InlineData("0.1.0", "0.1.0", InstallStatus.UpToDate)]
        [InlineData("0.1.0", "0.2.0", InstallStatus.UpdateAvailable)]
        [InlineData("0.2.0", "0.1.0", InstallStatus.UpToDate)]
        [InlineData("0.0.0", "0.1.0", InstallStatus.UpdateAvailable)]
        [InlineData("0.2.0-rc1", "0.2.0", InstallStatus.UpdateAvailable)]
        [InlineData("0.2.0", "0.2.0-rc1", InstallStatus.UpToDate)]
        [InlineData("0.1.0", null, InstallStatus.UpToDate)]
        [InlineData(null, null, InstallStatus.NotInstalled)]
        [InlineData(Versioning.UnknownVersion, "0.1.0", InstallStatus.UpdateAvailable)]
        [InlineData(Versioning.UnknownVersion, "0.0.0", InstallStatus.UpdateAvailable)]
        [InlineData(Versioning.UnknownVersion, "0.0.0-rc1", InstallStatus.UpdateAvailable)]
        [InlineData(Versioning.UnknownVersion, null, InstallStatus.UpToDate)]
        public void Decide_compares_installed_with_embedded(string installed, string embedded, InstallStatus expected)
        {
            Assert.Equal(expected, Versioning.Decide(installed, embedded));
        }

        [Fact]
        public void NeedsInstall_for_missing_or_older_only()
        {
            Assert.True(Versioning.NeedsInstall(InstallStatus.NotInstalled));
            Assert.True(Versioning.NeedsInstall(InstallStatus.UpdateAvailable));
            Assert.False(Versioning.NeedsInstall(InstallStatus.UpToDate));
            Assert.False(Versioning.NeedsInstall(InstallStatus.Failed));
        }
    }
}
