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

        /// <summary>SimHub's folder of earlier versions, beside the settings file.</summary>
        public const string BackupsFolder = "_Backups";

        private SettingsRescue(string settingsPath, bool unreadable, string copyPath)
        {
            SettingsPath = settingsPath;
            Unreadable = unreadable;
            CopyPath = copyPath;
        }

        /// <summary>Nothing to say: the settings were read, or there was no file to read.</summary>
        public static readonly SettingsRescue Read = new SettingsRescue(null, false, null);

        /// <summary>The settings file SimHub was asked for, or null when nothing was set aside.</summary>
        public string SettingsPath { get; private set; }

        /// <summary>Whether the file was there with something in it and SimHub handed back defaults.</summary>
        public bool Unreadable { get; private set; }

        /// <summary>The copy that holds the unreadable file, or null when none does: a copy of some other file
        /// was already there, or the copy could not be written.</summary>
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
            if (string.IsNullOrEmpty(settingsPath)) return null;
            var folder = Path.GetDirectoryName(settingsPath) ?? string.Empty;
            return Path.Combine(folder, Path.GetFileNameWithoutExtension(settingsPath) + CopySuffix + Path.GetExtension(settingsPath));
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
