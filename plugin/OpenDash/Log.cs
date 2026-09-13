// Log.cs: SimHub's log (SimHub.Logging.Current, a log4net ILog) behind a prefix and a try/catch, so that a
// logging failure can never take the plugin down. Also the IInstallLog adapter the installer uses.
using System;

namespace OpenDashPlugin
{
    internal static class Log
    {
        public const string Prefix = "[OpenDash] ";

        public static void Info(string message)
        {
            try { SimHub.Logging.Current.Info(Prefix + message); } catch { }
        }

        public static void Warn(string message)
        {
            try { SimHub.Logging.Current.Warn(Prefix + message); } catch { }
        }

        public static void Error(string message)
        {
            try { SimHub.Logging.Current.Error(Prefix + message); } catch { }
        }

        public static void Error(string message, Exception ex)
        {
            Error(message + ": " + ex);
        }
    }

    internal sealed class SimHubInstallLog : IInstallLog
    {
        public void Info(string message) => Log.Info(message);
        public void Warn(string message) => Log.Warn(message);
        public void Error(string message) => Log.Error(message);
    }
}
