// PanelAddScreenTests.cs: what "add a screen" asks, in what order, and which questions it leaves out.
//
// The rule worth pinning is the one that makes the page short: the second question is asked only when
// there is something to ask, and for a companion or a pit wall it is which way round rather than how
// big. Nothing in the WPF can be reached from here, so what is held is the decision and the wording.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelAddScreenTests
    {
        private static PackageEntry Package(string folder, string kind, int width, int height)
        {
            return new PackageEntry
            {
                Package = "OpenDashPlugin.Resources." + folder + ".simhubdash",
                Folder = folder,
                Kind = kind,
                Width = width,
                Height = height,
            };
        }

        /// <summary>A build carrying the fourteen packages a release embeds.</summary>
        private static IList<PackageEntry> Catalogue()
        {
            return new List<PackageEntry>
            {
                Package("openDash", Contract.KindFace, 1920, 480),
                Package("openDash 1280x720", Contract.KindFace, 1280, 720),
                Package("openDash 1280x480", Contract.KindFace, 1280, 480),
                Package("openDash 850x480", Contract.KindFace, 850, 480),
                Package("openDash Companion", Contract.KindCompanion, 850, 480),
                Package("openDash Companion portrait", Contract.KindCompanion, 480, 850),
                Package("openDash Pit wall", Contract.KindPitWall, 1920, 1080),
                Package("openDash Pit wall portrait", Contract.KindPitWall, 1080, 1920),
                Package("openDash 800 round", Contract.KindSlots, 800, 800),
                Package("openDash 480 round", Contract.KindSlots, 480, 480),
            };
        }

        [Fact]
        public void The_first_question_is_what_kind_of_screen_it_is()
        {
            var types = PanelAddScreen.Types(Catalogue());
            Assert.Equal(new[] { Contract.KindFace, Contract.KindCompanion, Contract.KindPitWall, Contract.KindSlots }, types.Select(t => t.Kind));
            Assert.Equal(new[] { "Dash or wheel", "Companion", "Pit wall", "Card face" }, types.Select(t => t.Label));
            // Every one says what it is: two words on a button cannot, and a driver adding their first
            // screen has nowhere else to find out.
            foreach (var type in types) Assert.NotEmpty(type.Caption);
        }

        /// <summary>The census is what the build carries, so a build with no pit wall offers none.</summary>
        [Fact]
        public void A_kind_the_build_carries_no_package_for_is_not_offered()
        {
            var types = PanelAddScreen.Types(Catalogue().Where(e => e.Kind != Contract.KindPitWall).ToList());
            Assert.DoesNotContain(types, t => t.Kind == Contract.KindPitWall);
            Assert.Empty(PanelAddScreen.Types(new PackageEntry[0]));
            Assert.Empty(PanelAddScreen.Types(null));
        }

        /// <summary>
        /// The size question is only asked when there is a size to pick, and for the two kinds that ship
        /// one screen mounted two ways it is asked as an orientation.
        /// </summary>
        [Fact]
        public void The_second_question_is_asked_only_where_there_is_one()
        {
            var types = PanelAddScreen.Types(Catalogue());
            var of = new Dictionary<string, ScreenType>();
            foreach (var type in types) of[type.Kind] = type;

            Assert.Equal(SizeQuestion.Size, PanelAddScreen.Question(of[Contract.KindFace]));
            Assert.Equal(SizeQuestion.Orientation, PanelAddScreen.Question(of[Contract.KindCompanion]));
            Assert.Equal(SizeQuestion.Orientation, PanelAddScreen.Question(of[Contract.KindPitWall]));
            // The round faces are two genuine sizes rather than one screen either way up.
            Assert.Equal(SizeQuestion.Size, PanelAddScreen.Question(of[Contract.KindSlots]));

            // And a build carrying one companion asks nothing at all about it.
            var one = PanelAddScreen.Types(Catalogue().Where(e => e.Folder != "openDash Companion portrait").ToList())
                .First(t => t.Kind == Contract.KindCompanion);
            Assert.Equal(SizeQuestion.None, PanelAddScreen.Question(one));
        }

        [Fact]
        public void An_orientation_is_offered_landscape_first_and_written_as_a_way_round()
        {
            var pitWall = PanelAddScreen.Types(Catalogue()).First(t => t.Kind == Contract.KindPitWall);
            var offered = PanelAddScreen.Offered(pitWall);
            Assert.Equal(new[] { "openDash Pit wall", "openDash Pit wall portrait" }, offered.Select(e => e.Folder));
            Assert.Equal("Landscape", PanelAddScreen.SizeLabel(pitWall, offered[0], 0));
            Assert.Equal("Portrait", PanelAddScreen.SizeLabel(pitWall, offered[1], 1));
            // Which is the whole point: nobody has to work out that 1080 × 1920 is the same pit wall.
            Assert.DoesNotContain("1920", PanelAddScreen.SizeLabel(pitWall, offered[0], 0));
        }

        [Fact]
        public void A_size_is_written_as_the_pixels_a_driver_measures()
        {
            var faces = PanelAddScreen.Types(Catalogue()).First(t => t.Kind == Contract.KindFace);
            var offered = PanelAddScreen.Offered(faces);
            Assert.Equal(faces.Entries, offered);
            Assert.Equal("1920 × 480", PanelAddScreen.SizeLabel(faces, offered[0], 0));
            // The design's own caption wins where a package has one, so a round face reads as the
            // product writes it rather than as its pixels.
            var round = PanelAddScreen.Types(Catalogue()).First(t => t.Kind == Contract.KindSlots);
            Assert.Equal(round.Entries[0].SizeCaption ?? round.Entries[0].SizeLabel, PanelAddScreen.SizeLabel(round, round.Entries[0], 0));
        }

        /// <summary>The name box opens on something a driver would recognise, not on a resource name.</summary>
        [Fact]
        public void The_name_is_filled_in_with_the_package_the_design_names_or_with_its_size()
        {
            Assert.Equal("Rim", PanelAddScreen.DefaultName(Package("openDash 850x480", Contract.KindFace, 850, 480)));
            Assert.Equal("1280 × 720", PanelAddScreen.DefaultName(Package("openDash 1280x720", Contract.KindFace, 1280, 720)));
            Assert.Equal(string.Empty, PanelAddScreen.DefaultName(null));
        }

        /// <summary>The second screen at a size is the case worth saying out loud: it is the whole of
        /// ADR 0017 and is invisible from the outside until two rims cycle together.</summary>
        [Fact]
        public void The_note_says_what_the_button_will_do()
        {
            var entry = Package("openDash 850x480", Contract.KindFace, 850, 480);
            Assert.Contains("openDash 850x480", PanelAddScreen.Note(entry, false));
            Assert.Contains("second", PanelAddScreen.Note(entry, true));
            Assert.Contains("settings of its own", PanelAddScreen.Note(entry, true));
        }

        /// <summary>
        /// SimHub lists a dashboard under its title, which the installer sets to the name the driver
        /// chose. The line used to name the folder, which sent them looking through Dash Studio for a row
        /// that does not exist under that word.
        /// </summary>
        [Fact]
        public void What_is_said_after_adding_names_the_two_steps_SimHub_does_not_take()
        {
            var line = PanelAddScreen.Added("Rim", "Rim");
            Assert.Contains("Restart SimHub", line);
            Assert.Contains("Dash Studio", line);
            Assert.Contains("\"Rim\"", line);
            Assert.DoesNotContain("openDash 850x480", line);
        }

        /// <summary>A resize keeps everything but the pixels, which is the only reason to offer one
        /// rather than "remove it and add the right one".</summary>
        [Fact]
        public void A_resize_promises_the_settings_and_the_bindings()
        {
            Assert.Contains("stay as they are", PanelAddScreen.ResizeCaption);
            Assert.Contains("properties keep the names", PanelAddScreen.ResizeCaption);
            Assert.Contains("Restart SimHub", PanelAddScreen.Resized("Rim", "1280 × 480", "Rim"));
        }
    }
}
