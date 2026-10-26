// SettingsRescue.cs: a settings file SimHub could not read, set aside before OpenDash saves over it (#643).
//
// SimHub reads OpenDash.GeneralSettings.json through FromJsonFileWithVersionning, which tries the file and then
// its _Backups copies and hands back null when none of them can be read; ReadCommonSettings then calls the
// plugin's factory, and the plugin starts on defaults. Newtonsoft's exception goes to SimHub's log and nowhere
// else. The plugin's first save, in Init, then writes the defaults over the file, and SimHub's versioning moves
// the unreadable one to _b1, where ten later saves push it out of _Backups altogether. A driver whose file was
// damaged met an empty rig and no word of why.
//
// So when the factory was called and the file is there with something in it, the file is copied beside itself as
// OpenDash.GeneralSettings.unreadable.json before anything is saved, and Home says so (PanelAttention's
// SettingsUnreadable rule). A copy already there is never overwritten: it is the first unreadable file, the one
// a driver may have started mending. When it holds something else, the panel names SimHub's _Backups instead,
// where this start's save puts the file as _b1.
//
// A file a newer plugin wrote is the same outcome through another door (#864). SimHub reads it without complaint,
// since Json.NET ignores the members it does not know, so the older plugin gets a rig with every field it never
// had dropped, and its first save would write that reduced rig over the file. Every save stamps the file with the
// plugin version that wrote it (OpenDashSettings.SettingsVersion), and the rule is this: when the stamp is newer
// than the running plugin, the file is copied beside itself as OpenDash.GeneralSettings.newer.json before
// anything is saved, under the same never-overwrite rule as the unreadable copy, and OpenDash does not save the
// file on its own -- not at the end of Init, not on SimHub's close, not for the update check's bookkeeping --
// until the driver changes something. A rollback that is only tried leaves the file exactly as the newer plugin
// wrote it; a driver who works on the older plugin has their changes kept, and the newer rig is in the copy.
// Equal versions, and a file with no stamp (written before it existed), are read as they always were.
//
// Pure System.IO: no SimHub, no WPF. SettingsRescueTests holds it against an unreadable fixture file.
using System;
using System.IO;
using System.Linq;

namespace OpenDashPlugin
{
    public sealed class SettingsRescue
    {
        /// <summary>What the copy's name adds before the extension: OpenDash.GeneralSettings.unreadable.json.</summary>
        public const string CopySuffix = ".unreadable";

        /// <summary>What the copy of a file a newer plugin wrote adds: OpenDash.GeneralSettings.newer.json.</summary>
        public const string NewerSuffix = ".newer";

        /// <summary>SimHub's folder of earlier versions, beside the settings file.</summary>
        public const string BackupsFolder = "_Backups";

        private SettingsRescue(string settingsPath, bool unreadable, string copyPath)
        {
            SettingsPath = settingsPath;
            Unreadable = unreadable;
            CopyPath = copyPath;
        }

        private SettingsRescue(string settingsPath, string savedBy, string copyPath)
        {
            SettingsPath = settingsPath;
            Newer = true;
            SavedBy = savedBy;
            CopyPath = copyPath;
        }

        /// <summary>Nothing to say: the settings were read, or there was no file to read.</summary>
        public static readonly SettingsRescue Read = new SettingsRescue(null, false, null);

        /// <summary>Whether the file was written by a newer plugin than this one (#864). Never with
        /// <see cref="Unreadable"/>: a file SimHub could not read has no stamp to read.</summary>
        public bool Newer { get; private set; }

        /// <summary>The plugin version that wrote the file, when <see cref="Newer"/>; null otherwise.</summary>
        public string SavedBy { get; private set; }

        /// <summary>Whether OpenDash should leave the file alone until the driver changes something.</summary>
        public bool HoldsSaves { get { return Newer; } }

        /// <summary>The settings file SimHub was asked for, or null when nothing was set aside.</summary>
        public string SettingsPath { get; private set; }

        /// <summary>Whether the file was there with something in it and SimHub handed back defaults.</summary>
        public bool Unreadable { get; private set; }

        /// <summary>The copy that holds the unreadable file, or the newer one, or null when none does: a copy of
        /// some other file was already there, or the copy could not be written.</summary>
        public string CopyPath { get; private set; }

        /// <summary>SimHub's _Backups folder beside the settings file, or null.</summary>
        public string BackupsPath
        {
            get
            {
                var folder = SettingsPath == null ? null : Path.GetDirectoryName(SettingsPath);
                return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, BackupsFolder);
            }
        }

        /// <summary>Where the unreadable file goes: beside it, ".unreadable" before its extension.</summary>
        public static string CopyPathFor(string settingsPath)
        {
            return PathWith(settingsPath, CopySuffix);
        }

        /// <summary>Where a file a newer plugin wrote goes: beside it, ".newer" before its extension.</summary>
        public static string NewerPathFor(string settingsPath)
        {
            return PathWith(settingsPath, NewerSuffix);
        }

        private static string PathWith(string settingsPath, string suffix)
        {
            if (string.IsNullOrEmpty(settingsPath)) return null;
            var folder = Path.GetDirectoryName(settingsPath) ?? string.Empty;
            return Path.Combine(folder, Path.GetFileNameWithoutExtension(settingsPath) + suffix + Path.GetExtension(settingsPath));
        }

