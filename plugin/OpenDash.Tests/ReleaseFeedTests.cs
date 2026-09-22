// ReleaseFeedTests.cs: reads the real listing that api.github.com returned for this repository, captured as
// Fixtures/github-releases.json, and pins the two answers the feature gets wrong if the parser is generous:
// an error body must not read as "no releases", and an asset must be found by the name GitHub published.
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ReleaseFeedTests
    {
        private static string Fixture()
        {
            var assembly = typeof(ReleaseFeedTests).Assembly;
            var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("github-releases.json"));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        [Fact]
        public void Reads_the_listing_this_repository_actually_returns()
        {
            Assert.True(ReleaseFeed.TryParse(Fixture(), out var releases));
            Assert.Equal(2, releases.Count);

            var newest = releases[0];
            Assert.Equal("v0.1.0-rc.2", newest.Tag);
            Assert.Equal("0.1.0-rc.2", newest.Version);
            Assert.True(newest.PreRelease);
            Assert.False(newest.Draft);
            Assert.Equal(15, newest.Assets.Count);
            Assert.Contains("What's Changed", newest.Notes);
            Assert.StartsWith("https://github.com/xorob0/OpenDash/releases/tag/", newest.Url);
        }

        [Fact]
        public void Finds_a_package_by_the_name_GitHub_published_rather_than_the_name_on_disk()
        {
            ReleaseFeed.TryParse(Fixture(), out var releases);
            var newest = releases[0];

            // Every space becomes a period on upload, which is why the lookup goes forwards only.
            var wide = newest.AssetFor("OpenDash Pit wall portrait");
            Assert.NotNull(wide);
            Assert.Equal("OpenDash.Pit.wall.portrait.simhubdash", wide.Name);

            Assert.Equal("OpenDash.480.round.simhubdash", newest.AssetFor("OpenDash 480 round").Name);
            Assert.Equal("OpenDash.simhubdash", newest.AssetFor("OpenDash").Name);

            // A package this release does not carry is absent rather than mismatched.
            Assert.Null(newest.AssetFor("OpenDash zones 1920x480"));
            Assert.Null(newest.AssetFor(null));
        }

        [Fact]
        public void Every_asset_carries_the_digest_the_download_is_checked_against()
        {
            ReleaseFeed.TryParse(Fixture(), out var releases);
            foreach (var asset in releases[0].Assets)
            {
                Assert.StartsWith("sha256:", asset.Digest);
                Assert.Equal(71, asset.Digest.Length); // "sha256:" plus 64 hex characters.
                Assert.True(asset.Size > 0);
                Assert.StartsWith("https://github.com/xorob0/OpenDash/releases/download/", asset.DownloadUrl);
            }
        }

        /// <summary>
        /// The quiet wrong answer this feature can give, and the reason TryParse reports failure on an empty list.
        /// GitHub's error body is an object where an array is expected, and the serialiser does not object to it.
        /// </summary>
        [Theory]
        [InlineData("{\"message\":\"Not Found\",\"status\":\"404\"}")]
        [InlineData("[]")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("[{\"prerelease\":true}]")] // a release with no tag is not one we can act on
        public void Refuses_a_body_it_cannot_act_on(string body)
        {
            Assert.False(ReleaseFeed.TryParse(body, out var releases));
            Assert.Empty(releases);
        }

        [Fact]
        public void A_tag_without_a_v_is_read_as_it_stands()
        {
            Assert.True(ReleaseFeed.TryParse("[{\"tag_name\":\"0.2.0\"}]", out var plain));
            Assert.Equal("0.2.0", plain[0].Version);
            Assert.Empty(plain[0].Assets);

            Assert.True(ReleaseFeed.TryParse("[{\"tag_name\":\"V0.2.0\"}]", out var upper));
            Assert.Equal("0.2.0", upper[0].Version);
        }
    }
}
