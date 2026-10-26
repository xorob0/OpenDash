// NewerSettingsTests.cs: a settings file a newer plugin wrote, read by an older one after a rollback (#864). The
// stamp says who wrote it; a newer stamp sets the file aside as .newer.json before anything is saved and holds
// OpenDash's own saves; and a screen of a kind this plugin does not know keeps its kind and owns nothing, rather
// than becoming a face that a save would write back over the newer plugin's screen.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public sealed class NewerSettingsTests : IDisposable
    {
        private readonly string folder;
        private readonly string settings;

        /// <summary>What a newer plugin might write: a stamp ahead of this one, a screen of a kind this one has
        /// never heard of carrying fields it never declared, and a face beside it.</summary>
        private const string NewerFile = @"{
  ""SettingsVersion"": ""99.0.0"",
  ""SomethingNew"": { ""Deep"": [1, 2, 3] },
  ""Rig"": [
    { ""Kind"": ""dial"", ""Namespace"": ""Dial800x800"", ""Name"": ""Gauge"", ""Width"": 800, ""Height"": 800, ""Needle"": ""red"" },
    { ""Kind"": ""face"", ""Namespace"": ""Face1280x480"", ""Name"": ""Rim"", ""Width"": 1280, ""Height"": 480 }
  ]
}";

        public NewerSettingsTests()
        {
            folder = Path.Combine(Path.GetTempPath(), "opendash-newer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            settings = Path.Combine(folder, "OpenDash.GeneralSettings.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        [Theory]
        [InlineData("1.3.0", "1.2.0", true)]
        [InlineData("1.2.1", "1.2.0", true)]
        [InlineData("1.2.0", "1.2.0-rc.1", true)]
        [InlineData("1.2.0", "1.2.0", false)]
        [InlineData("1.1.9", "1.2.0", false)]
        [InlineData(null, "1.2.0", false)]
        [InlineData("", "1.2.0", false)]
        [InlineData("1.3.0", null, false)]
        public void Only_a_stamp_strictly_newer_than_the_plugin_is_newer(string savedBy, string running, bool newer)
        {
            Assert.Equal(newer, SettingsRescue.IsNewer(savedBy, running));
        }

        [Fact]
        public void The_stamp_is_read_from_the_file_and_kept_through_Normalise_and_the_copy()
        {
            var read = JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile);
            Assert.Equal("99.0.0", read.SettingsVersion);
            read.Normalise();
            Assert.Equal("99.0.0", read.SettingsVersion);
            Assert.Equal("99.0.0", read.NormalisedCopy().SettingsVersion);
            // A file written before the stamp existed has none, and reads as older than every plugin.
            Assert.Null(JsonConvert.DeserializeObject<OpenDashSettings>("{}").SettingsVersion);
        }

        [Fact]
        public void A_newer_file_is_set_aside_beside_itself_and_holds_the_saves()
        {
            File.WriteAllText(settings, NewerFile);
            var log = new ListLog();
            var rescue = SettingsRescue.InspectNewer(settings, "99.0.0", "1.2.0", log);
            Assert.True(rescue.Newer);
            Assert.True(rescue.HoldsSaves);
            Assert.False(rescue.Unreadable);
            Assert.Equal("99.0.0", rescue.SavedBy);
            Assert.Equal(Path.Combine(folder, "OpenDash.GeneralSettings.newer.json"), rescue.CopyPath);
            Assert.Equal(NewerFile, File.ReadAllText(rescue.CopyPath));
            // The file itself is not touched.
            Assert.Equal(NewerFile, File.ReadAllText(settings));
            Assert.Contains(log.Lines, l => l.StartsWith("warn: ") && l.Contains("99.0.0") && l.Contains(rescue.CopyPath));
        }

        [Fact]
        public void A_copy_already_there_of_another_file_is_never_overwritten_and_the_saves_are_held_all_the_same()
        {
            var copy = SettingsRescue.NewerPathFor(settings);
            File.WriteAllText(copy, "{ \"SettingsVersion\": \"98.0.0\" }");
            File.WriteAllText(settings, NewerFile);
            var rescue = SettingsRescue.InspectNewer(settings, "99.0.0", "1.2.0", new ListLog());
            Assert.True(rescue.HoldsSaves);
            Assert.Null(rescue.CopyPath);
            Assert.Equal("{ \"SettingsVersion\": \"98.0.0\" }", File.ReadAllText(copy));

            // The same file again is already kept, and says so.
            File.WriteAllText(copy, NewerFile);
            Assert.Equal(copy, SettingsRescue.InspectNewer(settings, "99.0.0", "1.2.0", new ListLog()).CopyPath);
        }

        [Fact]
        public void A_file_of_this_version_or_older_says_nothing_and_copies_nothing()
        {
            File.WriteAllText(settings, NewerFile);
            Assert.Same(SettingsRescue.Read, SettingsRescue.InspectNewer(settings, "1.2.0", "1.2.0", new ListLog()));
            Assert.Same(SettingsRescue.Read, SettingsRescue.InspectNewer(settings, null, "1.2.0", new ListLog()));
            Assert.False(File.Exists(SettingsRescue.NewerPathFor(settings)));
            Assert.False(SettingsRescue.Read.HoldsSaves);
        }

        [Fact]
        public void A_newer_file_that_cannot_be_named_still_holds_the_saves()
        {
            var rescue = SettingsRescue.InspectNewer(null, "99.0.0", "1.2.0", null);
            Assert.True(rescue.HoldsSaves);
            Assert.Null(rescue.CopyPath);
        }

        [Fact]
        public void A_screen_of_a_kind_this_plugin_does_not_know_keeps_its_kind_and_owns_nothing()
        {
            var read = JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile);
            read.Normalise();
            var dial = read.RigScreens().Single(s => s.Namespace == "Dial800x800");
            Assert.Equal("dial", dial.Kind);
            Assert.False(dial.IsKnownKind);
            Assert.False(dial.IsFace);
            Assert.Null(dial.Face);
            Assert.Null(dial.PitWallZones);
            Assert.Null(dial.Modules);
            Assert.Empty(dial.PropertyNames());
            Assert.Empty(dial.ActionNames());
            Assert.DoesNotContain(dial, read.FaceScreens());
            Assert.Equal(PanelScreens.UnknownKind, PanelScreens.KindOf(dial));

            // And it goes back out as the kind it came in as, so a save does not turn it into a face.
            var back = JsonConvert.DeserializeObject<OpenDashSettings>(JsonConvert.SerializeObject(read));
            back.Normalise();
            Assert.Equal("dial", back.RigScreens().Single(s => s.Namespace == "Dial800x800").Kind);

            // The face beside it is a face as before.
            var rim = read.RigScreens().Single(s => s.Namespace == "Face1280x480");
            Assert.True(rim.IsFace);
            Assert.NotNull(rim.Face);
        }

        [Fact]
        public void A_screen_with_no_kind_is_still_a_face()
        {
            var screen = new ScreenInstance { Width = 1280, Height = 480 };
            screen.Normalise();
            Assert.True(screen.IsFace);
            Assert.NotNull(screen.Face);
        }

        [Fact]
        public void Home_says_a_newer_plugin_saved_the_settings_and_where_the_file_is_kept()
        {
            const string copy = @"C:\SimHub\PluginsData\Common\OpenDash.GeneralSettings.newer.json";
            const string file = @"C:\SimHub\PluginsData\Common\OpenDash.GeneralSettings.json";
            var input = new AttentionInput { SettingsNewer = true, SettingsSavedBy = "1.3.0", SettingsCopy = copy, SettingsFile = file, RestartPending = true };
            var issues = PanelAttention.Find(input);
            var issue = issues.First();
            Assert.Equal(PanelAttention.SettingsNewer, issue.Id);
            Assert.Equal("A newer OpenDash saved these settings", issue.Title);
            Assert.Equal("OpenDash 1.3.0 saved them. What this version does not know is left out here. The file is kept as " + copy + ".", issue.Detail);
            Assert.Equal(new[] { "Close SimHub" }, issue.Steps[0]);
            Assert.Equal(new[] { "Install OpenDash 1.3.0 again, and save the kept file as OpenDash.GeneralSettings.json" }, issue.Steps[1]);
            Assert.Equal(new[] { "Start SimHub" }, issue.Steps[2]);
            Assert.Equal(PanelIssueAction.OpenFolder, issue.Action);
            Assert.Equal(PanelPage.Home, issue.Page);
            Assert.Equal(copy, issue.Subject);
            Assert.True(PanelNav.Warns(PanelPage.Home, issues));

            // No copy: the press opens on the file itself, which is left as it is until something changes.
            var bare = PanelAttention.Find(new AttentionInput { SettingsNewer = true, SettingsSavedBy = "1.3.0", SettingsFile = file }).Single();
            Assert.Equal(file, bare.Subject);
            Assert.Equal("OpenDash 1.3.0 saved them. What this version does not know is left out here. It is left as it is until something is changed here.", bare.Detail);
            Assert.Equal(new[] { "Install OpenDash 1.3.0 again" }, bare.Steps[1]);

            foreach (var text in issues.SelectMany(i => new[] { i.Title, i.Detail }.Concat(i.Steps.SelectMany(s => s))))
            {
                Assert.DoesNotContain("n't", text);
            }
            Assert.Empty(PanelAttention.Find(new AttentionInput { SettingsNewer = false, SettingsCopy = copy }));
        }
    }
}
