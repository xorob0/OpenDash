// FlagBoxInstallPlan.cs: what to do with the flag box profile, decided without touching SimHub.
//
// The decision and the act are split the way DashboardInstaller splits them: everything here is pure
// and is compiled into OpenDash.Tests, while FlagBoxInstaller.cs holds the half that reaches into
// SimHub's own object model and cannot run off Windows. No SimHub or WPF types in this file.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>What the plugin found when it compared its own profile with what SimHub holds.</summary>
    public enum FlagBoxInstallState
    {
        /// <summary>No .ledsprofile is embedded in this build.</summary>
        NotEmbedded,
        /// <summary>SimHub's matrix settings could not be reached; the file is on disk to import by hand.</summary>
        Unavailable,
        /// <summary>SimHub has no openDash profile.</summary>
        NotInstalled,
        /// <summary>SimHub has this exact version of it.</summary>
        UpToDate,
        /// <summary>SimHub has an older openDash profile.</summary>
        Outdated,
        /// <summary>Installing failed; the message says why.</summary>
        Failed,
    }

    /// <summary>One profile already in SimHub, reduced to what the decision needs.</summary>
    public sealed class InstalledProfile
    {
        public Guid ProfileId { get; set; }

        public string Name { get; set; }

        /// <summary>The profile's Description, which carries the openDash version when we wrote it.</summary>
        public string Description { get; set; }
    }

    public sealed class FlagBoxPlan
    {
        public FlagBoxInstallState State { get; set; }

        /// <summary>The profile in SimHub this plan is about, null when there is none.</summary>
        public Guid? Existing { get; set; }

        /// <summary>Version stamped in the embedded profile, null when it carries none.</summary>
        public string EmbeddedVersion { get; set; }

        /// <summary>Version of the installed profile, null when it carries none or there is none.</summary>
        public string InstalledVersion { get; set; }

        /// <summary>True when clicking the button would change SimHub's settings.</summary>
        public bool WouldChange
        {
            get { return State == FlagBoxInstallState.NotInstalled || State == FlagBoxInstallState.Outdated; }
        }
    }

    public static class FlagBoxInstallPlan
    {
        /// <summary>Stamped into the profile's Author by the build; how we tell ours from the user's.</summary>
        public const string Author = "openDash";

        /// <summary>
        /// The version the build stamped into a profile description, or null.
        /// Mirrors flagBoxVersion() in packages/dash/src/leds/profile.ts; the two are one contract and
        /// a test reads the built profile to keep them from drifting.
        /// </summary>
        public static string VersionOf(string description)
        {
            if (string.IsNullOrEmpty(description)) return null;
            const string marker = "Built by openDash ";
            int at = description.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return null;
            int start = at + marker.Length;
            int end = start;
            while (end < description.Length && IsVersionChar(description[end])) end++;
            return end == start ? null : description.Substring(start, end - start);
        }

        private static bool IsVersionChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '+';
        }

        /// <summary>
        /// What to do, given the embedded profile and what SimHub already holds.
        ///
        /// Matching is by ProfileId, which the build derives from the profile's name and so is stable
        /// across versions. A profile of ours that the user has since edited keeps that id, so an
        /// update replaces their edits: the panel says so, and it only ever happens when the version
        /// differs and the user presses the button.
        /// </summary>
        public static FlagBoxPlan Decide(Guid embeddedId, string embeddedDescription, IEnumerable<InstalledProfile> installed)
        {
            var plan = new FlagBoxPlan { EmbeddedVersion = VersionOf(embeddedDescription) };
            if (embeddedId == Guid.Empty)
            {
                plan.State = FlagBoxInstallState.NotEmbedded;
                return plan;
            }
            if (installed == null)
            {
                plan.State = FlagBoxInstallState.Unavailable;
                return plan;
            }
            foreach (var profile in installed)
            {
                if (profile == null || profile.ProfileId != embeddedId) continue;
                plan.Existing = profile.ProfileId;
                plan.InstalledVersion = VersionOf(profile.Description);
                plan.State = string.Equals(plan.InstalledVersion, plan.EmbeddedVersion, StringComparison.Ordinal)
                    ? FlagBoxInstallState.UpToDate
                    : FlagBoxInstallState.Outdated;
                return plan;
            }
            plan.State = FlagBoxInstallState.NotInstalled;
            return plan;
        }

        /// <summary>The label on the button, which has to say what pressing it does.</summary>
        public static string ButtonLabel(FlagBoxPlan plan)
        {
            if (plan == null) return "Install into SimHub";
            switch (plan.State)
            {
                case FlagBoxInstallState.Outdated: return "Update in SimHub";
                case FlagBoxInstallState.UpToDate: return "Reinstall in SimHub";
                default: return "Install into SimHub";
            }
        }

        /// <summary>The line beside the button. Says what is true now, not what we wish were true.</summary>
        public static string Summary(FlagBoxPlan plan, string path)
        {
            if (plan == null) return "Flag box: not checked.";
            switch (plan.State)
            {
                case FlagBoxInstallState.NotEmbedded:
                    return "No flag box profile is embedded in this build.";
                case FlagBoxInstallState.Unavailable:
                    return "SimHub's matrix settings are not available, so openDash cannot install the profile. "
                        + "Import it by hand from " + (path ?? "the openDash folder") + ".";
                case FlagBoxInstallState.NotInstalled:
                    return "Not installed. Press the button to add it to SimHub's matrix profiles; "
                        + "then pick it on your matrix device.";
                case FlagBoxInstallState.UpToDate:
                    return "Installed and up to date"
                        + (plan.InstalledVersion == null ? "." : " (" + plan.InstalledVersion + ").");
                case FlagBoxInstallState.Outdated:
                    return "A newer profile is available ("
                        + (plan.InstalledVersion ?? "unknown") + " to " + (plan.EmbeddedVersion ?? "unknown")
                        + "). Updating replaces the copy in SimHub, including any changes you made to it there.";
                default:
                    return "Installing the flag box profile failed.";
            }
        }
    }
}
