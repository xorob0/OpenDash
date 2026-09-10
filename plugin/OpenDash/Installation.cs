// Installation.cs: the small types the installer and the panel share. Pure: compiled into the tests.
namespace OpenDashPlugin
{
    public enum InstallStatus
    {
        NotInstalled,
        UpToDate,
        UpdateAvailable,
        Failed,
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
        /// <summary>The folder under DashTemplates, e.g. "openDash".</summary>
        public string FolderName { get; set; }

        /// <summary>DashboardVersion of the package that was installed, null when the sidecar has none.</summary>
        public string Version { get; set; }

        public int FontsCopied { get; set; }

        /// <summary>Path of the zip made from the previous folder, null when there was none.</summary>
        public string BackupPath { get; set; }
    }
}
