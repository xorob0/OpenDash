// SettingsHold.cs: when OpenDash may save its settings file, and what a save stamps on it (#864).
//
// The start reads the file's stamp (OpenDashSettings.SettingsVersion) before Normalise, which is what turns a newer
// plugin's rig into this one's shape, and asks SettingsRescue what to make of it. A file a newer plugin wrote holds
// OpenDash's own saves -- the end of Init, SimHub closing, the update check's bookkeeping -- so that a rollback
// that is only tried leaves the file as the newer plugin wrote it. The first save somebody causes by changing
// something (the panel, a wheel press, an update) is made all the same, stamps the file with this version, and
// ends the hold; from then on the newer rig is in the .newer.json copy, or in SimHub's _Backups when no copy
// holds it, and Home says which (PanelAttention, AttentionInput.SettingsSavedOver).
//
// Pure: no SimHub, no WPF. OpenDash.cs passes the reading and the writing in, and NewerSettingsTests holds it.
using System;

namespace OpenDashPlugin
{
    public sealed class SettingsHold
    {
        private SettingsHold(SettingsRescue rescue)
        {
            Rescue = rescue ?? SettingsRescue.Read;
            Holding = Rescue.HoldsSaves;
        }

        /// <summary>What the start made of the file. Never null.</summary>
        public SettingsRescue Rescue { get; private set; }

        /// <summary>Whether the file is a newer plugin's and nothing has been saved over it yet.</summary>
        public bool Holding { get; private set; }

        /// <summary>Whether this session has saved over a file a newer plugin wrote. The newer rig is then only in
        /// the copy, or in SimHub's _Backups when no copy holds it.</summary>
        public bool SavedOver { get; private set; }

        /// <summary>Whether a save OpenDash makes on its own, rather than because somebody changed something, is
        /// made: never while the file is a newer plugin's.</summary>
        public bool MaySaveOnOwn { get { return !Holding; } }

        /// <summary>The hold before the settings are read, and after a start that read nothing out of the
        /// ordinary: it never holds.</summary>
        public static SettingsHold None() { return new SettingsHold(SettingsRescue.Read); }

        /// <summary>
        /// What the start makes of the settings SimHub handed back, before anything is saved: a file SimHub could
        /// not read is set aside (<paramref name="defaulted"/>), a file a newer plugin stamped is set aside and
        /// holds the saves, and only then is <paramref name="settings"/> normalised, since Normalise is what
        /// reduces a newer rig to this plugin's shape. A failure to look at the file is logged and holds nothing.
        /// </summary>
        public static SettingsHold Begin(OpenDashSettings settings, bool defaulted, Func<string> settingsPath, string running, IInstallLog log)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var rescue = SettingsRescue.Read;
            try
            {
                var path = settingsPath == null ? null : settingsPath();
                rescue = defaulted
                    ? SettingsRescue.Inspect(path, true, log)
                    : SettingsRescue.InspectNewer(path, settings.SettingsVersion, running, log);
            }
            catch (Exception ex)
            {
                if (log != null)
                {
                    log.Error((defaulted
                        ? "Looking at the settings file SimHub could not read failed: "
                        : "Looking at the settings a newer OpenDash saved failed: ") + ex.Message);
                }
            }
            settings.Normalise();
            return new SettingsHold(rescue);
        }

        /// <summary>
        /// A save somebody caused by changing something: <paramref name="stamped"/> is stamped with
        /// <paramref name="running"/> before <paramref name="write"/> runs, and the hold ends once it has written.
        /// A write that throws leaves the hold as it was and the exception to the caller.
        /// </summary>
        public void Save(OpenDashSettings stamped, string running, Action write)
        {
            if (stamped == null) throw new ArgumentNullException("stamped");
            if (write == null) throw new ArgumentNullException("write");
            stamped.SettingsVersion = running;
            write();
            Holding = false;
            if (Rescue.Newer) SavedOver = true;
        }

        /// <summary>
        /// Something was changed whose save is still to come, as an update's is (OpenDash.ApplyUpdate): the hold
        /// ends now, so that a SimHub closing before that save runs saves on its way out instead of dropping it.
        /// </summary>
        public void Changed()
        {
            Holding = false;
        }

        /// <summary>The line the log takes for a save that was skipped.</summary>
        public string SkippedLine()
        {
            return "Not saving the settings: OpenDash " + Rescue.SavedBy + " wrote them and nothing has been changed since.";
        }
    }
}
