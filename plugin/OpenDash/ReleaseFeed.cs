// ReleaseFeed.cs: what a GitHub releases listing says, and how it is read.
//
// The reading is here rather than behind the HTTP adapter so that it is unit tested. DataContractJsonSerializer
// is in the framework on both net48 and net8.0 and needs no package in either project, which is the whole reason
// it is used in preference to Newtonsoft: Newtonsoft is available to the plugin but not to the test project, and
// a parser nothing exercises is where a release feed's next surprise lands.
//
// See docs/decisions/0012-update-checks.md for what may be fetched and why the list endpoint is used rather than
// releases/latest.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>One file published with a release.</summary>
    public sealed class ReleaseAsset
    {
        /// <summary>The name GitHub published, which is not the name on disk: every space becomes a period, so
        /// "openDash Pit wall.simhubdash" is published as "openDash.Pit.wall.simhubdash".</summary>
        public string Name { get; set; }

        /// <summary>Answers 302 to a signed URL on another host that expires within the hour, so it is followed
        /// promptly after the listing that named it rather than kept between runs.</summary>
        public string DownloadUrl { get; set; }

        public long Size { get; set; }

        /// <summary>"sha256:&lt;hex&gt;" as the API publishes it, or null on a release cut before GitHub reported one.</summary>
        public string Digest { get; set; }
    }

    /// <summary>One release, reduced to what openDash reads.</summary>
    public sealed class ReleaseInfo
    {
        public string Tag { get; set; }
        public bool PreRelease { get; set; }
        public bool Draft { get; set; }

        /// <summary>The release body, which is GitHub's generated summary rather than CHANGELOG.md.</summary>
        public string Notes { get; set; }

        public string Url { get; set; }
        public IReadOnlyList<ReleaseAsset> Assets { get; set; } = new ReleaseAsset[0];

        /// <summary>The tag without its leading "v", which is the form VERSION and every sidecar take.</summary>
        public string Version
        {
            get
            {
                var tag = Tag ?? string.Empty;
                return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;
            }
        }

        /// <summary>The asset carrying a package folder, or null when this release does not publish it.</summary>
        /// <remarks>
        /// Forward only. GitHub's rewrite maps a folder to exactly one asset name, whereas undoing it is ambiguous,
        /// so a folder is turned into the name it would have been published under and looked up. Nothing tries to
        /// read a folder out of an asset name.
        /// </remarks>
        public ReleaseAsset AssetFor(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return null;
            var published = folderName.Replace(' ', '.') + ReleaseFeed.PackageSuffix;
            return Assets.FirstOrDefault(a => string.Equals(a.Name, published, StringComparison.Ordinal));
        }
    }

    public static class ReleaseFeed
    {
        public const string PackageSuffix = ".simhubdash";

        /// <summary>
        /// Reads a releases listing. False when the body is not a listing openDash can act on, which includes an
        /// empty array and includes GitHub's own error shape.
        /// </summary>
        /// <remarks>
        /// The empty case is the one that matters and it is measured, not assumed: DataContractJsonSerializer turns
        /// `{"message":"Not Found"}` into an empty array without throwing, so a caller that treated "parsed, zero
        /// releases" as success would tell a user they are up to date on the strength of an error it could not read.
        /// That is the single quiet wrong answer this feature can give, therefore nothing here reports success
        /// without at least one release carrying a tag.
        /// </remarks>
        public static bool TryParse(string json, out IReadOnlyList<ReleaseInfo> releases)
        {
            releases = new ReleaseInfo[0];
            if (string.IsNullOrWhiteSpace(json)) return false;
            ReleaseDto[] parsed;
            try
            {
                var serialiser = new DataContractJsonSerializer(typeof(ReleaseDto[]));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    parsed = serialiser.ReadObject(stream) as ReleaseDto[];
                }
            }
            catch (Exception)
            {
                return false;
            }
            if (parsed == null) return false;

            var read = parsed
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.TagName))
                .Select(r => new ReleaseInfo
                {
                    Tag = r.TagName.Trim(),
                    PreRelease = r.PreRelease,
                    Draft = r.Draft,
                    Notes = r.Body,
                    Url = r.HtmlUrl,
                    Assets = (r.Assets ?? new AssetDto[0])
                        .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name))
                        .Select(a => new ReleaseAsset { Name = a.Name, DownloadUrl = a.DownloadUrl, Size = a.Size, Digest = a.Digest })
                        .ToArray(),
                })
                .ToArray();

            if (read.Length == 0) return false;
            releases = read;
            return true;
        }

        [DataContract]
        private sealed class ReleaseDto
        {
            [DataMember(Name = "tag_name")] public string TagName { get; set; }
            [DataMember(Name = "prerelease")] public bool PreRelease { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "body")] public string Body { get; set; }
            [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
            [DataMember(Name = "assets")] public AssetDto[] Assets { get; set; }
        }

        [DataContract]
        private sealed class AssetDto
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "browser_download_url")] public string DownloadUrl { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }
    }
}
