// DashboardInstaller.cs: the SimHub side of the installer. The packages are the .simhubdash resources embedded in this
// assembly, the root is SimHub's install directory, the log is SimHub's, and DashFonts is reloaded through SimHub after
// an install. Everything else (reading, deciding, installing each package, the worst status, the summary) lives in
// DashboardInstaller.Core.cs and runs in the tests against synthetic packages.
using System;
using System.Reflection;

namespace OpenDashPlugin
{
    public sealed partial class DashboardInstaller
    {
        /// <summary>SimHub's working directory is its install directory; DashTemplates and DashFonts are relative to it.</summary>
        public DashboardInstaller(IFolderRecord record = null)
            : this(AppDomain.CurrentDomain.BaseDirectory, new SimHubInstallLog(), new AssemblyPackageSource(typeof(DashboardInstaller).Assembly), record)
        {
        }

        /// <summary>SimHub reloads DashFonts through FontHelper.RefreshFonts(). The class is internal in SimHub 9.12.6,
        /// so it is reached by reflection; when it is missing the fonts load at the next SimHub start.</summary>
        partial void RefreshSimHubFonts()
        {
            try
            {
                var assembly = typeof(SimHub.Plugins.PluginManager).Assembly;
                var type = assembly.GetType("SimHub.Plugins.OutputPlugins.GraphicalDash.FontHelper", false);
                var method = type?.GetMethod("RefreshFonts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    log.Warn("FontHelper.RefreshFonts was not found; the dashboard fonts load at the next SimHub start.");
                    return;
                }
                method.Invoke(null, null);
                log.Info("SimHub fonts refreshed.");
            }
            catch (Exception ex)
            {
                log.Warn("Refreshing SimHub fonts failed; they load at the next SimHub start: " + ex.Message);
            }
        }
    }
}
