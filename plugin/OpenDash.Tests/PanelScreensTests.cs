// PanelScreensTests.cs: when the Screens page tells a driver to remove the dashboards they have no screen for.
//
// The line is for a rig the migration made, and whether a screen is one of those is carried by the screen
// rather than guessed from how many the rig holds (#478). What is held here is that fact through every path
// that sets it: a rig added by hand, a rig migrated from a settings file older than ADR 0017, the driver
// keeping or removing its screens, and a rig a released build migrated before the fact was recorded. The
// settings go through Json.NET, which is what SimHub writes and reads them with.
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelScreensTests
    {
        [Fact]
        public void A_rig_of_five_screens_added_on_the_rig_tab_is_never_told_to_remove_any()
        {
            // The rig of the #459 walk-through: built from nothing, one screen at a time, five by the end,
            // two of them at one size.
            var settings = new OpenDashSettings();
            settings.Normalise();
            Assert.Empty(settings.RigScreens());
            settings.AddScreen(Entry("OpenDash 1280x480", Contract.KindFace, 1280, 480), "Main dash");
            settings.AddScreen(Entry("OpenDash 1280x480", Contract.KindFace, 1280, 480), "Rim");
            settings.AddScreen(Entry("OpenDash 850x480", Contract.KindFace, 850, 480), null);
            settings.AddScreen(Entry("OpenDash Companion", Contract.KindCompanion, 850, 480), null);
            settings.AddScreen(Entry("OpenDash Pit wall", Contract.KindPitWall, 1920, 1080), null);
            settings.Normalise();

            Assert.Equal(5, settings.RigScreens().Count);
            Assert.False(PanelScreens.ShowsUnclaimedNote(settings.RigScreens()));
            // Nor once written and read back, which is where a sixth start would find it.
            var read = RoundTrip(settings);
            Assert.Equal(5, read.RigScreens().Count);
            Assert.False(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));
        }

        [Fact]
        public void A_migrated_rig_is_told_until_every_screen_the_migration_made_is_kept_or_removed()
        {
            // Four folders, one fewer than the count used to ask for, so this rig was never told at all.
            var settings = Upgraded("OpenDash 1280x480", "OpenDash 850x480", "OpenDash Companion", "OpenDash Pit wall");
            settings.Normalise();
            Assert.Equal(4, settings.RigScreens().Count);
            Assert.All(settings.RigScreens(), screen => Assert.True(screen.Unclaimed));
            Assert.True(PanelScreens.ShowsUnclaimedNote(settings.RigScreens()));

            // It survives being written and read back, and the copy the panel works on.
            var read = RoundTrip(settings);
            Assert.True(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));
            var copy = new OpenDashSettings();
            copy.CopyFrom(read);
            Assert.True(PanelScreens.ShowsUnclaimedNote(copy.RigScreens()));

            // A screen the driver adds beside them answers for itself and for none of the others.
            var rim = read.AddScreen(Entry("OpenDash 1280x480", Contract.KindFace, 1280, 480), "Rim");
            read.Normalise();
            Assert.False(rim.Unclaimed);
            Assert.True(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));

            // Removing some of them is not the end of it while one is left that nobody has answered for.
            Assert.True(read.RemoveScreen("Face850x480"));
            Assert.True(read.RemoveScreen(Contract.PitWallPrefix));
            read.Normalise();
            Assert.True(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));
            read.ScreenByNamespace(Contract.CompanionPrefix).Keep();
            Assert.True(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));

            // Keeping the last one is the moment the line stops being true, and it stays gone.
            read.ScreenByNamespace("Face1280x480").Keep();
            Assert.False(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));
            Assert.False(PanelScreens.ShowsUnclaimedNote(RoundTrip(read).RigScreens()));
        }

        [Fact]
        public void A_rig_of_one_screen_the_migration_made_is_told_as_well()
        {
            // How few is not the question either: a driver whose old plugin wrote a single dashboard still
            // has a card they did not choose, and it is theirs to keep or remove.
            var settings = Upgraded("OpenDash 1280x480");
            settings.Normalise();
            Assert.True(PanelScreens.ShowsUnclaimedNote(settings.RigScreens()));
            Assert.True(settings.RemoveScreen("Face1280x480"));
            Assert.False(PanelScreens.ShowsUnclaimedNote(settings.RigScreens()));
        }

        [Fact]
        public void A_rig_a_released_build_migrated_is_recognised_while_it_is_as_the_migration_left_it()
        {
            // What 0.3.0-rc.7 wrote for a rig it had migrated and nobody had touched since: no screen says
            // whether the migration made it, the faces carry no package because the migration recorded
            // none, and the companion carries the one the start filled in, which leaves it looking added.
            var read = Legacy(
                new[] { "OpenDash 1280x480", "OpenDash 850x480", "OpenDash Companion" },
                Face("Face1280x480", "1280 × 480", 1280, "OpenDash 1280x480", null),
                Face("Face850x480", "850 × 480", 850, "OpenDash 850x480", null),
                Companion("Companion", "OpenDash Companion", Package("OpenDash Companion")));

            Assert.True(read.ScreenByNamespace("Face1280x480").Unclaimed);
            Assert.True(read.ScreenByNamespace("Face850x480").Unclaimed);
            Assert.False(read.ScreenByNamespace(Contract.CompanionPrefix).Unclaimed);
            Assert.True(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));

            // Answered once, and from then on carried like any other: removing one leaves a folder on record
            // that no screen holds, and the other is still unanswered for all that.
            Assert.True(read.RemoveScreen("Face850x480"));
            read = RoundTrip(read);
            Assert.True(read.ScreenByNamespace("Face1280x480").Unclaimed);
            Assert.True(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));
        }

        [Fact]
        public void A_rig_a_released_build_migrated_is_not_told_once_it_shows_the_driver_has_been_through_it()
        {
            // A face renamed since is one the driver kept, since the migration named every face by its size.
            var renamed = Legacy(
                new[] { "OpenDash 1280x480", "OpenDash 850x480" },
                Face("Face1280x480", "Main dash", 1280, "OpenDash 1280x480", null),
                Face("Face850x480", "850 × 480", 850, "OpenDash 850x480", null));
            Assert.False(renamed.ScreenByNamespace("Face1280x480").Unclaimed);
            Assert.True(renamed.ScreenByNamespace("Face850x480").Unclaimed);

            // And a rig that has let go of a folder the old plugin wrote is one whose driver has begun
            // removing screens. What is left may well all be theirs, and nothing on the rig says otherwise,
            // so it is not told to remove any: the conservative answer where the file runs out of facts.
            var pruned = Legacy(
                new[] { "OpenDash 1280x480", "OpenDash 850x480", "OpenDash 800x480" },
                Face("Face1280x480", "1280 × 480", 1280, "OpenDash 1280x480", null),
                Face("Face850x480", "850 × 480", 850, "OpenDash 850x480", null));
            Assert.False(PanelScreens.ShowsUnclaimedNote(pruned.RigScreens()));
        }

        [Fact]
        public void A_rig_a_released_build_wrote_from_screens_added_on_the_rig_tab_is_never_told()
        {
            // The #459 rig as the build it was made on wrote it: five screens, every one carrying the
            // package it was added from, and no word from any of them on where they came from.
            var read = Legacy(
                new[] { "OpenDash 1280x480", "OpenDash Rim", "OpenDash 850x480", "OpenDash Companion", "OpenDash Pit wall" },
                Face("Face1280x480", "Main dash", 1280, "OpenDash 1280x480", Package("OpenDash 1280x480")),
                Face("Rim", "Rim", 1280, "OpenDash Rim", Package("OpenDash 1280x480")),
                Face("Face850x480", "850 × 480", 850, "OpenDash 850x480", Package("OpenDash 850x480")),
                Companion("Companion", "OpenDash Companion", Package("OpenDash Companion")),
                @"{ ""Namespace"": ""PitWall"", ""Name"": ""Pit wall"", ""Kind"": ""pitwall"", ""Width"": 1920, ""Height"": 1080,
                    ""Folder"": ""OpenDash Pit wall"", ""Package"": """ + Package("OpenDash Pit wall") + @""" }");

            Assert.Equal(5, read.RigScreens().Count);
            Assert.All(read.RigScreens(), screen => Assert.False(screen.Unclaimed));
            Assert.False(PanelScreens.ShowsUnclaimedNote(read.RigScreens()));
        }

        private static string Package(string folder) => "OpenDashPlugin.Resources." + folder + ".simhubdash";

        private static PackageEntry Entry(string folder, string kind, int width, int height)
        {
            return new PackageEntry { Package = Package(folder), Folder = folder, Kind = kind, Width = width, Height = height };
        }

        /// <summary>A settings file written before ADR 0017 by a plugin that had installed these folders.</summary>
        private static OpenDashSettings Upgraded(params string[] folders)
        {
            var settings = new OpenDashSettings();
            foreach (var folder in folders) settings.FolderFingerprints[folder] = "written by 0.2";
            return settings;
        }

        /// <summary>
        /// A settings file as a released build wrote it after migrating: the rig, and the folder record the
        /// migration read, which nothing has ever pruned. Read as SimHub reads it, and normalised as the
        /// plugin's start does.
        /// </summary>
        private static OpenDashSettings Legacy(string[] folders, params string[] screens)
        {
            var record = new List<string>();
            foreach (var folder in folders) record.Add("\"" + folder + "\": \"x\"");
            var json = "{ \"FolderFingerprints\": { " + string.Join(", ", record) + " }, \"Rig\": [ " + string.Join(", ", screens) + " ] }";
            var read = JsonConvert.DeserializeObject<OpenDashSettings>(json);
            foreach (var screen in read.RigScreens()) Assert.Null(screen.Unclaimed);
            read.Normalise();
            return read;
        }

        private static string Face(string ns, string name, int width, string folder, string package)
        {
            return "{ \"Namespace\": \"" + ns + "\", \"Name\": \"" + name + "\", \"Kind\": \"face\", \"Width\": " + width
                + ", \"Height\": 480, \"Folder\": \"" + folder + "\""
                + (package == null ? string.Empty : ", \"Package\": \"" + package + "\"") + " }";
        }

        private static string Companion(string ns, string folder, string package)
        {
            return "{ \"Namespace\": \"" + ns + "\", \"Name\": \"Companion\", \"Kind\": \"companion\", \"Width\": 850, \"Height\": 480"
                + ", \"Folder\": \"" + folder + "\", \"Package\": \"" + package + "\" }";
        }

        private static OpenDashSettings RoundTrip(OpenDashSettings settings)
        {
            var read = JsonConvert.DeserializeObject<OpenDashSettings>(JsonConvert.SerializeObject(settings));
            read.Normalise();
            return read;
        }
    }
}
