// RigConcurrencyTests.cs: the panel adds and removes screens and strips while SimHub's threads are reading the rig (#572).
//
// The panel edits the live settings object on the interface thread, and every property a dashboard or a profile reads
// goes through the same object on SimHub's: a screen's delegates look their screen up with ScreenByNamespace, and
// DataUpdate and CarLadderChosen ask AnyCarLadderWanted, which walks the strips. A List written in place under a
// foreach throws "Collection was modified" at the reader, which inside DataUpdate skips the frame and inside a delegate
// surfaces in SimHub's property evaluation. Every collection the panel changes is therefore replaced rather than
// written into, so that a reader holds either the old one or the new one and never one halfway through a change.
//
// The race is held with a fixed number of rounds rather than a time: the writer adds and removes a screen and a strip
// a set number of times, and the reader reads for as long as the writer has rounds left. The other half pins the
// replacement itself, for the dictionaries whose race is too rare to meet in a test of this length.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class RigConcurrencyTests
    {
        private static readonly PackageEntry Rim = new PackageEntry { Package = "p", Folder = "OpenDash 850x480", Kind = Contract.KindFace, Width = 850, Height = 480 };
        private static readonly PackageEntry Wall = new PackageEntry { Package = "w", Folder = "OpenDash Pit wall", Kind = Contract.KindPitWall, Width = 1920, Height = 1080 };

        private const int Rounds = 3000;

        /// <summary>A rig of a few screens and strips, so that a reader's walk is long enough to be interrupted.</summary>
        private static OpenDashSettings Rig()
        {
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            foreach (var name in new[] { "Rim", "Dash", "Spare rim", "Fourth" }) settings.AddScreen(Rim, name);
            settings.AddScreen(Wall, "Pit wall");
            foreach (var name in new[] { "Wheel rim", "Dash brow", "Monitor" }) settings.AddLedBar("3-9-3", name, "arduino");
            settings.Normalise();
            return settings;
        }

        /// <summary>
        /// Runs <paramref name="write"/> <see cref="Rounds"/> times on one thread while <paramref name="read"/> runs on
        /// another until the writer is done, and returns whatever the reader threw.
        /// </summary>
        private static Exception RaceOf(Action<int> write, Action read)
        {
            Exception thrown = null;
            var done = 0;
            using (var start = new Barrier(2))
            {
                var reader = new Thread(() =>
                {
                    start.SignalAndWait();
                    try
                    {
                        while (Volatile.Read(ref done) == 0) read();
                    }
                    catch (Exception ex)
                    {
                        thrown = ex;
                    }
                }) { IsBackground = true };
                reader.Start();
                start.SignalAndWait();
                try
                {
                    for (var round = 0; round < Rounds && thrown == null; round++) write(round);
                }
                finally
                {
                    Volatile.Write(ref done, 1);
                    reader.Join();
                }
            }
            return thrown;
        }

        [Fact]
        public void Looking_a_screen_up_while_the_panel_adds_and_removes_screens_never_throws()
        {
            var settings = Rig();
            var thrown = RaceOf(round =>
            {
                var added = settings.AddScreen(Rim, "Added");
                settings.RemoveScreen(added.Namespace);
                if (round % 10 == 0) settings.Normalise();
            }, () =>
            {
                // Absent, so that every read walks the whole rig, and then present, as a delegate's is.
                settings.ScreenByNamespace("NoSuchScreen");
                Assert.NotNull(settings.ScreenFace(settings.RigScreens()[0].Namespace));
            });
            Assert.Null(thrown);
        }

        [Fact]
        public void Asking_for_the_cars_ladder_while_the_panel_adds_and_removes_strips_never_throws()
        {
            var settings = Rig();
            var thrown = RaceOf(round =>
            {
                var added = settings.AddLedBar("3-9-3", "Added", "arduino");
                settings.RemoveLedBar(added.Namespace);
                if (round % 10 == 0) settings.Normalise();
            }, () =>
            {
                settings.AnyCarLadderWanted();
                settings.LedBarByNamespace("NoSuchStrip");
            });
            Assert.Null(thrown);
        }

        [Fact]
        public void A_duplicated_screen_and_a_face_made_on_demand_also_replace_the_rig()
        {
            var settings = Rig();
            var before = settings.Rig;
            var count = before.Count;
            var copy = settings.DuplicateScreen(before[0].Namespace, Rim, "Copy");
            Assert.NotNull(copy);
            Assert.NotSame(before, settings.Rig);
            Assert.Equal(count, before.Count);

            var withCopy = settings.Rig;
            settings.Face(Contract.FaceSizes.First(face => settings.ScreenByNamespace(Contract.FacePrefix(face)) == null));
            Assert.NotSame(withCopy, settings.Rig);
            Assert.Equal(count + 1, withCopy.Count);

            var withFace = settings.Rig;
            Assert.True(settings.RemoveScreen(copy.Namespace));
            Assert.NotSame(withFace, settings.Rig);
            Assert.Equal(count + 2, withFace.Count);

            // A remove whose folder stayed puts the screen back (#861), and that too is a new rig rather than an insert
            // into the one SimHub's threads may be walking.
            var withoutCopy = settings.Rig;
            settings.PutScreenBack(copy, 1);
            Assert.NotSame(withoutCopy, settings.Rig);
            Assert.Equal(count + 1, withoutCopy.Count);
            Assert.Same(copy, settings.Rig[1]);
        }

        [Fact]
        public void Looking_a_screen_up_while_the_panel_removes_a_screen_and_puts_it_back_never_throws()
        {
            var settings = Rig();
            var thrown = RaceOf(round =>
            {
                var added = settings.AddScreen(Rim, "Added");
                var at = settings.Rig.IndexOf(added);
                settings.RemoveScreen(added.Namespace);
                settings.PutScreenBack(added, at);
                settings.RemoveScreen(added.Namespace);
            }, () =>
            {
                settings.ScreenByNamespace("NoSuchScreen");
                Assert.NotNull(settings.ScreenFace(settings.RigScreens()[0].Namespace));
            });
            Assert.Null(thrown);
        }

        [Fact]
        public void A_strip_added_or_removed_and_a_save_replace_the_strips_rather_than_write_into_them()
        {
            var settings = Rig();
            var before = settings.LedBars;
            var count = before.Count;
            var added = settings.AddLedBar("3-9-3", "Added", "arduino");
            Assert.NotSame(before, settings.LedBars);
            Assert.Equal(count, before.Count);

            var withAdded = settings.LedBars;
            Assert.True(settings.RemoveLedBar(added.Namespace));
            Assert.NotSame(withAdded, settings.LedBars);
            Assert.Equal(count + 1, withAdded.Count);

            // A file with a hole in its strips is repaired into a new list, not by removing from the one being read.
            var holed = new List<LedBar>(settings.LedBars) { null };
            settings.LedBars = holed;
            settings.Normalise();
            Assert.NotSame(holed, settings.LedBars);
            Assert.Equal(count + 1, holed.Count);
            Assert.Equal(count, settings.LedBars.Count);
        }

        [Fact]
        public void A_theme_setting_replaces_the_rigs_choices_rather_than_writing_into_them()
        {
            var settings = Rig();
            var setting = Contract.ThemeSettings.First();
            var before = settings.ThemeSettings;
            settings.SetThemeSetting(setting, setting.Choices.First(choice => choice.Id != setting.Default).Id);
            Assert.NotSame(before, settings.ThemeSettings);
            Assert.False(before.ContainsKey(setting.Property));
            Assert.Equal(setting.Choices.First(choice => choice.Id != setting.Default).Id, settings.ThemeSetting(setting));
        }

        [Fact]
        public void A_pit_wall_zone_replaces_the_screens_zones_rather_than_writing_into_them()
        {
            var settings = Rig();
            var wall = settings.RigScreens().Single(screen => screen.IsPitWall);
            var slot = Contract.PitWallZoneSlots.First(s => !s.Wide);
            var before = wall.PitWallZones;
            var was = before[slot.Key];
            var page = Contract.NormaliseZonePage(was + 1, slot.Fallback);
            wall.SetZonePage(slot.Key, page);
            Assert.NotSame(before, wall.PitWallZones);
            Assert.Equal(was, before[slot.Key]);
            Assert.Equal(page, wall.ZonePage(slot.Key));
        }

        [Fact]
        public void A_strip_colour_replaces_the_strips_colours_rather_than_writing_into_them()
        {
            var settings = Rig();
            var bar = settings.LedBarList()[0];
            var colour = Contract.LedColours[0];
            var before = bar.Colours;
            Assert.True(bar.SetColour(colour.Key, "#123456"));
            Assert.NotSame(before, bar.Colours);
            Assert.False(before.ContainsKey(colour.Key));

            var set = bar.Colours;
            Assert.True(bar.SetColour(colour.Key, null));
            Assert.NotSame(set, bar.Colours);
            Assert.True(set.ContainsKey(colour.Key));
        }

        [Fact]
        public void A_folder_fingerprint_replaces_the_record_rather_than_writing_into_it()
        {
            var settings = Rig();
            var record = new SettingsFolderRecord(() => settings);
            record.Set("OpenDash 850x480", "first");
            var before = settings.FolderFingerprints;
            record.Set("OpenDash 850x480", "second");
            Assert.NotSame(before, settings.FolderFingerprints);
            Assert.Equal("first", before["OpenDash 850x480"]);
            Assert.Equal("second", record.Get("OpenDash 850x480"));
            // The comparer the file was read with goes with it, so a later spelling still finds the folder.
            Assert.Same(before.Comparer, settings.FolderFingerprints.Comparer);
        }
    }
}
