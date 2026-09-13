// LedProfiles.Core.cs: which LED profiles the plugin carries, where a user can get at them, and the one rule that
// governs all of it — openDash never writes to a device the user has not asked it to. ADR 0013.
// No SimHub or WPF types here; compiled into OpenDash.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenDashPlugin
{
    /// <summary>What kind of hardware a profile is for. The user opts in per family, not per profile: somebody who
    /// owns a wheel with a strip owns one strip, and asking them about nineteen of them is asking nineteen times.</summary>
    public enum LedDeviceFamily
    {
        /// <summary>An RGB strip on a wheel or a base: side, centre, side.</summary>
        Strip,

        /// <summary>A bare run above a monitor.</summary>
        Brow,

        /// <summary>The 8x8 matrix flag box.</summary>
        FlagBox,
    }

    /// <summary>One profile the plugin carries.</summary>
    public sealed class LedProfile
    {
        public LedProfile(string resourceName, string fileName, LedDeviceFamily family)
        {
            ResourceName = resourceName;
            FileName = fileName;
            Family = family;
        }

        /// <summary>The embedded resource name, which is the handle Open takes.</summary>
        public string ResourceName { get; }

        /// <summary>The file name as it is written out, e.g. "openDash 4-14-4.ledsprofile".</summary>
        public string FileName { get; }

        public LedDeviceFamily Family { get; }

        /// <summary>What the panel calls it: the file name without its extension.</summary>
        public string DisplayName
        {
            get
            {
                var dot = FileName.LastIndexOf('.');
                return dot > 0 ? FileName.Substring(0, dot) : FileName;
            }
        }
    }

    /// <summary>Where the profiles come from. The plugin feeds the assembly's embedded resources; the tests feed a list.</summary>
    public interface ILedProfileSource
    {
        IReadOnlyList<LedProfile> Profiles { get; }

        /// <summary>A fresh readable stream of the profile; the caller disposes it.</summary>
        Stream Open(LedProfile profile);
    }

    /// <summary>The profiles embedded in an assembly: every manifest resource whose name ends in .ledsprofile.</summary>
    public sealed class AssemblyLedProfileSource : ILedProfileSource
    {
        private readonly Assembly assembly;

        public AssemblyLedProfileSource(Assembly assembly)
        {
            this.assembly = assembly;
            Profiles = AssemblyPackageSource.ResourceNames(assembly, LedProfiles.Extension)
                .Select(name => new LedProfile(name, FileNameOf(name), LedProfiles.FamilyOf(FileNameOf(name))))
                .ToList();
        }

        public IReadOnlyList<LedProfile> Profiles { get; }

        /// <summary>MSBuild names a resource "&lt;RootNamespace&gt;.Resources.&lt;file name&gt;" and keeps the file name as it
        /// is, spaces included. The file name is everything after the last "Resources." — not after the last dot, since
        /// the file name has dots of its own.</summary>
        public static string FileNameOf(string resourceName)
        {
            const string marker = ".Resources.";
            var at = resourceName.IndexOf(marker, StringComparison.Ordinal);
            return at >= 0 ? resourceName.Substring(at + marker.Length) : resourceName;
        }

        public Stream Open(LedProfile profile)
        {
            var stream = assembly.GetManifestResourceStream(profile.ResourceName);
            if (stream == null) throw new FileNotFoundException("Embedded LED profile not found: " + profile.ResourceName);
            return stream;
        }
    }

    /// <summary>What happened when a family was written out.</summary>
    public sealed class LedExportResult
    {
        public LedExportResult(string folder, IReadOnlyList<string> written, string error = null)
        {
            Folder = folder;
            Written = written;
            Error = error;
        }

        public string Folder { get; }

        public IReadOnlyList<string> Written { get; }

        /// <summary>Null when it worked. A message the panel can show when it did not.</summary>
        public string Error { get; }

        public bool Ok => Error == null;
    }

    /// <summary>
    /// The LED profiles openDash carries, and how a user gets them onto their hardware.
    ///
    /// **openDash does not install these.** ADR 0013 draws the line: a dashboard package is a file in a folder and the
    /// worst case is a dashboard somebody can close, but a device profile changes what hardware they own does, in a dark
    /// room, at speed. So the plugin offers, and a person chooses, and the choice is remembered and reversible.
    ///
    /// It also does not write into the RGB driver's own settings file. That file — PluginsData/Common/*LedsSettings*.json
    /// — is read by the driver at startup and written back on change, so a profile added underneath a running SimHub is
    /// overwritten by the driver the next time anything moves. docs/research/simhub-leds-format.md records that as
    /// unverified and probably unsafe, and this is what "probably unsafe" costs: openDash writes the .ledsprofile files
    /// to a folder and tells the user to import them, which is a step more work and cannot corrupt anything.
    /// </summary>
    public static class LedProfiles
    {
        public const string Extension = ".ledsprofile";

        /// <summary>The folder the profiles are written to, under SimHub's own directory.</summary>
        public const string FolderName = "openDash LED profiles";

        /// <summary>A brow profile is named "openDash brow 25"; the flag box is named for itself; everything else is a strip.</summary>
        public static LedDeviceFamily FamilyOf(string fileName)
        {
            if (fileName.IndexOf("flag box", StringComparison.OrdinalIgnoreCase) >= 0) return LedDeviceFamily.FlagBox;
            if (fileName.IndexOf("brow", StringComparison.OrdinalIgnoreCase) >= 0) return LedDeviceFamily.Brow;
            return LedDeviceFamily.Strip;
        }

        /// <summary>What the panel calls a family.</summary>
        public static string Label(this LedDeviceFamily family)
        {
            switch (family)
            {
                case LedDeviceFamily.Strip: return "RGB strips";
                case LedDeviceFamily.Brow: return "Brow strips";
                case LedDeviceFamily.FlagBox: return "Flag box";
                default: return family.ToString();
            }
        }

        /// <summary>The families the plugin actually carries a profile for, in enum order.</summary>
        public static IReadOnlyList<LedDeviceFamily> FamiliesIn(ILedProfileSource source)
        {
            return source.Profiles.Select(p => p.Family).Distinct().OrderBy(f => (int)f).ToList();
        }

        /// <summary>
        /// One line for the panel, saying what is carried without suggesting anything has been installed.
        /// "openDash carries 20 LED profiles" is the whole claim; it has written nothing.
        /// </summary>
        public static string Summary(ILedProfileSource source)
        {
            var n = source.Profiles.Count;
            if (n == 0) return "No LED profiles are bundled with this build.";
            var families = FamiliesIn(source).Select(f => f.Label().ToLowerInvariant());
            return string.Format(
                "openDash carries {0} LED profile{1} for {2}. Nothing is installed until you choose to export them.",
                n, n == 1 ? "" : "s", string.Join(", ", families));
        }

        /// <summary>
        /// Writes the profiles of one family into <paramref name="root"/>/<see cref="FolderName"/> and returns what it
        /// wrote. Overwrites a file of the same name, because those are openDash's own and replacing them is the point;
        /// it touches nothing else in the folder, and nothing at all outside it.
        /// </summary>
        public static LedExportResult Export(ILedProfileSource source, string root, LedDeviceFamily family)
        {
            var folder = Path.Combine(root ?? string.Empty, FolderName);
            var written = new List<string>();
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var profile in source.Profiles.Where(p => p.Family == family))
                {
                    var target = Path.Combine(folder, profile.FileName);
                    using (var input = source.Open(profile))
                    using (var output = File.Create(target))
                    {
                        input.CopyTo(output);
                    }

                    written.Add(profile.FileName);
                }
            }
            catch (Exception e)
            {
                return new LedExportResult(folder, written, e.Message);
            }

            return new LedExportResult(folder, written);
        }

        /// <summary>What to tell the user once the files are on disk. The import is theirs to drive, and this says how.</summary>
        public static string ImportInstructions(LedExportResult result, LedDeviceFamily family)
        {
            if (!result.Ok) return "Could not write the profiles: " + result.Error;
            if (result.Written.Count == 0) return "There are no " + family.Label().ToLowerInvariant() + " profiles in this build.";

            var where = family == LedDeviceFamily.FlagBox
                ? "SimHub's RGB Matrix (8x8) device"
                : "SimHub's RGB LEDs device";
            return string.Format(
                "Wrote {0} profile{1} to {2}.\r\n\r\nTo use one: open {3}, choose Profiles, then Import, and pick the file. " +
                "openDash does not write to your device itself, so nothing changes until you do this.",
                result.Written.Count, result.Written.Count == 1 ? "" : "s", result.Folder, where);
        }
    }
}
