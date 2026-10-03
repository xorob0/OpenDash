// Installation.cs: the small types the installer and the panel share, and the status labels. Pure: compiled into the tests.
namespace OpenDashPlugin
{
    public enum InstallStatus
    {
        NotInstalled,
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    public static class InstallStatusExtensions
    {
        /// <summary>The status as the panel and the log show it: "Up to date", "Update available", "Install failed", "Not installed".</summary>
        public static string Label(this InstallStatus status)
        {
            switch (status)
            {
                case InstallStatus.UpToDate: return "Up to date";
                case InstallStatus.UpdateAvailable: return "Update available";
                case InstallStatus.Failed: return "Install failed";
                default: return "Not installed";
            }
        }
    }

    /// <summary>The installer logs through this so that the IO core does not depend on SimHub.Logging.</summary>
    public interface IInstallLog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public sealed class NullInstallLog : IInstallLog
    {
        public static readonly NullInstallLog Instance = new NullInstallLog();
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    public sealed class InstallResult
    {
        /// <summary>The folder under DashTemplates, e.g. "OpenDash".</summary>
        public string FolderName { get; set; }

        /// <summary>DashboardVersion of the package that was installed, null when the sidecar has none.</summary>
        public string Version { get; set; }

        public int FontsCopied { get; set; }

        /// <summary>
        /// Why the package's fonts could not be copied into DashFonts, null when they were or there were none to copy.
        /// </summary>
        /// <remarks>
        /// The folder is in place either way, which is why this is reported here rather than thrown (#593): the fonts
        /// are copied after the new folder has replaced the old one, and an exception out of the install skipped the
        /// caller's record of the folder it had just written, so the next run read that folder as somebody's edit.
        /// </remarks>
        public string FontsError { get; set; }

        /// <summary>Path of the zip made from the previous folder, null when there was none.</summary>
        public string BackupPath { get; set; }
    }
}
