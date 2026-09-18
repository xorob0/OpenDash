// PluginUpdate.cs: updating the plugin itself, which it cannot do while it is running.
//
// The Update button replaced the dashboards and left OpenDash.dll exactly as it was, so a driver who
// pressed it ended up with packages from the new release and a plugin from the old one. That is not a
// cosmetic mismatch: a package's bindings are literals and the plugin is what attaches the properties
// behind them, so the half that moved reads names the half that did not has never heard of, and the
// symptom is a field that draws its fallback for ever with nothing in any log. It is the same shape of
// failure as the blank zones, arrived at from the other direction.
//
// **A loaded assembly cannot overwrite itself.** Windows maps OpenDash.dll without FILE_SHARE_DELETE
// while SimHub holds it, so neither a copy nor a rename over it succeeds, and nothing the plugin does
// from inside its own process can change that. What it can do is put the new file beside the old one
// and arrange for the swap to happen in the gap after SimHub exits: a detached `cmd` waits for the
// process to go, moves the staged file into place and deletes itself. Everything it does is reversible
// -- the file it replaces is copied aside first -- and a swap that cannot happen leaves the plugin
// exactly as it is rather than half replaced.
//
// Pure but for the file system: no SimHub types, so OpenDash.Tests compiles it and pins the staging,
// the script and the decision.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>What became of a staging attempt.</summary>
    public sealed class PluginStageResult
    {
        public bool Ok { get { return Error == null; } }

        /// <summary>Where the new assembly is waiting, or null when nothing was staged.</summary>
        public string Path { get; set; }

        /// <summary>Null when it worked; the reason otherwise, in the words the panel shows.</summary>
        public string Error { get; set; }
    }

    public static class PluginUpdate
    {
        /// <summary>The asset a release publishes the plugin as. GitHub turns a space into a period and
        /// this name has none, so what it publishes is what the build writes.</summary>
        public const string AssetName = "OpenDash-plugin.zip";

        /// <summary>The one entry of that zip this cares about. The others are INSTALL.md and the font
        /// licence, which a running SimHub has no use for.</summary>
        public const string DllName = "OpenDash.dll";

        /// <summary>Beside the flag box profile, in the plugin's own folder rather than in SimHub's:
        /// PluginsData is where SimHub keeps files it owns.</summary>
        public const string StagedName = DllName + ".pending";

        public const string BackupName = DllName + ".replaced";

        public const string ScriptName = "opendash-swap-plugin.cmd";

        /// <summary>How long the script waits for SimHub to go before giving up, in seconds. Long enough
        /// for a slow shutdown, short enough that a machine which never closes SimHub is not left with a
        /// process spinning for the rest of the session.</summary>
        public const int WaitSeconds = 120;

        public static string StagedPath(string simHubRoot)
        {
            return Path.Combine(FlagBoxProfile.FolderPath(simHubRoot), StagedName);
        }

        public static string ScriptPath(string simHubRoot)
        {
            return Path.Combine(FlagBoxProfile.FolderPath(simHubRoot), ScriptName);
        }

        /// <summary>The assembly SimHub loaded, which is the file the swap replaces.</summary>
        public static string InstalledPath(string simHubRoot)
        {
            return Path.Combine(simHubRoot ?? string.Empty, DllName);
        }

        /// <summary>Whether a new assembly is waiting to be put in place.</summary>
        public static bool Pending(string simHubRoot)
        {
            try
            {
                return File.Exists(StagedPath(simHubRoot));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes the assembly out of a downloaded plugin zip, beside the one in use.
        /// </summary>
        /// <remarks>
        /// The bytes are already checked against the digest GitHub published by the caller, so what is
        /// left to be wrong here is the shape of the archive. An entry that is missing or empty is a
        /// refusal rather than a zero-byte DLL staged for a swap that would leave SimHub with no plugin
        /// at all.
        /// </remarks>
        public static PluginStageResult Stage(byte[] zipBytes, string simHubRoot, IInstallLog log = null)
        {
            log = log ?? NullInstallLog.Instance;
            var result = new PluginStageResult();
            if (zipBytes == null || zipBytes.Length == 0)
            {
                result.Error = "the plugin download was empty";
                return result;
            }
            var path = StagedPath(simHubRoot);
            try
            {
                using (var stream = new MemoryStream(zipBytes, false))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = null;
                    foreach (var candidate in zip.Entries)
                    {
                        if (string.Equals(Path.GetFileName(candidate.FullName), DllName, StringComparison.OrdinalIgnoreCase))
                        {
                            entry = candidate;
                            break;
                        }
                    }
                    if (entry == null || entry.Length == 0)
                    {
                        result.Error = "the plugin download carries no " + DllName;
                        return result;
                    }
                    Directory.CreateDirectory(FlagBoxProfile.FolderPath(simHubRoot));
                    // Written whole and then moved into place, so that a download interrupted part way
                    // through cannot leave a half assembly staged for the swap to believe in.
                    var partial = path + ".part";
                    using (var source = entry.Open())
                    using (var target = File.Create(partial))
                    {
                        source.CopyTo(target);
                    }
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(partial, path);
                }
                result.Path = path;
                log.Info("Staged the new plugin at " + path + "; it is put in place when SimHub closes.");
                return result;
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                log.Error("Staging the new plugin failed: " + e);
                return result;
            }
        }

        /// <summary>
        /// The script that does the swap, which is the only part that runs while SimHub is not.
        /// </summary>
        /// <remarks>
        /// `ping` rather than `timeout`, which needs a console it will not have. The old assembly is
        /// copied aside before it is replaced and the copy is left there, so a release that turns out to
        /// be worse than the one it replaced is one rename away from being undone. The script deletes
        /// itself last, so a swap that never happened leaves both the staged assembly and the script
        /// that would have applied it, and the next shutdown tries again.
        /// </remarks>
        public static string SwapScript(string simHubRoot)
        {
            var installed = InstalledPath(simHubRoot);
            var staged = StagedPath(simHubRoot);
            var backup = Path.Combine(FlagBoxProfile.FolderPath(simHubRoot), BackupName);
            var text = new StringBuilder();
            text.AppendLine("@echo off");
            text.AppendLine("rem Written by openDash to put a downloaded plugin in place once SimHub has closed.");
            text.AppendLine("rem It replaces " + DllName + " and keeps the one it replaced as " + BackupName + ".");
            text.AppendLine("setlocal");
            text.AppendLine("set /a waited=0");
            text.AppendLine(":wait");
            text.AppendLine("tasklist /FI \"IMAGENAME eq SimHubWPF.exe\" | find /I \"SimHubWPF.exe\" >nul 2>&1 || goto swap");
            text.AppendLine("ping -n 2 127.0.0.1 >nul 2>&1");
            text.AppendLine("set /a waited+=1");
            text.AppendLine("if %waited% GEQ " + WaitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " goto giveup");
            text.AppendLine("goto wait");
            text.AppendLine(":swap");
            text.AppendLine("copy /y \"" + installed + "\" \"" + backup + "\" >nul 2>&1");
            text.AppendLine("move /y \"" + staged + "\" \"" + installed + "\" >nul 2>&1 || goto giveup");
            text.AppendLine("del \"%~f0\" >nul 2>&1");
            text.AppendLine("exit /b 0");
            text.AppendLine(":giveup");
            text.AppendLine("rem SimHub is still running, or the file could not be replaced. Both leave the");
            text.AppendLine("rem plugin exactly as it is, and the next shutdown tries again.");
            text.AppendLine("exit /b 1");
            return text.ToString();
        }

        /// <summary>
        /// Writes the script and starts it, detached, so that it outlives the process that asked for it.
        /// </summary>
        /// <remarks>
        /// Called from `End`, which is the last thing SimHub gives a plugin, and does nothing at all
        /// unless an assembly is actually staged. `UseShellExecute` false with no window, so a driver
        /// closing SimHub does not get a console flashing at them.
        /// </remarks>
        public static bool Launch(string simHubRoot, IInstallLog log = null)
        {
            log = log ?? NullInstallLog.Instance;
            if (!Pending(simHubRoot)) return false;
            var script = ScriptPath(simHubRoot);
            try
            {
                File.WriteAllText(script, SwapScript(simHubRoot), new UTF8Encoding(false));
                var start = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + script + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = FlagBoxProfile.FolderPath(simHubRoot),
                };
                Process.Start(start);
                log.Info("Started the plugin swap; it takes effect as soon as SimHub has closed.");
                return true;
            }
            catch (Exception e)
            {
                log.Error("The plugin swap could not be started, so the plugin is unchanged: " + e);
                return false;
            }
        }
    }
}
