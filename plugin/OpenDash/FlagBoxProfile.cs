// FlagBoxProfile.cs: the embedded .ledsprofile, written out to a folder.
//
// This is not the installer -- FlagBoxInstaller.cs is, and it hands the profile to SimHub's own API
// rather than writing SimHub's settings file. What this does is keep a copy on disk, which is the
// fallback when SimHub's matrix settings cannot be reached, the thing a user copies to a second
// machine, and the thing somebody opens to read what openDash is asking their hardware to do.
//
// It still never writes PluginsData/Common/ArduinoRGBMatrixSettings.json: that file belongs to
// SimHub's RGBMatrixDriver, which rewrites it whenever anything changes. See the amendment to
// ADR 0013 for why the install goes through the object model instead.
//
// WHICH of the embedded profiles this is about is decided by name, and that is the whole of the
// discriminator. SimHub gives both of its lighting families the same ".ledsprofile" extension: the
// RGB LED strips the build now emits nineteen of, and the 8x8 matrix the flag box is. So the
// extension says nothing about which driver a file belongs to, and "the first .ledsprofile in the
// assembly" -- which is what this used to take -- became a ten-LED RPM strip the moment the strips
// were embedded, and would have been handed to RGBMatrixDriver.Settings.AddProfile as the flag box.
// The file name is the identity: `FLAG_BOX_PROFILE_NAME` in packages/dash/src/leds/profile.ts is the
// profile's name, its file stem and this constant, and FlagBoxProfileTests embeds a decoy that sorts
// ahead of it so that going back to sort order fails rather than shipping.
//
// The strips are now installed too, so the guarantee runs both ways and the selection is shared:
// `SelectResource` takes the file name it is looking for, a shape asks for `StripFileName(id)` and the
// flag box asks for `FileName`. Only an exact spelling wins, and the two spellings cannot collide, so
// neither driver can be handed the other's profile.
//
// No SimHub or WPF types here, so this compiles into the tests.
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

        /// <summary>The profile itself, so the panel can install it without reading the file back.</summary>
        public string Json { get; set; }

        public string Message { get; set; }
    }

    /// <summary>Extracts the embedded flag box profile beside SimHub, and never further than that.</summary>
    public static class FlagBoxProfile
    {
        /// <summary>The extension SimHub gives BOTH of its lighting families, the LED strips and the
        /// matrix. It says a file is a profile; it does not say which driver the profile is for, so it
        /// is never on its own enough to pick the flag box out. <see cref="FileName"/> is.</summary>
        public const string ProfileExtension = ".ledsprofile";

        /// <summary>The profile's Name, its file stem and the folder-free half of its resource name.
        /// One contract in two halves with FLAG_BOX_PROFILE_NAME in packages/dash/src/leds/profile.ts,
        /// the way the version marker is one with flagBoxVersion(); a rename on either side has to move
        /// both, and FlagBoxInstallPlanTests reads the built file to see that it did.</summary>
        public const string ProfileName = "openDash Flag box";

        /// <summary>The file the build writes and the plugin embeds, and the only one of the embedded
        /// profiles that is the flag box.</summary>
        public const string FileName = ProfileName + ProfileExtension;

        /// <summary>The prefix every profile file name carries, the flag box's and every strip's alike.
        /// It is what makes "openDash 4-14-4" a shape id of ours rather than a file somebody dropped in.</summary>
        public const string FilePrefix = "openDash ";

        /// <summary>The file a strip or brow shape is written under. The other half of rpmStripFileName()
        /// in packages/dash/src/leds/rpmStrip.ts, which spells it `openDash ${shape.id}`; the shape ids
        /// themselves stay in STRIP_SHAPES and BROW_SHAPES and are never copied here.</summary>
        public static string StripFileName(string shapeId)
        {
            return string.IsNullOrEmpty(shapeId) ? null : FilePrefix + shapeId + ProfileExtension;
        }

        /// <summary>Where the profile is left: SimHub/OpenDash/. Its own folder rather than PluginsData,
        /// because PluginsData is where SimHub keeps files it owns and this one is for the user to pick up.</summary>
        public const string FolderName = "OpenDash";

        /// <summary>SimHub's settings file for matrix profiles. Named here only so the panel can say what
        /// openDash does not touch; nothing anywhere in the plugin opens it.</summary>
        public const string SimHubMatrixSettings = @"PluginsData\Common\ArduinoRGBMatrixSettings.json";

        public static string FolderPath(string simHubRoot)
        {
            return Path.Combine(simHubRoot, FolderName);
        }

        /// <summary>EVERY embedded profile resource, in name order: the flag box and the nineteen LED
        /// strips alike. Empty when the build embedded none. This is a census, not a choice -- it is how
        /// the panel can say how many profiles a build carries -- and nothing should read an entry of it
        /// positionally. <see cref="ResourceName"/> is how the flag box is found.</summary>
        public static IReadOnlyList<string> ResourceNames(Assembly assembly)
        {
            return AssemblyPackageSource.ResourceNames(assembly, ProfileExtension);
        }

        /// <summary>
        /// The flag box's own resource, or null when this build embeds no such file.
        ///
        /// Found by <see cref="FileName"/> rather than by position. An exact spelling wins; a
        /// case-only variant is accepted only when nothing spells it exactly, which is the trap a
        /// case-insensitive filesystem sets -- copying "openDash Flag box.ledsprofile" over an older
        /// "openDash flag box.ledsprofile" replaces the bytes on Windows and keeps the old casing, so
        /// an exact-only match would embed the profile and then fail to find it.
        /// </summary>
        public static string ResourceName(Assembly assembly)
        {
            return SelectResource(ResourceNames(assembly), FileName);
        }

        /// <summary>One embedded profile by its file name, or null when this build embeds no such file.
        /// The same selection the flag box uses, so a shape asking for its own file can never be handed
        /// the matrix profile and the matrix can never be handed a strip: the two names differ, and only
        /// an exact name wins.</summary>
        public static string ResourceName(Assembly assembly, string fileName)
        {
            return SelectResource(ResourceNames(assembly), fileName);
        }

        /// <summary>The selection itself, over the names alone, so that it can be shown to do the right
        /// thing on lists no build of ours would produce.</summary>
        internal static string SelectResource(IEnumerable<string> names, string fileName)
        {
            if (names == null || string.IsNullOrEmpty(fileName)) return null;
            string variant = null;
            foreach (var name in names)
            {
                var file = FileNameOf(name);
                if (string.Equals(file, fileName, StringComparison.Ordinal)) return name;
                if (variant == null && string.Equals(file, fileName, StringComparison.OrdinalIgnoreCase)) variant = name;
            }
            return variant;
        }

        /// <summary>Every embedded profile that is not the flag box, in name order: one per strip or brow
        /// shape the build emitted. Derived from what is embedded rather than from a table copied out of
        /// packages/dash/src/leds/strip.ts, so the plugin offers exactly the shapes its own build carries
        /// and a shape added there needs no second edit here.</summary>
        public static IReadOnlyList<string> StripResourceNames(Assembly assembly)
        {
            return ResourceNames(assembly).Where(n => ShapeIdOf(n) != null).ToList();
        }

        /// <summary>The shape id a profile resource is for -- "4-14-4", "brow-9" -- or null when the
        /// resource is the flag box or is not one of ours at all.</summary>
        public static string ShapeIdOf(string resourceName)
        {
            var file = FileNameOf(resourceName);
            if (file == null) return null;
            if (string.Equals(file, FileName, StringComparison.OrdinalIgnoreCase)) return null;
            if (!file.StartsWith(FilePrefix, StringComparison.Ordinal)) return null;
            if (!file.EndsWith(ProfileExtension, StringComparison.OrdinalIgnoreCase)) return null;
            var id = file.Substring(FilePrefix.Length, file.Length - FilePrefix.Length - ProfileExtension.Length);
            return id.Length == 0 ? null : id;
        }

        /// <summary>One embedded profile's JSON, or null when it cannot be read. The installer hands this
        /// straight to SimHub, so a profile is offered without ever having been written to disk.</summary>
        public static string ResourceText(Assembly assembly, string resourceName, IInstallLog log = null)
        {
            if (assembly == null || string.IsNullOrEmpty(resourceName)) return null;
            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                (log ?? NullInstallLog.Instance).Error("The embedded profile " + resourceName + " could not be read: " + e.Message);
                return null;
            }
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

        /// <summary>The profile's Description, read the same way as its Name.</summary>
        public static string DescriptionOf(string json)
        {
            return FieldOf(json, "Description");
        }

        /// <summary>The profile's Author, which the build stamps with "openDash".</summary>
        public static string AuthorOf(string json)
        {
            return FieldOf(json, "Author");
        }

        /// <summary>
        /// A string field of the profile's TOP-LEVEL object, read without a JSON parser.
        ///
        /// Depth matters: every container in the tree has a "Description" of its own and they appear
        /// before the profile's, so the first match in the file is a container's. This walks the text
        /// tracking brace depth and string state, and only accepts a key at depth 1. The plugin targets
        /// net48 and the test project net8.0, and they share no JSON library between them, which is why
        /// this is here rather than three lines of Newtonsoft.
        /// </summary>
        internal static string FieldOf(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int end = EndOfString(json, i);
                    if (end < 0) return null;
                    // A key at depth 1, followed by a colon, is one of the profile's own fields.
                    if (depth == 1 && Matches(json, i + 1, end, field))
                    {
                        int colon = SkipSpace(json, end + 1);
                        if (colon < json.Length && json[colon] == ':')
                        {
                            int valueStart = SkipSpace(json, colon + 1);
                            if (valueStart >= json.Length || json[valueStart] != '"') return null;
                            int valueEnd = EndOfString(json, valueStart);
                            return valueEnd < 0 ? null : Unescape(json.Substring(valueStart + 1, valueEnd - valueStart - 1));
                        }
                    }
                    i = end;
                    continue;
                }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return null;
        }

        /// <summary>Index of the closing quote of the string starting at `open`, or -1.</summary>
        private static int EndOfString(string json, int open)
        {
            for (int i = open + 1; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i;
            }
            return -1;
        }

        private static int SkipSpace(string json, int from)
        {
            int i = from;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            return i;
        }

        private static bool Matches(string json, int start, int end, string field)
        {
            return end - start == field.Length && string.CompareOrdinal(json, start, field, 0, field.Length) == 0;
        }

        /// <summary>The escapes the build can actually emit in a description.</summary>
        private static string Unescape(string raw)
        {
            if (raw.IndexOf('\\') < 0) return raw;
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] != '\\' || i + 1 >= raw.Length) { sb.Append(raw[i]); continue; }
                char next = raw[++i];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    default: sb.Append(next); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>The profile's Name. Same depth-aware read as the other two.</summary>
        public static string ProfileNameOf(string json)
        {
            return FieldOf(json, "Name");
        }

        /// <summary>Writes the embedded profile into SimHub/OpenDash/ when it is missing or has changed.
        ///
        /// A file the user has edited is left alone: the check is against what was last written, which is
        /// what the embedded copy is compared with. Identical content is not rewritten, so the file's
        /// timestamp means what it says.</summary>
        public static FlagBoxResult Extract(string simHubRoot, Assembly assembly, IInstallLog log = null)
        {
            log = log ?? NullInstallLog.Instance;
            string resource = ResourceName(assembly);
            if (resource == null)
            {
                // Two different builds, and saying "none embedded" for the second would send somebody
                // looking for a missing copy step when the profiles are all there under other names.
                var embedded = ResourceNames(assembly).Count;
                log.Warn((embedded == 0
                        ? "No " + ProfileExtension + " is embedded in this build"
                        : "None of the " + embedded + " embedded " + ProfileExtension + " resources is " + FileName)
                    + "; the flag box profile is not available (see plugin/OpenDash/Resources/README.md).");
                return new FlagBoxResult { Status = FlagBoxStatus.NotEmbedded, Message = "No profile embedded" };
            }

            string fileName = FileNameOf(resource);
            string folder = FolderPath(simHubRoot);
            string path = Path.Combine(folder, fileName);
            try
            {
                string embedded = ResourceText(assembly, resource, log);
                if (embedded == null) throw new InvalidOperationException("resource " + resource + " could not be opened");

                string profileName = ProfileNameOf(embedded);
                if (File.Exists(path) && string.Equals(File.ReadAllText(path), embedded, StringComparison.Ordinal))
                {
                    return new FlagBoxResult { Status = FlagBoxStatus.UpToDate, Path = path, ProfileName = profileName, Json = embedded, Message = "Up to date" };
                }

                Directory.CreateDirectory(folder);
                File.WriteAllText(path, embedded, new UTF8Encoding(false));
                log.Info("Wrote the flag box profile to " + path + ". Install it from the OpenDash settings page under Lights.");
                return new FlagBoxResult { Status = FlagBoxStatus.Extracted, Path = path, ProfileName = profileName, Json = embedded, Message = "Written" };
            }
            catch (Exception e)
            {
                log.Error("Could not write the flag box profile to " + path + ": " + e.Message);
                return new FlagBoxResult { Status = FlagBoxStatus.Failed, Path = path, Message = e.Message };
            }
        }

        /// <summary>
        /// Where SimHub's own profile import dialog opens: Documents\SimHub.
        ///
        /// ProfilesManager.importProfile_Click sets InitialDirectory to
        /// Path.Combine(GetFolderPath(SpecialFolder.Personal), "SimHub") (ProfilesManager.cs:208-212),
        /// so a copy put there is the file already in front of the user when the dialog opens. That
        /// only matters on the fallback path -- normally the Lights page installs the profile without
        /// a dialog at all -- which is why nothing writes here unless the user asks.
        /// </summary>
        public static string ImportFolder(string documents = null)
        {
            var root = documents ?? Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "SimHub");
        }

        /// <summary>
        /// Copies the profile to where SimHub's import dialog opens, and returns the path.
        ///
        /// Deliberately on demand: this writes into the user's Documents, and a stray file there for
        /// everybody who never needs it would be a poor trade for a case that is already the fallback.
        /// </summary>
        public static FlagBoxResult CopyForImport(FlagBoxResult extracted, string documents = null, IInstallLog log = null)
        {
            log = log ?? NullInstallLog.Instance;
            if (extracted == null || string.IsNullOrEmpty(extracted.Json))
            {
                return new FlagBoxResult { Status = FlagBoxStatus.NotEmbedded, Message = "No profile embedded" };
            }
            var folder = ImportFolder(documents);
            if (folder == null)
            {
                return new FlagBoxResult { Status = FlagBoxStatus.Failed, Message = "No Documents folder" };
            }
            var name = FileNameOf(extracted.Path == null ? null : Path.GetFileName(extracted.Path)) ?? FileName;
            var path = Path.Combine(folder, Path.GetFileName(extracted.Path ?? name));
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(path, extracted.Json, new UTF8Encoding(false));
                log.Info("Copied the flag box profile to " + path + ", where SimHub's import dialog opens.");
                return new FlagBoxResult { Status = FlagBoxStatus.Extracted, Path = path, ProfileName = extracted.ProfileName, Json = extracted.Json, Message = "Copied" };
            }
            catch (Exception e)
            {
                log.Error("Could not copy the flag box profile to " + path + ": " + e.Message);
                return new FlagBoxResult { Status = FlagBoxStatus.Failed, Path = path, Message = e.Message };
            }
        }

        /// <summary>What the log says at startup. The panel says the rest, because only the panel knows
        /// whether SimHub already holds the profile; see FlagBoxInstallPlan.Summary.</summary>
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
                        + ". Install it from the OpenDash settings page under Lights.";
            }
        }
    }
}
