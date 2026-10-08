// CarCrest.cs: the Porsche crest, fetched once into the user's own SimHub folder and never shipped (#714).
//
// The crest is a registered trade mark and no package, no release and no line of this repository carries it
// (#194, the trade dress paragraph of docs/scope.md). What the plugin carries is an address and the SHA-256 of
// the file expected there. On a rig with a Porsche screen it downloads that file once into
// SimHub/OpenDash/Crests/, beside the car light tables, checks it, and publishes its path as
// OpenDash.PorscheCrest; the Porsche foot's badge draws the file with SimHub's ImageFromFileItem. The copy on
// disk is the user's own, as the tables are.
//
// **Why a file and not SimHub's own ImageFromUrlItem.** The spike on the VM (2026-10-08, recorded in
// docs/research/simhub-dash-format.md) found that the URL item fetches again every ImageRefreshIntervalSeconds,
// sixty by default, for as long as the dash is open, with a bare WebClient that sends no User-Agent, which
// Wikimedia refuses with a 403. A file is one request per rig, works offline afterwards, and lets the panel say
// whether the crest is there.
//
// **What leaves the machine.** One request to the address's host, carrying the User-Agent ReleaseClient puts on
// every request and the IP address, on a start or a panel change that finds the crest missing, and only on a
// rig with a Porsche screen at a size that draws the badge. Clearing the address on the panel stops it entirely
// and removes the copy. Nothing about the car, the session or the rig is in it.
//
// No SimHub or WPF types: compiled into the tests. Nothing here throws, since a crest that is not there is the
// empty shield the badge drew before this file existed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenDashPlugin
{
    /// <summary>What a fetch did, so that the panel can say it and the tests can assert it.</summary>
    public sealed class CarCrestFetch
    {
        public bool Ok { get; set; }

        /// <summary>Why it did not work, for the log and the panel. Null when it did.</summary>
        public string Reason { get; set; }

        public static CarCrestFetch Failed(string reason)
        {
            return new CarCrestFetch { Ok = false, Reason = reason };
        }
    }

    public static class CarCrestLibrary
    {
        /// <summary>
        /// Where the crest is fetched from unless the driver says otherwise: the English Wikipedia's copy of the
        /// Porsche crest, 274 by 364, a PNG with a transparent ground.
        /// </summary>
        /// <remarks>
        /// The original upload rather than one of its thumbnails, because a thumbnail is regenerated on
        /// Wikimedia's schedule and a regenerated one would fail the hash; SimHub fits the 274 px original into
        /// the 54 by 62 place as cleanly as the 120 and 60 px thumbnails, which the spike compared side by side.
        /// Wikipedia files it as non-free, used there under fair use: its terms grant nothing to anyone else,
        /// which is why the fetch is the driver's own and why scope.md records drawing the mark as a use of it,
        /// accepted knowingly. A default the author may replace.
        /// </remarks>
        public const string DefaultUrl = "https://upload.wikimedia.org/wikipedia/en/c/c2/Porsche_Logo_2024.png";

        /// <summary>
        /// The SHA-256 of the file at <see cref="DefaultUrl"/>, as fetched on 2026-10-08. A different file at that
        /// address -- a new upload, or somebody else's -- is refused rather than drawn.
        /// </summary>
        public const string DefaultSha256 = "584a9f9fac93454a224479c453aae369c2688f950b51c4eb79b249ecec121b94";

        /// <summary>The theme that draws this crest (Contract.Themes).</summary>
        public const string ThemeId = "porsche";

        /// <summary>
        /// The sizes at which the Porsche keeps its settings column, and so the badge (#205): the 8:3 drawing
        /// and its shorter, taller and wider forms. 850 x 480, 800 x 480 and 800 x 286 shed the badge and the
        /// portrait face has none, so a rig whose Porsche screens are only those fetches nothing.
        /// </summary>
        public static readonly IReadOnlyList<string> BadgeSizes = new[] { "1280x480", "1280x400", "1280x720", "1920x480" };

        /// <summary>Under SimHub/OpenDash/, beside the car light tables. Not PluginsData: that is SimHub's.</summary>
        public const string FolderName = "Crests";

        /// <summary>The crest's file. A PNG by the default; the decoder SimHub uses reads what the bytes are.</summary>
        public const string FileName = "porsche.png";

        /// <summary>Beside the file: the address it came from, so that a changed address is a missing crest.</summary>
        public const string SourceFile = "porsche.source.txt";

        /// <summary>A crest is tens of kilobytes. Anything past this is not one, and is not written.</summary>
        public const int MaxBytes = 4 * 1024 * 1024;

        public static string FolderPath(string simHubRoot)
        {
            return Path.Combine(simHubRoot ?? string.Empty, FlagBoxProfile.FolderName, FolderName);
        }

        /// <summary>
        /// The address as the setting keeps it: trimmed, and empty for anything that is not an absolute http or
        /// https address, which is how the driver says no crest.
        /// </summary>
        public static string NormaliseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            var trimmed = url.Trim();
            Uri parsed;
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out parsed)) return string.Empty;
            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return string.Empty;
            return trimmed;
        }

        /// <summary>The hash a file from <paramref name="url"/> must have, or null for an address the driver chose,
        /// whose file is theirs to vouch for.</summary>
        public static string ExpectedSha256(string url)
        {
            return string.Equals(NormaliseUrl(url), DefaultUrl, StringComparison.Ordinal) ? DefaultSha256 : null;
        }

        /// <summary>Whether a screen of this theme and size draws the badge.</summary>
        public static bool DrawsBadge(string theme, int width, int height)
        {
            if (!string.Equals(theme, ThemeId, StringComparison.Ordinal)) return false;
            return BadgeSizes.Contains(width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" + height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Whether any screen on the rig draws the badge, which is the only case in which anything is fetched.</summary>
        public static bool Wanted(IEnumerable<ScreenInstance> screens)
        {
            return (screens ?? Enumerable.Empty<ScreenInstance>()).Any(s => s != null && DrawsBadge(s.Theme, s.Width, s.Height));
        }

        public static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes ?? new byte[0]);
                var text = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) text.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        /// <summary>
        /// Whether the bytes begin as a PNG, a JPEG, a GIF or a BMP does: the formats WPF's decoder reads, and so
        /// the ones SimHub can draw. A web page an address answered with instead is refused here rather than
        /// drawn as nothing.
        /// </summary>
        public static bool LooksLikeImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return false;
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return true;
            if (bytes[0] == 0x42 && bytes[1] == 0x4D) return true;
            return false;
        }

        /// <summary>
        /// The path the dash should draw: the file, when it is there and came from <paramref name="url"/>, and
        /// "" otherwise. Read from the disk, so a copy fetched on an earlier start is drawn with no network.
        /// </summary>
        public static string OnDisk(string folder, string url)
        {
            var wanted = NormaliseUrl(url);
            if (wanted.Length == 0 || string.IsNullOrWhiteSpace(folder)) return string.Empty;
            try
            {
                var file = Path.Combine(folder, FileName);
                var source = Path.Combine(folder, SourceFile);
                if (!File.Exists(file) || !File.Exists(source)) return string.Empty;
                if (!string.Equals(File.ReadAllText(source).Trim(), wanted, StringComparison.Ordinal)) return string.Empty;
                return Path.GetFullPath(file);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Removes the copy, which is what clearing the address asks for. Says whether anything is left.</summary>
        public static bool Remove(string folder)
        {
            try
            {
                foreach (var name in new[] { FileName, SourceFile })
                {
                    var path = Path.Combine(folder, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Fetches the crest and writes it, synchronously. The whole decision is here so that it is testable;
        /// <see cref="CarCrestService"/> puts it on a thread.
        /// </summary>
        /// <remarks>
        /// The file is written beside its final name and moved over it, so the dash never reads half a crest;
        /// SimHub reloads an ImageFromFileItem within a second of its file's write time changing. A refusal
        /// leaves whatever was on disk alone.
        /// </remarks>
        public static CarCrestFetch Fetch(IReleaseSource source, string url, string folder, CancellationToken cancel = default(CancellationToken))
        {
            var wanted = NormaliseUrl(url);
            if (wanted.Length == 0) return CarCrestFetch.Failed("no address");
            if (source == null) return CarCrestFetch.Failed("no source");
            FetchResult result;
            try
            {
                result = source.GetBytes(wanted, null, cancel);
            }
            catch (Exception e)
            {
                return CarCrestFetch.Failed(e.Message);
            }
            if (result == null || !result.Ok || result.Bytes == null) return CarCrestFetch.Failed(result?.Reason ?? "no answer");
            var bytes = result.Bytes;
            if (bytes.Length > MaxBytes) return CarCrestFetch.Failed("the file is too large to be a crest");
            if (!LooksLikeImage(bytes)) return CarCrestFetch.Failed("the address did not answer with an image");
            var expected = ExpectedSha256(wanted);
            if (expected != null && !string.Equals(Sha256(bytes), expected, StringComparison.OrdinalIgnoreCase))
            {
                return CarCrestFetch.Failed("the file at the address is not the one expected");
            }
            try
            {
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, FileName);
                var part = file + ".part";
                File.WriteAllBytes(part, bytes);
                if (File.Exists(file)) File.Delete(file);
                File.Move(part, file);
                File.WriteAllText(Path.Combine(folder, SourceFile), wanted);
            }
            catch (Exception e)
            {
                return CarCrestFetch.Failed(e.Message);
            }
            return new CarCrestFetch { Ok = true };
        }
    }

    /// <summary>What the panel says about the crest.</summary>
    public enum CarCrestState
    {
        /// <summary>Before the start's look at the disk has finished.</summary>
        Loading,
        /// <summary>The address is empty: the driver asked for no crest.</summary>
        Cleared,
        /// <summary>No screen on the rig draws the badge, so nothing was fetched.</summary>
        NotNeeded,
        Fetching,
        Present,
        Failed,
    }

    /// <summary>The crest on disk, the path the dash reads, and the one fetch. Nothing here throws.</summary>
    public sealed class CarCrestService
    {
        private readonly IReleaseSource source;
        private readonly string folder;
        private readonly object fetchLock = new object();

        private volatile string path = string.Empty;
        private volatile string reason;
        private volatile CarCrestState state = CarCrestState.Loading;

        public CarCrestService(IReleaseSource source, string folder)
        {
            this.source = source;
            this.folder = folder;
        }

        /// <summary>What OpenDash.PorscheCrest publishes: the file's full path, or "".</summary>
        public string Path
        {
            get { return path; }
        }

        public CarCrestState State
        {
            get { return state; }
        }

        /// <summary>Why the last fetch failed, for the panel and the log. Null unless <see cref="State"/> is Failed.</summary>
        public string Reason
        {
            get { return reason; }
        }

        /// <summary>
        /// Makes the disk and the property agree with the address, fetching only when a screen draws the badge
        /// and the crest from that address is not already there. Synchronous and returning the state, so that
        /// every branch is tested; <see cref="RefreshInBackground"/> is what the plugin calls.
        /// </summary>
        public CarCrestState Refresh(string url, bool wanted, CancellationToken cancel = default(CancellationToken))
        {
            lock (fetchLock)
            {
                var address = CarCrestLibrary.NormaliseUrl(url);
                reason = null;
                if (address.Length == 0)
                {
                    CarCrestLibrary.Remove(folder);
                    path = string.Empty;
                    return state = CarCrestState.Cleared;
                }
                var present = CarCrestLibrary.OnDisk(folder, address);
                if (present.Length > 0)
                {
                    path = present;
                    return state = CarCrestState.Present;
                }
                path = string.Empty;
                if (!wanted) return state = CarCrestState.NotNeeded;
                state = CarCrestState.Fetching;
                var fetch = CarCrestLibrary.Fetch(source, address, folder, cancel);
                if (!fetch.Ok)
                {
                    reason = fetch.Reason;
                    return state = CarCrestState.Failed;
                }
                path = CarCrestLibrary.OnDisk(folder, address);
                if (path.Length == 0)
                {
                    reason = "the file was written and could not be read back";
                    return state = CarCrestState.Failed;
                }
                return state = CarCrestState.Present;
            }
        }

        /// <summary>
        /// The same, off the calling thread. Swallows everything: an unobserved exception on the thread pool ends
        /// the SimHub process on .NET Framework. <paramref name="done"/> runs on that thread afterwards.
        /// </summary>
        public void RefreshInBackground(string url, bool wanted, Action<CarCrestState, string> done = null)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                CarCrestState result;
                try
                {
                    result = Refresh(url, wanted);
                }
                catch (Exception e)
                {
                    reason = e.Message;
                    path = string.Empty;
                    state = CarCrestState.Failed;
                    result = CarCrestState.Failed;
                }
                try
                {
                    if (done != null) done(result, reason);
                }
                catch (Exception)
                {
                    // A log line that could not be written is no reason to end SimHub.
                }
            });
        }
    }
}
