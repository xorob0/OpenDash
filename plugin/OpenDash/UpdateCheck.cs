// UpdateCheck.cs: whether to ask, which release matters, and what the panel says about it.
//
// Everything here is a decision openDash makes and can therefore get wrong, so all of it is pure and all of it is
// tested. What is left outside is the HTTP call itself, which encodes a fact about Windows rather than a choice of
// ours. See docs/decisions/0012-update-checks.md, which this implements rather than resembles.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What the Dashboard section is showing about updates.</summary>
    public enum UpdateState
    {
        /// <summary>The setting is off, so nothing has been fetched and nothing will be.</summary>
        Disabled,

        /// <summary>Nothing asked for yet.</summary>
        Idle,

        Checking,
        UpToDate,
        UpdateAvailable,

        /// <summary>Asked and could not tell: no network, a refused or unreadable answer, a repository that
        /// cannot be seen. Deliberately not distinguished from one another in the panel, because a driver can
        /// act on none of them differently.</summary>
        Unreachable,
    }

    /// <summary>The answer a check produced, and the sentence the panel puts in front of a person.</summary>
    public sealed class UpdateStatus
    {
        public UpdateState State { get; set; } = UpdateState.Idle;
        public string InstalledVersion { get; set; }
        public string LatestVersion { get; set; }
        public string Notes { get; set; }
        public string Url { get; set; }

        /// <summary>True when a person pressed the button. A background check that finds nothing says nothing.</summary>
        public bool Manual { get; set; }

        /// <summary>Whether the Dashboard section shows anything at all about updates.</summary>
        public bool IsVisible =>
            State == UpdateState.UpdateAvailable
            || State == UpdateState.Checking
            || (Manual && (State == UpdateState.UpToDate || State == UpdateState.Unreachable));

        public string Line => UpdateWording.Line(this);
    }

    public static class UpdateCheck
    {
        /// <summary>
        /// The list endpoint, not releases/latest.
        /// </summary>
        /// <remarks>
        /// releases/latest answers 404 while every release carries a hyphen in its tag, because release.yml marks
        /// such a tag as a pre-release and that endpoint ignores pre-releases. The day a tag without a hyphen is
        /// cut it will begin to answer, and moving to it will look like an obvious simplification; it would still
        /// be wrong for every user then running a candidate, who would be told nothing exists. ReleaseFor is what
        /// decides which release a given user may be offered, and it needs the list to do it.
        /// </remarks>
        public const string ReleasesUrl = "https://api.github.com/repos/xorob0/OpenDash/releases?per_page=10";

        /// <summary>Where the panel's link goes, for a person who would rather read it themselves.</summary>
        public const string ReleasesPageUrl = "https://github.com/xorob0/OpenDash/releases";

        /// <summary>Not within a day of the last answer, which ADR 0012 sets and nothing else depends on.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        /// <summary>
        /// Whether a request may be constructed at all. The setting is read here, before anything is built, so
        /// that off means nothing is fetched rather than fetched and discarded.
        /// </summary>
        public static bool ShouldCheck(bool enabled, long lastCheckTicksUtc, DateTime nowUtc, bool manual)
        {
            if (!enabled) return false;
            if (manual) return true; // a person who presses a button has asked, so the interval does not apply
            if (lastCheckTicksUtc <= 0) return true;
            if (lastCheckTicksUtc > nowUtc.Ticks) return true; // a clock that went backwards is not a reason to go quiet
            return new DateTime(lastCheckTicksUtc, DateTimeKind.Utc) + Interval <= nowUtc;
        }

        /// <summary>
        /// The version a release is compared against: what is installed, falling back to what the plugin carries.
        /// </summary>
        /// <remarks>
        /// A dashboard whose sidecar cannot be read reports Versioning.UnknownVersion, which parses as 0.0.0 with
        /// no pre-release part and therefore looks like a stable release. Left alone, such a user is never offered
        /// a candidate, which is the opposite of what their situation deserves, so the plugin's own version stands
        /// in instead.
        /// </remarks>
        public static string ComparableInstalled(string installedVersion, string fallbackVersion)
        {
            if (string.IsNullOrWhiteSpace(installedVersion) || installedVersion == Versioning.UnknownVersion)
            {
                return string.IsNullOrWhiteSpace(fallbackVersion) ? null : fallbackVersion;
            }
            return installedVersion;
        }

        /// <summary>
        /// The newest release this user may be offered, or null when none is newer than what they have.
        /// </summary>
        /// <remarks>
        /// A user on a stable version is not offered a pre-release; a user already on one is, since installing a
        /// candidate is how they said so. Drafts are never offered: they are not published.
        /// </remarks>
        public static ReleaseInfo ReleaseFor(IEnumerable<ReleaseInfo> releases, string installedVersion)
        {
            if (releases == null) return null;
            var onPreRelease = IsPreRelease(installedVersion);
            return releases
                .Where(r => r != null && !r.Draft)
                .Where(r => onPreRelease || !r.PreRelease)
                .Where(r => Versioning.VersionCompare(r.Version, installedVersion) > 0)
                .OrderByDescending(r => r.Version, Comparer<string>.Create(Versioning.VersionCompare))
                .FirstOrDefault();
        }

        /// <summary>True when a version carries a pre-release suffix, which is the dash and nothing else.</summary>
        public static bool IsPreRelease(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            var core = version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version.Substring(1) : version;
            var dash = core.IndexOf('-');
            return dash > 0 && dash < core.Length - 1;
        }

        /// <summary>What a completed check concluded.</summary>
        public static UpdateStatus Conclude(string installedVersion, IReadOnlyList<ReleaseInfo> releases, bool manual)
        {
            // Nothing readable came back. Never UpToDate on the strength of an answer we could not read.
            if (releases == null || releases.Count == 0)
            {
                return new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = installedVersion, Manual = manual };
            }
            var newer = ReleaseFor(releases, installedVersion);
            if (newer == null)
            {
                return new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = installedVersion, Manual = manual };
            }
            return new UpdateStatus
            {
                State = UpdateState.UpdateAvailable,
                InstalledVersion = installedVersion,
                LatestVersion = newer.Version,
                Notes = newer.Notes,
                Url = newer.Url,
                Manual = manual,
            };
        }
    }
}
