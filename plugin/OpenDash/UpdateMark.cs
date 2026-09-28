// UpdateMark.cs: what the idle screen is told about a newer release. #83.
//
// The idle screen is the one dashboard surface that may say an update exists, because nobody is driving
// while it is up. What it is told is two properties, OpenDash.UpdateAvailable and OpenDash.UpdateVersion,
// and this file is every decision behind them. Like UpdateCheck it is pure and tested; the plugin holds
// the one remembered answer and reads it through here.
using System;

namespace OpenDashPlugin
{
    public static class UpdateMark
    {
        /// <summary>
        /// What a completed check leaves remembered: the release it offered, or nothing when it found the rig
        /// current.
        /// </summary>
        /// <remarks>
        /// Remembered, and persisted, because the check runs at most once a day (ADR 0012) and SimHub starts
        /// more often than that: a mark that lived only as long as the answer that raised it would vanish on
        /// the second start of the day, with the release still unapplied. An answer nobody could read changes
        /// nothing in either direction -- Unreachable is not "up to date", and it is not "no longer
        /// available" either -- and a check that never ran (null, Disabled, Checking, Idle) is not an answer.
        /// </remarks>
        public static string Remember(string remembered, UpdateStatus answer)
        {
            if (answer == null) return remembered;
            switch (answer.State)
            {
                case UpdateState.UpdateAvailable:
                    return string.IsNullOrWhiteSpace(answer.LatestVersion) ? remembered : answer.LatestVersion;
                case UpdateState.UpToDate:
                    return null;
                default:
                    return remembered;
            }
        }

        /// <summary>
        /// The version the offer is compared against: UpdateCheck.ComparableInstalled, unless the new plugin is
        /// already staged.
        /// </summary>
        /// <remarks>
        /// A staged assembly is a plugin that has moved: it is put in place when SimHub closes, and what is left
        /// to do is the restart the panel already asks for rather than a second download. Compared as it was
        /// before, the running plugin's old version would keep the mark up, and the panel's Update button with
        /// it, until the restart -- offering again a release that has been fetched and applied.
        /// </remarks>
        public static string Installed(string dashboards, string plugin, bool pluginStaged) =>
            pluginStaged ? dashboards : UpdateCheck.ComparableInstalled(dashboards, plugin);

        /// <summary>
        /// The release the mark offers, or null when it offers none.
        /// </summary>
        /// <param name="remembered">What <see cref="Remember"/> left.</param>
        /// <param name="installed">What the rig runs, as UpdateCheck.ComparableInstalled reduces it: the older of
        /// the dashboards and the plugin, the same version the check compared.</param>
        /// <remarks>
        /// Compared again rather than trusted, so that a rig which has caught up -- an update applied, a plugin
        /// swapped at the restart -- stops being told about a release it now runs, without waiting a day for
        /// the next check to say so. An installed version nobody can read keeps the mark quiet: silence is the
        /// failure this feature is allowed.
        /// </remarks>
        public static string Offered(string remembered, string installed)
        {
            if (string.IsNullOrWhiteSpace(remembered) || string.IsNullOrWhiteSpace(installed)) return null;
            if (installed == Versioning.UnknownVersion) return null;
            return Versioning.VersionCompare(remembered, installed) > 0 ? remembered : null;
        }

        /// <summary>
        /// OpenDash.UpdateAvailable: whether there is a release to offer.
        /// </summary>
        /// <param name="enabled">The update check setting. Off silences the mark whatever was remembered, so one
        /// switch governs the panel line and the mark together, and it is read live rather than folded into
        /// <paramref name="offered"/>, so the mark goes the moment the switch does.</param>
        /// <param name="offered">What <see cref="Offered"/> returned.</param>
        public static bool Available(bool enabled, string offered) => enabled && offered != null;

        /// <summary>
        /// What the panel's update line opens on.
        /// </summary>
        /// <param name="enabled">The update check setting.</param>
        /// <param name="last">What this session's last completed check concluded, or null.</param>
        /// <param name="offered">What <see cref="Offered"/> returned.</param>
        /// <param name="installed">The dashboards' version, which is what the panel's line names.</param>
        /// <remarks>
        /// The mark and the panel say the same thing, because a driver who reads "update" on the idle screen
        /// and opens the panel to act on it has to find the offer there. The check runs once a day, so on the
        /// second start of the day there is no answer from this session and the offer is the remembered one;
        /// the panel's Update button then asks GitHub for the release before applying it, since a person who
        /// presses a button has asked and the interval does not apply (ADR 0012).
        ///
        /// A session's answer that offers a release the rig has since caught up with is not shown again, and
        /// one that offers nothing stands as it was: up to date, or unreachable, and in either case silent
        /// unless it was a press that asked.
        /// </remarks>
        public static UpdateStatus Opening(bool enabled, UpdateStatus last, string offered, string installed)
        {
            if (!enabled) return new UpdateStatus { InstalledVersion = installed };
            if (offered == null)
            {
                return last != null && last.State != UpdateState.UpdateAvailable ? last : new UpdateStatus { InstalledVersion = installed };
            }
            if (last != null && last.State == UpdateState.UpdateAvailable && last.LatestVersion == offered) return last;
            return new UpdateStatus { State = UpdateState.UpdateAvailable, InstalledVersion = installed, LatestVersion = offered };
        }

        /// <summary>
        /// OpenDash.UpdateVersion: the release, as the mark can draw it, or the empty string.
        /// </summary>
        /// <remarks>
        /// The mark's box is measured in contract.ts for Contract.UpdateVersionMaxLength of the widest
        /// character in Contract.UpdateVersionCharacters, and WPF clips whatever outgrows a box without a
        /// word. A version that does not fit that promise is therefore not published at all, and the mark says
        /// an update is available without naming it, which is less specific and never wrong. A leading v, the
        /// way a tag is written, is not part of the version.
        /// </remarks>
        public static string Shown(bool enabled, string offered)
        {
            if (!Available(enabled, offered)) return string.Empty;
            var version = offered.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? offered.Substring(1) : offered;
            if (version.Length == 0 || version.Length > Contract.UpdateVersionMaxLength) return string.Empty;
            foreach (var c in version)
            {
                if (Contract.UpdateVersionCharacters.IndexOf(c) < 0) return string.Empty;
            }
            return version;
        }
    }
}
