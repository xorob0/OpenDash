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
        /// <summary>
        /// The dialog opens on 850 x 480 where that size is offered, and on the first entry otherwise.
        /// </summary>
        /// <remarks>
        /// It used to open on whichever size came first in the catalogue, which is the 1920 x 480: the
        /// widest, the one the artboards lead with, and not the one most of these screens are. Reported
        /// from a rig as the size that should be the default and the most tested.
        ///
        /// Held against the real catalogue as well as a made-up one, because the point of the change is
        /// which size a driver is actually offered.
        /// </remarks>
        [Fact]
        public void The_size_control_opens_on_the_preferred_face()
        {
            var faces = new ScreenType(Contract.KindFace, "Dash face", "caption", new[]
            {
                Package("OpenDash", Contract.KindFace, 1920, 480),
                Package("OpenDash 850x480", Contract.KindFace, 850, 480),
                Package("OpenDash 800x480", Contract.KindFace, 800, 480),
            });
            var offered = PanelAddScreen.Offered(faces);
            var index = PanelAddScreen.PreferredIndex(faces);
            Assert.Equal(Contract.PreferredFaceWidth, offered[index].Width);
            Assert.Equal(Contract.PreferredFaceHeight, offered[index].Height);

            // A type that does not offer it keeps the first entry, which is every type but the face.
            var companions = new ScreenType(Contract.KindCompanion, "Companion", "caption", new[]
            {
                Package("OpenDash Companion", Contract.KindCompanion, 850, 480),
                Package("OpenDash Companion portrait", Contract.KindCompanion, 480, 850),
            });
            Assert.InRange(PanelAddScreen.PreferredIndex(companions), 0, PanelAddScreen.Offered(companions).Count - 1);

            // And the migration target is deliberately NOT moved with it: a settings file written
            // before the faces were separated already landed on that face for everyone on rc.2.
            Assert.Equal(1920, Contract.ReferenceFace.Width);
            Assert.Equal(480, Contract.ReferenceFace.Height);
        }

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
                Package("OpenDash", Contract.KindFace, 1920, 480),
                Package("OpenDash 1280x720", Contract.KindFace, 1280, 720),
                Package("OpenDash 1280x480", Contract.KindFace, 1280, 480),
                Package("OpenDash 850x480", Contract.KindFace, 850, 480),
                Package("OpenDash Companion", Contract.KindCompanion, 850, 480),
                Package("OpenDash Companion portrait", Contract.KindCompanion, 480, 850),
                Package("OpenDash Pit wall", Contract.KindPitWall, 1920, 1080),
                Package("OpenDash Pit wall portrait", Contract.KindPitWall, 1080, 1920),
                Package("OpenDash 800 round", Contract.KindSlots, 800, 800),
                Package("OpenDash 480 round", Contract.KindSlots, 480, 480),
            };
        }

        /// <summary>A kind is named one way on its card, under its name and on the Add sheet's tile:
        /// Screens.dc.html's Face, Pit wall, Companion and Round, never the internal id "Slots".</summary>
        [Fact]
        public void A_kind_has_one_name()
        {
            Assert.Equal("Face", PanelAddScreen.KindName(Contract.KindFace));
            Assert.Equal("Companion", PanelAddScreen.KindName(Contract.KindCompanion));
            Assert.Equal("Pit wall", PanelAddScreen.KindName(Contract.KindPitWall));
            Assert.Equal("Round", PanelAddScreen.KindName(Contract.KindSlots));
            foreach (var kind in new[] { Contract.KindCompanion, Contract.KindPitWall, Contract.KindSlots })
            {
                Assert.Equal(PanelAddScreen.KindName(kind), PanelAddScreen.LabelOf(kind));
            }
            // The sheet's tile calls a face what AddScreen.dc.html does; the card says what it is.
            Assert.Equal("Dash face", PanelAddScreen.LabelOf(Contract.KindFace));
        }

        /// <summary>AddScreen.dc.html's four kinds, in its order, named as it names them, each with the note
        /// under its name saying where such a screen is found.</summary>
        [Fact]
        public void The_first_question_is_what_kind_of_screen_it_is()
        {
            var types = PanelAddScreen.Types(Catalogue());
            Assert.Equal(new[] { Contract.KindFace, Contract.KindPitWall, Contract.KindCompanion, Contract.KindSlots }, types.Select(t => t.Kind));
            Assert.Equal(new[] { "Dash face", "Pit wall", "Companion", "Round" }, types.Select(t => t.Label));
            Assert.Equal(new[] { "Wheel or dash", "Monitor or TV", "Phone or tablet", "Cards on a round screen" }, types.Select(t => t.Caption));
            // The fifth tile is greyed (#116) and carries its own note.
            Assert.Equal("Flags screen", PanelSoon.FlagsScreen.Title);
            Assert.Equal("A second display for flags", PanelAddScreen.FlagsScreenCaption);
        }

        /// <summary>The sheet's three steps and its foot, in AddScreen.dc.html's words where voice.md agrees:
        /// a heading is a noun, and the step names the name the driver typed, which SimHub lists it under.</summary>
        [Fact]
        public void The_sheet_asks_in_three_steps_and_says_what_comes_next()
        {
            Assert.Equal("Add a screen", PanelAddScreen.SectionTitle);
            Assert.Equal("Kind", PanelAddScreen.KindStep);
            Assert.Equal("Size", PanelAddScreen.SizeStep);
            Assert.Equal("Name", PanelAddScreen.NameStep);
            Assert.Equal("Next steps", PanelAddScreen.NextStepsTitle);
            Assert.Equal("Restart SimHub, then assign \"Rim\" to this display in Dash Studio.", PanelAddScreen.NextStep("Rim"));
            Assert.Equal("Restart SimHub, then assign \"Rim\" to this display in Dash Studio.", PanelAddScreen.NextStep("  Rim "));
            // The same step the line after Add says, so the foot and the message cannot disagree.
            Assert.EndsWith(PanelAddScreen.NextStep("Rim"), PanelAddScreen.Added("Rim", "Rim"));

            // The foot names what Add will call the screen: the rig's settings name a taken name apart and
            // give an empty box the size, and NameFor says the same before the press.
            var settings = new OpenDashSettings();
            settings.Normalise();
            var rim = Package("OpenDash 850x480", Contract.KindFace, 850, 480);
            settings.AddScreen(rim, "Rim");
            var names = settings.RigScreens().Select(s => s.Name).ToList();
            Assert.Equal("Rim (2)", PanelAddScreen.NameFor(" Rim ", rim, names));
            Assert.Equal(settings.AddScreen(rim, " Rim ").Name, PanelAddScreen.NameFor(" Rim ", rim, names));
            names = settings.RigScreens().Select(s => s.Name).ToList();
            Assert.Equal("850 × 480", PanelAddScreen.NameFor("  ", rim, names));
            Assert.Equal(settings.AddScreen(rim, "  ").Name, PanelAddScreen.NameFor("  ", rim, names));
            Assert.Equal("Restart SimHub, then assign \"Rim (3)\" to this display in Dash Studio.",
                PanelAddScreen.NextStep(PanelAddScreen.NameFor("Rim", rim, settings.RigScreens().Select(s => s.Name))));
            Assert.Equal("Restart SimHub, then assign \"Rim\" to this display in Dash Studio.", PanelAddScreen.NextStep(PanelAddScreen.NameFor("Rim", rim, null)));
            Assert.Equal("Add screen", PanelAddScreen.AddButton);
            Assert.Equal("Adds the screen and installs its dashboard.", PanelAddScreen.AddTooltip);
            Assert.Equal("Cancel", PanelAddScreen.CancelButton);
            Assert.Equal("Goes back without adding anything.", PanelAddScreen.CancelTooltip);
            Assert.Equal("This build ships no dashboards.", PanelAddScreen.NothingToAdd);
        }

        /// <summary>A size tile is the screen's outline, its size and the name the design gives it: a wide
        /// face squeezed, a tall one stretched, a round one a circle, and none larger than its band.</summary>
        [Fact]
        public void A_size_tile_draws_the_screens_shape_and_its_name()
        {
            Assert.Equal(new double[] { 64, 20 }, PanelAddScreen.TileShape(1920, 480));
            Assert.Equal(new double[] { 64, 30 }, PanelAddScreen.TileShape(1280, 480));
            Assert.Equal(new double[] { 25, 36 }, PanelAddScreen.TileShape(600, 686));
            Assert.Equal(new double[] { 36, 36 }, PanelAddScreen.TileShape(480, 480));
            Assert.Equal(new double[] { 40, 40 }, PanelAddScreen.TileShape(0, 0));
            // Every outline fits the band it is drawn in.
            Assert.Equal(44, PanelAddScreen.SizeBand);
            foreach (var entry in Catalogue()) Assert.True(PanelAddScreen.TileShape(entry.Width, entry.Height)[1] <= PanelAddScreen.SizeBand);
            // The tiles' grids and words, as AddScreen.dc.html draws them.
            Assert.Equal(140, PanelAddScreen.KindTileLeast);
            Assert.Equal(3, PanelAddScreen.KindColumns);
            Assert.Equal(96, PanelAddScreen.SizeTileLeast);
            Assert.Equal(4, PanelAddScreen.SizeColumns);
            Assert.Equal(8, PanelAddScreen.TileGap);
            Assert.Equal(15, PanelAddScreen.KindTitleSize);
            Assert.Equal(14, PanelAddScreen.SizeLabelSize);
            Assert.Equal(11, PanelAddScreen.SizeHintSize);
            foreach (var entry in Catalogue())
            {
                var shape = PanelAddScreen.TileShape(entry.Width, entry.Height);
                Assert.InRange(shape[0], 12, 96);
                Assert.InRange(shape[1], 12, 40);
            }
            var types = PanelAddScreen.Types(Catalogue());
            var faces = types.First(t => t.Kind == Contract.KindFace);
            Assert.Equal("Main DDU", PanelAddScreen.SizeHint(faces, faces.Entries.First(e => e.Folder == "OpenDash")));
            Assert.Equal("Rim", PanelAddScreen.SizeHint(faces, faces.Entries.First(e => e.Folder == "OpenDash 850x480")));
            Assert.Null(PanelAddScreen.SizeHint(faces, faces.Entries.First(e => e.Folder == "OpenDash 1280x720")));
            // A way round, and a round screen, already say what they are.
            var pitWall = types.First(t => t.Kind == Contract.KindPitWall);
            Assert.Null(PanelAddScreen.SizeHint(pitWall, pitWall.Entries[0]));
            var round = types.First(t => t.Kind == Contract.KindSlots);
            Assert.Null(PanelAddScreen.SizeHint(round, round.Entries.First(e => e.Folder == "OpenDash 480 round")));
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
            // The Add sheet's second step is titled as the question is asked, as the edit sheet's row is.
            Assert.Equal("Size", PanelAddScreen.SizeStepTitle(of[Contract.KindFace]));
            Assert.Equal("Orientation", PanelAddScreen.SizeStepTitle(of[Contract.KindPitWall]));
            Assert.Equal("Orientation", PanelAddScreen.SizeStepTitle(of[Contract.KindCompanion]));

            // And a build carrying one companion asks nothing at all about it.
            var one = PanelAddScreen.Types(Catalogue().Where(e => e.Folder != "OpenDash Companion portrait").ToList())
                .First(t => t.Kind == Contract.KindCompanion);
            Assert.Equal(SizeQuestion.None, PanelAddScreen.Question(one));
        }

        [Fact]
        public void An_orientation_is_offered_landscape_first_and_written_as_a_way_round()
        {
            var pitWall = PanelAddScreen.Types(Catalogue()).First(t => t.Kind == Contract.KindPitWall);
            var offered = PanelAddScreen.Offered(pitWall);
            Assert.Equal(new[] { "OpenDash Pit wall", "OpenDash Pit wall portrait" }, offered.Select(e => e.Folder));
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
            // product writes it rather than as its pixels, and so does the round screen it names none for.
            var round = PanelAddScreen.Types(Catalogue()).First(t => t.Kind == Contract.KindSlots);
            Assert.Equal(new[] { "800 round", "480 round" }, PanelAddScreen.Offered(round).Select((e, i) => PanelAddScreen.SizeLabel(round, e, i)));
        }

        /// <summary>The name box opens on something a driver would recognise, not on a resource name, and a
        /// name the driver typed is never overwritten by the default of the next kind or size picked.</summary>
        [Fact]
        public void The_name_is_filled_in_with_the_package_the_design_names_or_with_its_size()
        {
            Assert.Equal("Rim", PanelAddScreen.DefaultName(Package("OpenDash 850x480", Contract.KindFace, 850, 480)));
            Assert.Equal("1280 × 720", PanelAddScreen.DefaultName(Package("OpenDash 1280x720", Contract.KindFace, 1280, 720)));
            Assert.Equal(string.Empty, PanelAddScreen.DefaultName(null));

            var rim = Package("OpenDash 850x480", Contract.KindFace, 850, 480);
            var big = Package("OpenDash 1280x720", Contract.KindFace, 1280, 720);
            // A default fills the box, distinct on the rig, while nothing is typed.
            Assert.Equal("Rim (2)", PanelAddScreen.FilledName("1280 × 720", false, rim, new[] { "Rim" }));
            // What the driver typed stays whatever is picked after it.
            Assert.Equal("Wheel", PanelAddScreen.FilledName("Wheel", true, big, new[] { "Rim" }));
            // Typing makes the box theirs, emptying it gives it back, and a default written from elsewhere
            // changes nothing.
            Assert.True(PanelAddScreen.Typed(false, true, "Wheel"));
            Assert.False(PanelAddScreen.Typed(true, true, "   "));
            Assert.True(PanelAddScreen.Typed(true, false, "Rim"));
            Assert.False(PanelAddScreen.Typed(false, false, "Rim"));
        }

        /// <summary>The second screen at a size is the case worth saying out loud: it is the whole of
        /// ADR 0017 and is invisible from the outside until two rims cycle together.</summary>
        [Fact]
        public void The_note_says_what_the_button_will_do()
        {
            var entry = Package("OpenDash 850x480", Contract.KindFace, 850, 480);
            // The ordinary case says nothing at all: a button reading "Add screen" has already said it.
            Assert.Equal(string.Empty, PanelAddScreen.Note(entry, false));
            // Another screen at a size is the one case worth a line, since two rims that page together is
            // what somebody would otherwise report as a bug. It counts nothing: the third at a size is not
            // "your second", and a second added after the first was removed is the only one.
            Assert.Equal("This 850 × 480 gets settings of its own.", PanelAddScreen.Note(entry, true));
            Assert.DoesNotContain("second", PanelAddScreen.Note(entry, true));
            // Except a card face, since every one of them reads the same twelve slots and a second gets none (#474).
            Assert.Equal(string.Empty, PanelAddScreen.Note(Package("OpenDash 480 round", Contract.KindSlots, 480, 480), true));
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
            Assert.DoesNotContain("OpenDash 850x480", line);
        }

        /// <summary>An edit keeps everything but the name and the pixels, which is the only reason to
        /// offer one rather than "remove it and add the right one".</summary>
        [Fact]
        public void An_edit_promises_the_settings_and_the_bindings()
        {
            // In the user's terms rather than in the settings model's: the promise is that the rig
            // survives an edit, and the property names behind it are not something a driver acts on.
            Assert.Equal("Your settings and bindings are kept.", PanelAddScreen.EditCaption);
            // The edit sheet's rows and presses, each in the words it is drawn in.
            Assert.Equal("Edit", PanelAddScreen.EditTitle);
            Assert.Equal("Edit Rim", PanelAddScreen.EditSheetTitle("Rim"));
            Assert.Equal("Name", PanelAddScreen.NameTitle);
            Assert.Equal("Also shown in SimHub's dashboard list.", PanelAddScreen.NameCaption);
            // One name for the one question, on the Add sheet's step and the edit sheet's row.
            Assert.Equal("Size", PanelAddScreen.SizeTitle);
            Assert.Equal(PanelAddScreen.SizeStep, PanelAddScreen.SizeTitle);
            Assert.Equal("Orientation", PanelAddScreen.OrientationTitle);
            Assert.Equal("Dashboard", PanelAddScreen.ReinstallTitle);
            Assert.Equal("Reinstall", PanelAddScreen.ReinstallButton);
            Assert.Equal("Save", PanelAddScreen.SaveButton);
            Assert.Equal("Applies your changes and writes the dashboard.", PanelAddScreen.SaveTooltip);
            // In the verb of the line after it, and with no hover on the button beside it to say it again.
            Assert.Equal("Installs this screen's dashboard again, at its saved name and size.", PanelAddScreen.ReinstallCaption);
            Assert.StartsWith("Installed Rim's dashboard again.", PanelAddScreen.Reinstalled("Rim"));
            var screens = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.cs"));
            Assert.DoesNotContain("reinstall.ToolTip", screens);
            // A round screen keeps no settings and has no bindings, so its sheet promises neither.
            Assert.Equal(PanelAddScreen.EditCaption, PanelAddScreen.EditCaptionFor(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.Equal(PanelAddScreen.EditCaption, PanelAddScreen.EditCaptionFor(new ScreenInstance { Kind = Contract.KindPitWall }));
            Assert.Null(PanelAddScreen.EditCaptionFor(new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 }));
            Assert.Contains("PanelAddScreen.EditCaptionFor(screen)", screens);
            Assert.Equal("Goes back without changing anything.", PanelAddScreen.EditCancelTooltip);
            // A failure says what happened and points at the log, where the installer's reason is written
            // (voice.md), as Duplicate and Remove do.
            Assert.Equal("Added Rim, but its dashboard could not be installed. See SimHub's log.", PanelAddScreen.AddFailed("Rim"));
            // The resize happened before the write failed: the screen is its new size, and the line says so.
            Assert.Equal("Rim is now 1280 × 480, but its dashboard could not be installed. See SimHub's log.", PanelAddScreen.ResizeFailed("Rim", "1280 × 480"));
            Assert.DoesNotContain("properties", PanelAddScreen.EditCaption);
            Assert.Contains("Restart SimHub", PanelAddScreen.Resized("Rim", "1280 × 480", "Rim"));
        }

        /// <summary>
        /// What Save does, from the two answers the panel holds.
        /// </summary>
        /// <remarks>
        /// The case worth pinning is the middle one: a name on its own is not a no-op, because SimHub
        /// lists a dashboard under its title and a rename that stopped at the card left the screen listed
        /// under the name the driver had just stopped using.
        /// </remarks>
        [Fact]
        public void Saving_an_edit_does_only_what_was_changed()
        {
            Assert.Equal(ScreenEdit.None, PanelAddScreen.Edit("Rim", "Rim", false));
            Assert.Equal(ScreenEdit.Rename, PanelAddScreen.Edit("Rim", "Wheel", false));
            Assert.Equal(ScreenEdit.Resize, PanelAddScreen.Edit("Rim", "Rim", true));

            // A size that moved decides on its own: the folder is written from the other package and the
            // name goes into it on the way, so there is never a rename left to do separately.
            Assert.Equal(ScreenEdit.Resize, PanelAddScreen.Edit("Rim", "Wheel", true));

            // Surrounding space is not a new name, and an empty box is somebody who cleared it and
            // thought better of it rather than a request to call the screen nothing.
            Assert.Equal(ScreenEdit.None, PanelAddScreen.Edit("Rim", "  Rim  ", false));
            Assert.Equal(ScreenEdit.None, PanelAddScreen.Edit("Rim", "   ", false));
            Assert.Equal(ScreenEdit.None, PanelAddScreen.Edit("Rim", null, false));
            Assert.Equal(ScreenEdit.Rename, PanelAddScreen.Edit("Rim", "  Wheel  ", false));

            // Case alone is a rename: SimHub prints the title, and "rim" is not what "Rim" looks like.
            Assert.Equal(ScreenEdit.Rename, PanelAddScreen.Edit("Rim", "rim", false));
        }

        /// <summary>
        /// A screen at a size this build offers no package for -- a card face a migration made at 1280 x 480,
        /// or a face size a later build dropped -- is never resized by a Save that only renamed it.
        /// </summary>
        /// <remarks>
        /// The edit sheet's size row opened on the first size offered and held it as the answer, so Save took
        /// the resize path: the screen's dashboard folder was removed and the screen became an 800 round.
        /// </remarks>
        [Fact]
        public void A_screen_at_a_size_not_offered_is_not_resized_unless_a_size_is_picked()
        {
            var rounds = new[] { Package("OpenDash 480 round", Contract.KindSlots, 480, 480), Package("OpenDash 800 round", Contract.KindSlots, 800, 800) };
            // The legacy card face is not among them: its row leads with its own size, and opens there.
            Assert.Equal(-1, PanelAddScreen.OpensOn(rounds, 1280, 480));
            var choices = PanelAddScreen.EditSizes(rounds, 1280, 480);
            Assert.Equal(3, choices.Count);
            Assert.Null(choices[0]);
            Assert.Same(rounds[0], choices[1]);
            // Nothing picked, or its own size picked again, is no resize, and so no rename becomes one.
            Assert.False(PanelAddScreen.Resizes(null, 1280, 480));
            Assert.False(PanelAddScreen.Resizes(choices[0], 1280, 480));
            Assert.Equal(ScreenEdit.Rename, PanelAddScreen.Edit("Round", "Dial", PanelAddScreen.Resizes(null, 1280, 480)));
            // A size picked is.
            Assert.True(PanelAddScreen.Resizes(choices[2], 1280, 480));

            // A screen at a size offered opens on it, and the row is the sizes alone.
            Assert.Equal(1, PanelAddScreen.OpensOn(rounds, 800, 800));
            Assert.Same(rounds, PanelAddScreen.EditSizes(rounds, 800, 800));
            Assert.False(PanelAddScreen.Resizes(rounds[1], 800, 800));
            Assert.True(PanelAddScreen.Resizes(rounds[0], 800, 800));

            // The sheet holds no answer until one is picked, and Save asks the model.
            var screens = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.cs"));
            Assert.Contains("PanelAddScreen.EditSizes(PanelAddScreen.Offered(type), screen.Width, screen.Height)", screens);
            Assert.Contains("PanelAddScreen.Resizes(entry, screen.Width, screen.Height)", screens);
            Assert.DoesNotContain("?? offered[0]", screens);
        }

        /// <summary>
        /// The three lines the edit panel can leave behind, each naming what the driver is now waiting for.
        /// </summary>
        /// <remarks>
        /// SimHub reads its dashboard list once, at startup, so a name that has just reached the disk is
        /// not yet a name in Dash Studio. Saying so is the difference between a rename that looks broken
        /// and one that is merely pending.
        /// </remarks>
        [Fact]
        public void What_is_said_after_an_edit_names_the_restart()
        {
            var renamed = PanelAddScreen.Renamed("Wheel");
            Assert.Contains("Wheel", renamed);
            Assert.Contains("Restart SimHub", renamed);

            var reinstalled = PanelAddScreen.Reinstalled("Rim");
            Assert.Contains("Rim", reinstalled);
            Assert.Contains("Restart SimHub", reinstalled);

            // A failure names the screen and points at the log rather than repeating the installer's reason,
            // which can be a sentence of its own in the settings model's words, and claims nothing about
            // restarting.
            var slots = "This build ships no package for a 1280 × 480 slots.";
            Assert.Equal("Could not install Rim's dashboard. See SimHub's log.", PanelAddScreen.ReinstallFailed("Rim", slots));
            Assert.DoesNotContain("slots", PanelAddScreen.ReinstallFailed("Rim", slots));
            Assert.DoesNotContain("Restart SimHub", PanelAddScreen.ReinstallFailed("Rim", slots));
            // A rename whose dashboard could not be installed did happen: the card says Wheel, and the
            // line has to admit the half that did not land rather than report a plain failure, from the
            // name the screen now has, as the success line reads.
            Assert.Equal("Renamed to Wheel, but its dashboard could not be installed. See SimHub's log.", PanelAddScreen.RenameFailed("Wheel"));
            Assert.StartsWith("Renamed to Wheel", PanelAddScreen.Renamed("Wheel"));
        }

        /// <summary>The reinstall says what it costs, and says more when there is something to lose.</summary>
        [Fact]
        public void The_reinstall_says_what_it_replaces()
        {
            Assert.DoesNotContain("edited", PanelAddScreen.ReinstallCaption);
            // The Updates page's own promise, in the same words, because it is the same copy and the same
            // button that puts it back.
            Assert.Contains("a copy is kept", PanelAddScreen.ReinstallEditedCaption);
            Assert.Contains("Put mine back", PanelAddScreen.ReinstallEditedCaption);
            // Where "Put mine back" is now that the tabs have gone (#503).
            Assert.EndsWith("\"Put mine back\" on the Updates page restores it.", PanelAddScreen.ReinstallEditedCaption);
        }
    }
}
