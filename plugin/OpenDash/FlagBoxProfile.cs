// FlagBoxProfile.cs: the embedded .ledsprofile, extracted to a folder and left there.
//
// This is deliberately not an installer. ADR 0013 decided the flag box profile is the one artefact
// the plugin does not install: on the Arduino path every matrix profile a user owns lives inside
// PluginsData/Common/ArduinoRGBMatrixSettings.json, which SimHub's RGBMatrixDriver reads when it is
// constructed and rewrites whenever anything changes, so merging into it is a write we cannot
// sequence against and would lose somebody's other profiles. Painting hardware the user owns
// because they installed a dashboard is also a larger liberty than installing a dashboard.
//
// So: write the file where the user can find it, tell them where, and stop. No SimHub or WPF types
// here, so this compiles into the tests.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>What the plugin found and what it did with it, for the panel and the log.</summary>
    public enum FlagBoxStatus
    {
        /// <summary>No .ledsprofile is embedded in this build.</summary>
        NotEmbedded,
        /// <summary>The file on disk is byte for byte the embedded one.</summary>
        UpToDate,
        /// <summary>The file was written or refreshed.</summary>
        Extracted,
        /// <summary>Extraction failed; the message says why.</summary>
        Failed,
    }

    public sealed class FlagBoxResult
    {
        public FlagBoxStatus Status { get; set; }

        /// <summary>Absolute path of the profile on disk, null when nothing was embedded.</summary>
        public string Path { get; set; }

        /// <summary>The profile's Name field, for the panel. Null when it could not be read.</summary>
        public string ProfileName { get; set; }

        public string Message { get; set; }
    }

    /// <summary>Extracts the embedded flag box profile beside SimHub, and never further than that.</summary>
    public static class FlagBoxProfile
    {
        public const string ProfileExtension = ".ledsprofile";

        /// <summary>Where the profile is left: SimHub/OpenDash/. Its own folder rather than PluginsData,
        /// because PluginsData is where SimHub keeps files it owns and this one is for the user to pick up.</summary>
        public const string FolderName = "OpenDash";

        /// <summary>SimHub's settings file for matrix profiles. Named here only so the panel can say what
        /// the plugin does not touch; nothing in this class opens it.</summary>
        public const string SimHubMatrixSettings = @"PluginsData\Common\ArduinoRGBMatrixSettings.json";

        public static string FolderPath(string simHubRoot)
        {
            return Path.Combine(simHubRoot, FolderName);
        }

        /// <summary>The embedded profile resources, in name order. Empty when the build embedded none.</summary>
        public static IReadOnlyList<string> ResourceNames(Assembly assembly)
        {
            return AssemblyPackageSource.ResourceNames(assembly, ProfileExtension);
        }

        /// <summary>The file name a resource is written under: everything after the last ".Resources."
        /// part. MSBuild names an embedded resource "&lt;RootNamespace&gt;.&lt;folder&gt;.&lt;file name&gt;", turning only the
        /// folder part into an identifier, so the file name keeps its spaces and "openDash Flag box.ledsprofile"
        /// survives the round trip. DashboardInstaller.Core.cs relies on the same naming.</summary>
        public static string FileNameOf(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;
            const string marker = ".Resources.";
            int at = resourceName.LastIndexOf(marker, StringComparison.Ordinal);
            return at < 0 ? resourceName : resourceName.Substring(at + marker.Length);
        }

        /// <summary>The profile's Name, read without a JSON parser: the file is generated, the field is a
        /// plain string, and taking a dependency for one value would be worse than this.</summary>
        public static string ProfileNameOf(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            const string key = "\"Name\":";
            int at = json.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = json.IndexOf('"', at + key.Length);
            if (open < 0) return null;
            int close = json.IndexOf('"', open + 1);
            return close < 0 ? null : json.Substring(open + 1, close - open - 1);
        }

        /// <summary>Writes the embedded profile into SimHub/OpenDash/ when it is missing or has changed.
        ///
        /// A file the user has edited is left alone: the check is against what was last written, which is
        /// what the embedded copy is compared with. Identical content is not rewritten, so the file's
        /// timestamp means what it says.</summary>
        public static FlagBoxResult Extract(string simHubRoot, Assembly assembly, IInstallLog log = null)
        {
            log = log ?? NullInstallLog.Instance;
            var names = ResourceNames(assembly);
            if (names.Count == 0)
            {
                log.Warn("No " + ProfileExtension + " is embedded in this build; the flag box profile is not available (see plugin/OpenDash/Resources/README.md).");
                return new FlagBoxResult { Status = FlagBoxStatus.NotEmbedded, Message = "No profile embedded" };
            }

            string resource = names[0];
            string fileName = FileNameOf(resource);
            string folder = FolderPath(simHubRoot);
            string path = Path.Combine(folder, fileName);
            try
            {
                string embedded;
                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new InvalidOperationException("resource " + resource + " could not be opened");
                    using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        embedded = reader.ReadToEnd();
                    }
                }

                string profileName = ProfileNameOf(embedded);
                if (File.Exists(path) && string.Equals(File.ReadAllText(path), embedded, StringComparison.Ordinal))
                {
                    return new FlagBoxResult { Status = FlagBoxStatus.UpToDate, Path = path, ProfileName = profileName, Message = "Up to date" };
                }

                Directory.CreateDirectory(folder);
                File.WriteAllText(path, embedded, new UTF8Encoding(false));
                log.Info("Wrote the flag box profile to " + path + ". Import it in SimHub under the matrix device; OpenDash does not install it.");
                return new FlagBoxResult { Status = FlagBoxStatus.Extracted, Path = path, ProfileName = profileName, Message = "Written" };
            }
            catch (Exception e)
            {
                log.Error("Could not write the flag box profile to " + path + ": " + e.Message);
                return new FlagBoxResult { Status = FlagBoxStatus.Failed, Path = path, Message = e.Message };
            }
        }

        /// <summary>What the lights page says. The wording carries the manual step, because it is the whole
        /// difference between this and a package and a user who is not told will wait for something that
        /// is never going to happen.</summary>
        public static string Summary(FlagBoxResult result)
        {
            if (result == null) return "Flag box: not checked";
            switch (result.Status)
            {
                case FlagBoxStatus.NotEmbedded:
                    return "Flag box: no profile in this build";
                case FlagBoxStatus.Failed:
                    return "Flag box: could not write the profile (" + result.Message + ")";
                default:
                    return "Flag box: " + (result.ProfileName ?? "profile") + " is at " + result.Path
                        + ". Import it in SimHub's matrix device settings; OpenDash does not install it.";
            }
        }
    }
}
