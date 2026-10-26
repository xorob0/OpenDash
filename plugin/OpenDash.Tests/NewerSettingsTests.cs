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

        [Fact]
        public void Home_names_Backups_once_a_newer_file_with_no_copy_has_been_saved_over()
        {
            const string file = @"C:\SimHub\PluginsData\Common\OpenDash.GeneralSettings.json";
            const string backups = @"C:\SimHub\PluginsData\Common\_Backups";
            var issue = PanelAttention.Find(new AttentionInput
            {
                SettingsNewer = true, SettingsSavedBy = "1.3.0", SettingsFile = file, SettingsBackups = backups, SettingsSavedOver = true,
            }).Single();
            Assert.Equal(backups, issue.Subject);
            Assert.Equal("OpenDash 1.3.0 saved them. What this version does not know is left out here. Something was changed here, so SimHub keeps the newer file in " + backups + ".", issue.Detail);
            Assert.Equal(new[] { "Install OpenDash 1.3.0 again, take the file it saved from _Backups, and save it as OpenDash.GeneralSettings.json" }, issue.Steps[1]);
            foreach (var text in new[] { issue.Detail }.Concat(issue.Steps.SelectMany(s => s))) Assert.DoesNotContain("n't", text);

            // With a copy, the copy still holds the newer rig, saved over or not.
            const string copy = @"C:\SimHub\PluginsData\Common\OpenDash.GeneralSettings.newer.json";
            var kept = PanelAttention.Find(new AttentionInput
            {
                SettingsNewer = true, SettingsSavedBy = "1.3.0", SettingsCopy = copy, SettingsFile = file, SettingsBackups = backups, SettingsSavedOver = true,
            }).Single();
            Assert.Equal(copy, kept.Subject);
            Assert.EndsWith("The file is kept as " + copy + ".", kept.Detail);
        }

        [Fact]
        public void A_screen_of_a_kind_this_plugin_does_not_know_lists_no_properties_in_its_details()
        {
            Assert.Equal(PanelScreens.PropertiesNone, PanelScreens.Properties(new ScreenInstance { Kind = "dial", Namespace = "Dial800x800" }));
        }

        // --- SettingsHold: when OpenDash saves, and what it stamps -----------------------------------------------

        [Fact]
        public void A_start_on_a_newer_file_reads_the_stamp_sets_the_file_aside_holds_the_saves_and_then_normalises()
        {
            File.WriteAllText(settings, NewerFile);
            var read = JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile);
            var hold = SettingsHold.Begin(read, false, () => settings, "1.2.0", new ListLog());
            Assert.True(hold.Holding);
            Assert.False(hold.MaySaveOnOwn);
            Assert.False(hold.SavedOver);
            Assert.True(hold.Rescue.Newer);
            Assert.Equal("99.0.0", hold.Rescue.SavedBy);
            Assert.Equal(SettingsRescue.NewerPathFor(settings), hold.Rescue.CopyPath);
            Assert.Equal(NewerFile, File.ReadAllText(hold.Rescue.CopyPath));
            // Normalised once the stamp was read: the face has its per-kind state, the newer kind kept its own.
            Assert.NotNull(read.RigScreens().Single(s => s.Namespace == "Face1280x480").Face);
            Assert.Equal("dial", read.RigScreens().Single(s => s.Namespace == "Dial800x800").Kind);
            Assert.Equal("99.0.0", read.SettingsVersion);
            Assert.Contains("99.0.0", hold.SkippedLine());
        }

        [Fact]
        public void A_start_on_a_file_of_this_version_or_none_saves_on_its_own_as_before()
        {
            File.WriteAllText(settings, "{}");
            foreach (var stamp in new[] { null, "1.2.0", "1.1.0" })
            {
                var read = new OpenDashSettings { SettingsVersion = stamp };
                var hold = SettingsHold.Begin(read, false, () => settings, "1.2.0", new ListLog());
                Assert.False(hold.Holding);
                Assert.True(hold.MaySaveOnOwn);
                Assert.Same(SettingsRescue.Read, hold.Rescue);
            }
            Assert.False(File.Exists(SettingsRescue.NewerPathFor(settings)));
            Assert.True(SettingsHold.None().MaySaveOnOwn);
        }

        [Fact]
        public void A_start_SimHub_handed_defaults_looks_for_an_unreadable_file_and_holds_nothing()
        {
            File.WriteAllText(settings, "{ not json");
            var hold = SettingsHold.Begin(new OpenDashSettings(), true, () => settings, "1.2.0", new ListLog());
            Assert.True(hold.Rescue.Unreadable);
            Assert.False(hold.Holding);
        }

        [Fact]
        public void A_start_that_cannot_name_the_file_logs_it_holds_nothing_and_still_normalises()
        {
            var log = new ListLog();
            var read = JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile);
            var hold = SettingsHold.Begin(read, false, () => { throw new InvalidOperationException("no manager"); }, "1.2.0", log);
            Assert.Same(SettingsRescue.Read, hold.Rescue);
            Assert.False(hold.Holding);
            Assert.Contains(log.Lines, l => l.StartsWith("error: ") && l.Contains("no manager"));
            Assert.NotNull(read.RigScreens().Single(s => s.Namespace == "Face1280x480").Face);
        }

        [Fact]
        public void A_save_somebody_caused_stamps_the_file_and_ends_the_hold()
        {
            File.WriteAllText(settings, NewerFile);
            var live = JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile);
            var hold = SettingsHold.Begin(live, false, () => settings, "1.2.0", new ListLog());
            Assert.False(hold.MaySaveOnOwn);

            // As SaveRigPress does: the live settings are stamped, and the copy made in the write carries it.
            OpenDashSettings written = null;
            hold.Save(live, "1.2.0", () => written = live.NormalisedCopy());
            Assert.Equal("1.2.0", live.SettingsVersion);
            Assert.Equal("1.2.0", written.SettingsVersion);
            Assert.False(hold.Holding);
            Assert.True(hold.MaySaveOnOwn);
            Assert.True(hold.SavedOver);

            // And it goes out as this version, which the next start reads as nothing out of the ordinary.
            var next = JsonConvert.DeserializeObject<OpenDashSettings>(JsonConvert.SerializeObject(written));
            Assert.False(SettingsHold.Begin(next, false, () => settings, "1.2.0", new ListLog()).Holding);
        }

        [Fact]
        public void A_save_that_fails_keeps_the_hold()
        {
            File.WriteAllText(settings, NewerFile);
            var live = JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile);
            var hold = SettingsHold.Begin(live, false, () => settings, "1.2.0", new ListLog());
            Assert.Throws<IOException>(() => hold.Save(live, "1.2.0", () => { throw new IOException("locked"); }));
            Assert.True(hold.Holding);
            Assert.False(hold.SavedOver);
        }

        [Fact]
        public void A_save_of_a_file_this_version_wrote_stamps_it_and_saves_over_no_newer_rig()
        {
            var live = new OpenDashSettings();
            var hold = SettingsHold.Begin(live, false, () => settings, "1.2.0", new ListLog());
            hold.Save(live, "1.2.0", () => { });
            Assert.Equal("1.2.0", live.SettingsVersion);
            Assert.False(hold.SavedOver);
        }

        [Fact]
        public void An_update_started_on_a_newer_file_ends_the_hold_so_the_close_saves_what_it_recorded()
        {
            File.WriteAllText(settings, NewerFile);
            var hold = SettingsHold.Begin(JsonConvert.DeserializeObject<OpenDashSettings>(NewerFile), false, () => settings, "1.2.0", new ListLog());
            hold.Changed();
            Assert.True(hold.MaySaveOnOwn);
        }
    }
}
