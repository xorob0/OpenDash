// UpdatePlan.cs: which packages a release can replace, and the source an installer reads them from.
//
// Applying an update goes through DashboardInstaller rather than PackageExtractor directly, so that a downloaded
// package gets the same per-package comparison, the same backup, the same font copy and the same font refresh as an
// embedded one. The only new thing is where the bytes come from, which is what DownloadedPackageSource is.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OpenDashPlugin
{
    /// <summary>One package to fetch: the folder it installs into and the asset carrying it.</summary>
    public sealed class UpdateItem
    {
        public string FolderName { get; set; }
        public ReleaseAsset Asset { get; set; }
    }

    public sealed class UpdatePlan
    {
        /// <summary>What the release carries and this machine has installed.</summary>
        public IReadOnlyList<UpdateItem> Items { get; set; } = new UpdateItem[0];

        /// <summary>Folders installed here that the release does not publish, which are left as they are.</summary>
        public IReadOnlyList<string> NotCarried { get; set; } = new string[0];

        public bool IsEmpty => Items.Count == 0;

        /// <summary>
        /// What to fetch in order to bring this machine's dashboards up to a release.
        /// </summary>
        /// <remarks>
        /// Only folders already installed here are considered. A release publishes more packages than any one user
        /// has, and adding a screen size somebody never asked for is the dashboard manager's business (#84), not
        /// an update's. The arithmetic is real rather than hypothetical: the build produces twenty-two packages and
        /// v0.1.0-rc.2 published fourteen, so both directions of mismatch already exist.
        /// </remarks>
        public static UpdatePlan For(IEnumerable<PackageStatus> installed, ReleaseInfo release)
        {
            if (release == null || installed == null) return new UpdatePlan();
            var items = new List<UpdateItem>();
            var missing = new List<string>();
            foreach (var package in installed)
            {
                var folder = package?.FolderName;
                if (string.IsNullOrEmpty(folder) || package.InstalledVersion == null) continue;
                var asset = release.AssetFor(folder);
                if (asset == null || string.IsNullOrWhiteSpace(asset.DownloadUrl)) missing.Add(folder);
                else items.Add(new UpdateItem { FolderName = folder, Asset = asset });
            }
            return new UpdatePlan { Items = items, NotCarried = missing };
        }
    }

    /// <summary>
    /// A yes to replacing edited dashboards, carried from the run that staged a plugin to the start that writes them.
    /// </summary>
    /// <remarks>
    /// Since #438 an update stages the plugin and the dashboards come inside it, so the process that asked "replace
    /// your version?" is never the one that replaces anything: the new plugin's first start writes the dashboards,
    /// and without the answer it holds every edited one back, leaving new code reading old dashboards. The answer is
    /// therefore kept as the version it was given for, and honoured only by a plugin of exactly that version, so a
    /// swap that never happened cannot spend it on the old plugin's dashboards and a later version cannot inherit it.
    /// </remarks>
    public static class EditedConsent
    {
        /// <summary>Whether the plugin starting now should replace edited dashboards.</summary>
        public static bool AppliesNow(string consentedFor, string running)
        {
            if (string.IsNullOrWhiteSpace(consentedFor) || string.IsNullOrWhiteSpace(running)) return false;
            return Versioning.VersionCompare(consentedFor, running) == 0;
        }

        /// <summary>
        /// Whether the answer is spent or stale after this start: spent when it applied, stale when no swap is still
        /// waiting to bring the version it was given for. Kept only while the old plugin runs and a swap is pending.
        /// </summary>
        public static bool Forget(string consentedFor, string running, bool swapPending)
        {
            if (string.IsNullOrWhiteSpace(consentedFor)) return false;
            return AppliesNow(consentedFor, running) || !swapPending;
        }
    }

    /// <summary>Packages held in memory, for installing what has just been downloaded.</summary>
    public sealed class DownloadedPackageSource : IPackageSource
    {
        private readonly Dictionary<string, byte[]> bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly List<string> names = new List<string>();

        public IReadOnlyList<string> Names => names;

        public DownloadedPackageSource Add(string name, byte[] package)
        {
            if (string.IsNullOrEmpty(name) || package == null) throw new ArgumentException("a downloaded package needs a name and bytes");
            if (!bytes.ContainsKey(name)) names.Add(name);
            bytes[name] = package;
            return this;
        }

        public Stream Open(string name)
        {
            if (!bytes.TryGetValue(name, out var package)) throw new FileNotFoundException("Downloaded package not found: " + name);
            return new MemoryStream(package, false);
        }
    }

    public static class Digest
    {
        /// <summary>
        /// Whether bytes are the ones the release published, given the API's "sha256:&lt;hex&gt;" form.
        /// </summary>
        /// <remarks>
        /// An asset published before GitHub reported digests carries none, and a missing digest is not a reason to
        /// refuse: what it verifies is that the bytes are the ones GitHub holds, not that they are trustworthy, and
        /// the package is read and its folder and version checked before anything under DashTemplates is touched.
        /// </remarks>
        public static bool Matches(byte[] content, string published)
        {
            if (content == null) return false;
            if (string.IsNullOrWhiteSpace(published)) return true;
            var expected = published.Trim();
            if (!expected.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return true;
            using (var sha = SHA256.Create())
            {
                var actual = "sha256:" + BitConverter.ToString(sha.ComputeHash(content)).Replace("-", string.Empty).ToLowerInvariant();
                return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
