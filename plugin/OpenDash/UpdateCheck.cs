// UpdateCheck.cs: whether to ask, which release matters, and what the panel says about it.
//
// Everything here is a decision OpenDash makes and can therefore get wrong, so all of it is pure and all of it is
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
        /// The first page of the list endpoint, not releases/latest.
        /// </summary>
        /// <remarks>
        /// releases/latest answers 404 while every release carries a hyphen in its tag, because release.yml marks
        /// such a tag as a pre-release and that endpoint ignores pre-releases. The day a tag without a hyphen is
        /// cut it will begin to answer, and moving to it will look like an obvious simplification; it would still
        /// be wrong for every user then running a candidate, who would be told nothing exists. ReleaseFor is what
        /// decides which release a given user may be offered, and it needs the list to do it.
        ///
        /// The listing is newest first by creation date whatever the kind of each release, so one page is a window
        /// that a run of candidates can fill on its own, pushing the newest stable release out of sight. A user on
        /// a stable version has every candidate discarded and would then be told they have the newest release while
        /// a newer one exists, which is why pages are read rather than one page: see ReleasesPage and MaxPages. A
        /// hundred per page would only move that cliff further out, and it would cost about three megabytes on each
        /// check, since one release of this repository is roughly thirty kilobytes of JSON.
        /// </remarks>
        public static readonly string ReleasesUrl = "https://api.github.com/repos/xorob0/OpenDash/releases?per_page=" + PageSize;

        /// <summary>How many releases one page of the listing holds.</summary>
        public const int PageSize = 10;

        /// <summary>
        /// How many pages one check may read.
        /// </summary>
        /// <remarks>
        /// A bound is what keeps a rig that is merely up to date from walking the whole history of the repository
        /// every day, and fifty consecutive candidates is well past what this project could cut between two stable
        /// releases. Reaching it without an answer is reported as unreachable rather than as up to date, since a
        /// question the pages did not settle is precisely the quiet wrong answer this feature must never give.
        /// </remarks>
        public const int MaxPages = 5;

        /// <summary>The listing one page at a time. Page one is ReleasesUrl itself, which is the page GitHub
        /// answers with when none is asked for.</summary>
        public static string ReleasesPage(int page) => page <= 1 ? ReleasesUrl : ReleasesUrl + "&page=" + page;

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
        /// The version a release is compared against: the older of what is installed and what the plugin is.
        /// </summary>
        /// <remarks>
        /// **The older of the two, not the dashboards' alone.** An openDash install is two halves that move
        /// together, and either can be behind. Reading only the dashboards' version told a rig whose packages
        /// were current and whose plugin was a release behind that it had "the newest release" -- which is
        /// exactly the state a machine is in between staging the new assembly and restarting SimHub, and the
        /// state it stays in for good if that swap never lands. The offer has to survive until both halves have
        /// actually moved, or the only way out is a manual download.
        ///
        /// A dashboard whose sidecar cannot be read reports Versioning.UnknownVersion, which parses as 0.0.0 with
        /// no pre-release part and therefore looks like a stable release. Left alone, such a user is never offered
        /// a candidate, which is the opposite of what their situation deserves, so the plugin's own version stands
        /// in instead.
        /// </remarks>
        public static string ComparableInstalled(string installedVersion, string pluginVersion)
        {
            var readable = !string.IsNullOrWhiteSpace(installedVersion) && installedVersion != Versioning.UnknownVersion;
            if (!readable) return string.IsNullOrWhiteSpace(pluginVersion) ? null : pluginVersion;
            if (string.IsNullOrWhiteSpace(pluginVersion)) return installedVersion;
            return Versioning.VersionCompare(pluginVersion, installedVersion) < 0 ? pluginVersion : installedVersion;
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
            return releases
                .Where(r => MayBeOffered(r, installedVersion))
                .Where(r => Versioning.VersionCompare(r.Version, installedVersion) > 0)
                .OrderByDescending(r => r.Version, Comparer<string>.Create(Versioning.VersionCompare))
                .FirstOrDefault();
        }

        /// <summary>Whether a release is of a kind this user may be offered at all, whatever its version.</summary>
        public static bool MayBeOffered(ReleaseInfo release, string installedVersion)
        {
            if (release == null || release.Draft) return false;
            return !release.PreRelease || IsPreRelease(installedVersion);
        }

        /// <summary>
        /// Whether the pages read so far settle the question, or one more has to be asked for.
        /// </summary>
        /// <remarks>
        /// The kind is what a page can hide, rather than the version: a page holding nothing but candidates says
        /// nothing whatsoever to a user on a stable version, whereas one holding a single stable release answers
        /// them either way, since the listing is newest first and no release below that one was cut later. A user
        /// already on a candidate is answered by any release at all, so for them the first page always settles it.
        /// What remains unsettled by this, and was equally unsettled before pages were read, is a stable release
        /// cut after a higher-numbered one, which the listing orders by date and not by version.
        /// </remarks>
        public static bool Settles(IEnumerable<ReleaseInfo> releases, string installedVersion) =>
            releases != null && releases.Any(r => MayBeOffered(r, installedVersion));

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
