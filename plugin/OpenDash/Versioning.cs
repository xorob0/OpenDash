// Versioning.cs: version comparison and the install decision. Pure: compiled into the tests.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenDashPlugin
{
    public static class Versioning
    {
        /// <summary>Installed version reported for a dashboard folder whose sidecar is missing, malformed or has no
        /// DashboardVersion. Decide ranks it below every embedded version, so such a folder is reinstalled. The panel
        /// shows it as it is ("openDash (unknown version)").</summary>
        public const string UnknownVersion = "(unknown version)";

        private static readonly Regex DashboardVersionPattern =
            new Regex("\"DashboardVersion\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);

        /// <summary>Semver-style comparison. The core is numeric dot-separated segments, missing segments count as 0,
        /// a leading "v" is ignored and so is build metadata after "+". A pre-release after "-" sorts below its release
        /// ("0.2.0-rc1" is older than "0.2.0"); two pre-releases compare identifier by identifier as semver does:
        /// numeric identifiers by value, others ordinally, numeric below alphanumeric, and when the shared identifiers
        /// are equal the longer list is newer ("rc" is older than "rc.1").
        /// Returns negative when a is older than b, 0 when equal, positive when newer. Null or empty is the oldest.</summary>
        public static int VersionCompare(string a, string b)
        {
            var leftEmpty = string.IsNullOrWhiteSpace(a);
            var rightEmpty = string.IsNullOrWhiteSpace(b);
            if (leftEmpty || rightEmpty) return leftEmpty == rightEmpty ? 0 : (leftEmpty ? -1 : 1);

            var left = Parse(a);
            var right = Parse(b);
            var length = Math.Max(left.Core.Count, right.Core.Count);
            for (var i = 0; i < length; i++)
            {
                var l = i < left.Core.Count ? left.Core[i] : 0;
                var r = i < right.Core.Count ? right.Core[i] : 0;
                if (l != r) return l.CompareTo(r);
            }
            return ComparePreRelease(left.PreRelease, right.PreRelease);
        }

        private sealed class ParsedVersion
        {
            public readonly List<int> Core = new List<int>();

            /// <summary>The identifiers after "-"; null for a release.</summary>
            public string[] PreRelease;
        }

        private static ParsedVersion Parse(string version)
        {
            var result = new ParsedVersion();
            var text = version.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);

            var plus = text.IndexOf('+');
            if (plus >= 0) text = text.Substring(0, plus); // build metadata carries no precedence

            var dash = text.IndexOf('-');
            if (dash >= 0)
            {
                var preRelease = text.Substring(dash + 1);
                if (preRelease.Length > 0) result.PreRelease = preRelease.Split('.');
                text = text.Substring(0, dash);
            }

            foreach (var part in text.Split('.'))
            {
                var digits = 0;
                while (digits < part.Length && IsAsciiDigit(part[digits])) digits++;
                int value;
                result.Core.Add(digits > 0 && int.TryParse(part.Substring(0, digits), out value) ? value : 0);
                if (digits < part.Length) break; // "0rc1": stop at the first segment that is not a number
            }
            return result;
        }

        private static int ComparePreRelease(string[] a, string[] b)
        {
            if (a == null) return b == null ? 0 : 1; // a release outranks its pre-releases
            if (b == null) return -1;
            var shared = Math.Min(a.Length, b.Length);
            for (var i = 0; i < shared; i++)
            {
                var order = CompareIdentifier(a[i], b[i]);
                if (order != 0) return order;
            }
            return a.Length.CompareTo(b.Length);
        }

        private static int CompareIdentifier(string a, string b)
        {
            var leftNumeric = IsNumeric(a);
            var rightNumeric = IsNumeric(b);
            if (leftNumeric && rightNumeric) return CompareNumeric(a, b);
            if (leftNumeric != rightNumeric) return leftNumeric ? -1 : 1; // numeric identifiers rank below alphanumeric ones
            return string.CompareOrdinal(a, b);
        }

        private static bool IsNumeric(string identifier)
        {
            if (identifier.Length == 0) return false;
            foreach (var c in identifier)
            {
                if (!IsAsciiDigit(c)) return false;
            }
            return true;
        }

        /// <summary>Compares two all-digit strings by value without overflowing: leading zeros off, then length, then ordinal.</summary>
        private static int CompareNumeric(string a, string b)
        {
            a = a.TrimStart('0');
            b = b.TrimStart('0');
            return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
        }

        private static bool IsAsciiDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>Reads DashboardVersion out of a .djson.metadata sidecar. Null when absent.
        /// A regex rather than a JSON parser, so that the tests need no dependency; the sidecar is written by
        /// our own generator or by SimHub, both of which emit "DashboardVersion": "x.y.z". Never throws, so a
        /// malformed sidecar reads as "no version".</summary>
        public static string ParseDashboardVersion(string metadataJson)
        {
            if (string.IsNullOrEmpty(metadataJson)) return null;
            var match = DashboardVersionPattern.Match(metadataJson);
            if (!match.Success) return null;
            var value = match.Groups[1].Value.Trim();
            return value.Length == 0 ? null : value;
        }

        /// <summary>What the installer should report, and do, for one package. An installed copy without a readable
        /// version (UnknownVersion) is older than anything embedded.</summary>
        public static InstallStatus Decide(string installedVersion, string embeddedVersion)
        {
            if (installedVersion == null) return InstallStatus.NotInstalled;
            if (embeddedVersion == null) return InstallStatus.UpToDate; // nothing embedded to compare with
            if (installedVersion == UnknownVersion) return InstallStatus.UpdateAvailable;
            return VersionCompare(embeddedVersion, installedVersion) > 0 ? InstallStatus.UpdateAvailable : InstallStatus.UpToDate;
        }

        public static bool NeedsInstall(InstallStatus status)
        {
            return status == InstallStatus.NotInstalled || status == InstallStatus.UpdateAvailable;
        }
    }
}