        /// <summary>
        /// Whether a file stamped <paramref name="savedBy"/> was written by a newer plugin than
        /// <paramref name="running"/>: strictly newer by <see cref="Versioning.VersionCompare"/>. No stamp is older
        /// than everything, so a file from before the stamp is never newer; an equal version is not newer.
        /// </summary>
        public static bool IsNewer(string savedBy, string running)
        {
            if (string.IsNullOrWhiteSpace(savedBy) || string.IsNullOrWhiteSpace(running)) return false;
            return Versioning.VersionCompare(savedBy, running) > 0;
        }

        /// <summary>
        /// What to make of a file SimHub read, before the first save: when its stamp is newer than this plugin,
        /// the file is copied beside itself as .newer.json, never over an earlier copy that holds something else,
        /// and the answer holds OpenDash's own saves. Otherwise <see cref="Read"/>.
        /// </summary>
        /// <param name="settingsPath">The file SimHub read; null when it cannot be named, and then nothing is
        /// copied but the saves are held all the same, since the file is still the newer plugin's.</param>
        /// <param name="savedBy">The file's <see cref="OpenDashSettings.SettingsVersion"/>, as read.</param>
        /// <param name="running">This plugin's version.</param>
        public static SettingsRescue InspectNewer(string settingsPath, string savedBy, string running, IInstallLog log)
        {
            if (!IsNewer(savedBy, running)) return Read;
            var said = "The settings were saved by OpenDash " + savedBy + ", newer than this " + running
                + ". OpenDash will not save over them until something is changed";
            var copy = NewerPathFor(settingsPath);
            if (copy == null)
            {
                if (log != null) log.Warn(said + ".");
                return new SettingsRescue(settingsPath, savedBy, null);
            }
            try
            {
                if (!File.Exists(settingsPath))
                {
                    if (log != null) log.Warn(said + ".");
                    return new SettingsRescue(settingsPath, savedBy, null);
                }
                if (!File.Exists(copy))
                {
                    File.Copy(settingsPath, copy, false);
                    if (log != null) log.Warn(said + ", and the file is kept as " + copy + ".");
                    return new SettingsRescue(settingsPath, savedBy, copy);
                }
                if (SameBytes(settingsPath, copy))
                {
                    if (log != null) log.Warn(said + ", and the file is already kept as " + copy + ".");
                    return new SettingsRescue(settingsPath, savedBy, copy);
                }
                if (log != null) log.Warn(said + ". " + copy + " already holds an earlier file and is left as it is.");
                return new SettingsRescue(settingsPath, savedBy, null);
            }
            catch (Exception ex)
            {
                if (log != null) log.Error(said + ", and setting the file aside as " + copy + " failed: " + ex.Message);
                return new SettingsRescue(settingsPath, savedBy, null);
            }
        }

        /// <summary>
        /// What to make of a read that handed back defaults, before the first save. A read that did not
        /// (<paramref name="defaulted"/> false), a file that is not there, and a file with nothing in it are
        /// all a first run: there is nothing to keep.
        /// </summary>
        /// <param name="settingsPath">The file SimHub read, PluginsData\Common\OpenDash.GeneralSettings.json.</param>
        /// <param name="defaulted">Whether the plugin's factory was called, or the read threw.</param>
        public static SettingsRescue Inspect(string settingsPath, bool defaulted, IInstallLog log)
        {
            if (!defaulted || string.IsNullOrEmpty(settingsPath)) return Read;
            string content;
            try
            {
                if (!File.Exists(settingsPath)) return Read;
                content = File.ReadAllText(settingsPath);
            }
            catch (Exception ex)
            {
                // There and not even readable as text: still a file somebody would want back.
                if (log != null) log.Warn("Could not read " + settingsPath + ": " + ex.Message);
                content = null;
            }
            if (content != null && string.IsNullOrWhiteSpace(content)) return Read;

            var copy = CopyPathFor(settingsPath);
            try
            {
                if (!File.Exists(copy))
                {
                    File.Copy(settingsPath, copy, false);
                    if (log != null) log.Warn("The settings could not be read and OpenDash started on defaults. The file is kept as " + copy + ".");
                    return new SettingsRescue(settingsPath, true, copy);
                }
                if (SameBytes(settingsPath, copy))
                {
                    if (log != null) log.Warn("The settings could not be read and OpenDash started on defaults. The file is already kept as " + copy + ".");
                    return new SettingsRescue(settingsPath, true, copy);
                }
                if (log != null) log.Warn("The settings could not be read and OpenDash started on defaults. " + copy
                    + " already holds an earlier file and is left as it is; SimHub keeps this one in " + BackupsFolder + ".");
                return new SettingsRescue(settingsPath, true, null);
            }
            catch (Exception ex)
            {
                if (log != null) log.Error("The settings could not be read, and setting the file aside as " + copy + " failed: " + ex.Message);
                return new SettingsRescue(settingsPath, true, null);
            }
        }

        private static bool SameBytes(string a, string b)
        {
            return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
        }
    }
}
