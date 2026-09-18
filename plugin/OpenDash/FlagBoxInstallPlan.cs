// FlagBoxInstallPlan.cs: what to do with the flag box profile, decided without touching SimHub.
//
// The decision and the act are split the way DashboardInstaller splits them: everything here is pure
// and is compiled into OpenDash.Tests, while FlagBoxInstaller.cs holds the half that reaches into
// SimHub's own object model and cannot run off Windows. No SimHub or WPF types in this file.
using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Something true about this install that is not its state, or null when there is nothing to add.
        /// </summary>
        /// <remarks>
        /// Today there is exactly one: the profile went in correctly and the device is listing its
        /// maker's built-in profiles instead, so it will not be in the dropdown until the user turns that
        /// off. Not a failure -- nothing went wrong and nothing needs repeating -- but a driver told only
        /// "installed" would go looking for a profile they cannot see.
        /// </remarks>
        public string Note { get; set; }

        /// <summary>True when clicking the button would change SimHub's settings.</summary>
        public bool WouldChange
        {
            get { return State == FlagBoxInstallState.NotInstalled || State == FlagBoxInstallState.Outdated; }
        }
    }

    public static class FlagBoxInstallPlan
    {
        /// <summary>
        /// Whether SimHub is showing the device's built-in profiles instead of the saved ones.
        /// </summary>
        /// <remarks>
        /// A state, not a failure. The install is correct either way -- the profile goes where SimHub
        /// saves it -- but while this is on, the device's own dropdown lists `BuiltInProfiles` and ours
        /// is not among them, so a driver is told they have installed something they cannot find. The
        /// switch is the user's and openDash does not touch it (`UseBuiltInProfiles` has a private
        /// setter in any case); what it can do is say which switch it is.
        /// </remarks>
        public static bool BuiltInModeOf(bool hasBuiltIn, bool useBuiltIn)
        {
            return hasBuiltIn && useBuiltIn;
        }

        /// <summary>The sentence a row shows when the profile is installed but the device is listing the
        /// maker's built-in profiles instead.</summary>
        public const string BuiltInModeNote =
            "SimHub is showing your device's built-in profiles, so openDash's will not be in its list. "
            + "Turn built-in profiles off on the device to see it.";

        /// <summary>Stamped into the profile's Author by the build; how we tell ours from the user's.</summary>
        public const string Author = "openDash";

        /// <summary>The warning both presses owe the user. Update and Reinstall cost the same thing --
        /// the copy in SimHub goes, and whatever the user changed in it goes with it -- so the sentence
        /// names the press rather than either verb and both branches carry it.</summary>
        public const string Replaces =
            "Pressing the button replaces the copy in SimHub, including any changes you made to it there.";

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

        /// <summary>
        /// One row's plan over several profiles, for a group the panel draws as a single line.
        ///
        /// The worst member decides, in the order below, so a row can never read better than the profile
        /// it is worst about: a group of nineteen strips where one is missing says "Not installed", and a
        /// press then installs every member. The versions survive only when the members agree, because a
        /// pill that says "Installed (0.4.0)" over a group holding two different versions is a lie the
        /// user cannot see through.
        ///
        /// <see cref="FlagBoxPlan.Existing"/> is left null: a group is several profiles and there is no
        /// single one for it to name. Nothing reads it for a row; the per-member plans still carry theirs.
        /// </summary>
        public static FlagBoxPlan Combine(IEnumerable<FlagBoxPlan> plans)
        {
            FlagBoxPlan worst = null;
            var count = 0;
            string embedded = null;
            string installed = null;
            var embeddedAgree = true;
            var installedAgree = true;
            foreach (var plan in plans ?? Enumerable.Empty<FlagBoxPlan>())
            {
                if (plan == null) continue;
                if (count == 0)
                {
                    embedded = plan.EmbeddedVersion;
                    installed = plan.InstalledVersion;
                }
                else
                {
                    if (!string.Equals(embedded, plan.EmbeddedVersion, StringComparison.Ordinal)) embeddedAgree = false;
                    if (!string.Equals(installed, plan.InstalledVersion, StringComparison.Ordinal)) installedAgree = false;
                }
                count++;
                if (worst == null || Severity(plan.State) < Severity(worst.State)) worst = plan;
            }
            // An empty group is a build that embedded none of these profiles, which is the same answer
            // the single case gives and the same one the panel already knows how to draw.
            if (worst == null) return new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
            return new FlagBoxPlan
            {
                State = worst.State,
                EmbeddedVersion = embeddedAgree ? embedded : null,
                InstalledVersion = installedAgree ? installed : null,
            };
        }

        /// <summary>How loudly a state has to be reported, lowest first. A failure the user can act on
        /// beats a state they cannot, and "one of these is missing" beats "the rest are current".</summary>
        private static int Severity(FlagBoxInstallState state)
        {
            switch (state)
            {
                case FlagBoxInstallState.Failed: return 0;
                case FlagBoxInstallState.NotEmbedded: return 1;
                case FlagBoxInstallState.Unavailable: return 2;
                case FlagBoxInstallState.NotInstalled: return 3;
                case FlagBoxInstallState.Outdated: return 4;
                default: return 5;
            }
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
                    // Installing adds a profile; it does not switch to one. SimHub picks the current
                    // profile from its own persisted activeProfileId, so a user who presses the button
                    // and then sees nothing on the box has not been told the rest of the job.
                    //
                    // The button here says Reinstall, which costs the user exactly what Update costs
                    // them: the copy in SimHub is replaced by id, their edits to it with it. Saying so
                    // only in the Outdated branch left the one press that is never necessary as the one
                    // press that was never warned about.
                    return "Installed and up to date"
                        + (plan.InstalledVersion == null ? ". " : " (" + plan.InstalledVersion + "). ")
                        + "Select it on your matrix device to use it. "
                        + Replaces;
                case FlagBoxInstallState.Outdated:
                    return "A newer profile is available ("
                        + (plan.InstalledVersion ?? "unknown") + " to " + (plan.EmbeddedVersion ?? "unknown")
                        + "). " + Replaces;
                default:
                    return "Installing the flag box profile failed.";
            }
        }
    }
}
