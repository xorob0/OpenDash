// DashboardInstaller.Installed.cs: the pure part of the installer, compiled into the tests: how the files under
// DashTemplates/<folder> become the installed version the panel shows and Versioning.Decide compares. No IO, no SimHub;
// DashboardInstaller.cs reads the files and adds the SimHub context.
namespace OpenDashPlugin
{
    public sealed partial class DashboardInstaller
    {
        /// <summary>The installed version of one dashboard folder, from what its files say. Null when
        /// <folder>/<folder>.djson is absent (not installed). The sidecar's DashboardVersion when it has one.
        /// Versioning.UnknownVersion when the sidecar is missing (null text), malformed or without a DashboardVersion,
        /// which Decide ranks below every embedded version so that the package is reinstalled. Never throws.</summary>
        public static string InstalledVersionFrom(bool dashExists, string sidecarText)
        {
            if (!dashExists) return null;
            return Versioning.ParseDashboardVersion(sidecarText) ?? Versioning.UnknownVersion;
        }
    }
}
