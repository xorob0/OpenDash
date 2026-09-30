// PanelScreensTests.cs: the Screens page's decisions and words -- what a card says, the zone list a face is
// configured through, the rows each kind draws and what search finds -- and first, when the page tells a
// driver to keep or remove the screens they did not choose.
//
// The line is for a rig the migration made, and whether a screen is one of those is carried by the screen
// rather than guessed from how many the rig holds (#478). What is held here is that fact through every path
// that sets it: a rig added by hand, a rig migrated from a settings file older than ADR 0017, the driver
// keeping or removing its screens, and a rig a released build migrated before the fact was recorded. The
// settings go through Json.NET, which is what SimHub writes and reads them with.
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelScreensTests
    {
        [Fact]
        public void A_rig_of_five_screens_added_on_the_screens_page_is_never_told_to_remove_any()
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
        public void A_rig_a_released_build_wrote_from_screens_added_on_the_screens_page_is_never_told()
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

        /// <summary>The page's title, the anchors its rows carry, and what search finds on it.</summary>
        [Fact]
        public void The_screens_page_is_titled_and_its_rows_are_found_where_they_are()
        {
            Assert.Equal("Screens", PanelScreens.Title);
            // The empty rig's words, which Home's card says too.
            Assert.Equal("No screens yet", PanelScreens.NoScreens);
            Assert.Equal("Add the screen your rig has.", PanelCopy.EmptyRig);
            // A failure points at the log (voice.md), in the words its siblings use.
            Assert.Equal("Could not duplicate Rim. See SimHub's log.", PanelAddScreen.DuplicateFailed("Rim"));
            var anchors = AnchorTable.Of(typeof(PanelScreens)).Select(line => line.Substring(line.IndexOf(" = ", System.StringComparison.Ordinal) + 3)).ToArray();
            Assert.All(anchors, anchor => Assert.StartsWith("screens.", anchor, System.StringComparison.Ordinal));
            Assert.Equal(anchors.Length, anchors.Distinct().Count());
            Assert.All(PanelScreens.Search, entry => Assert.Contains(entry.Route.Anchor, anchors));
            Assert.Equal(PanelScreens.Search.Length, PanelScreens.Search.Select(entry => entry.Label).Distinct().Count());
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelScreens.Title && entry.Route.Anchor == PanelScreens.AnchorCards);
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelAddScreen.AddButton);
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelScreens.RevBarTitle && entry.Route.Anchor == PanelScreens.AnchorRevBar);
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelScreens.RevRingTitle && entry.Route.Anchor == PanelScreens.AnchorRevRing);
            // Every row title and heading the page draws is a search label.
            foreach (var label in new[]
            {
                PanelScreens.RevBarTitle, PanelScreens.FlagDisplayTitle, PanelScreens.LapReviewTitle, PanelShortcuts.QuickGlanceTitle,
                PanelScreens.InfoBarTitle, PanelScreens.NextPageTitle, PanelScreens.PreviousPageTitle, PanelScreens.ClassOnlyTitle,
                PanelScreens.DetailsTitle, PanelScreens.PitWallPageTitle, PanelScreens.WebViewTitle, PanelScreens.PortraitTitle,
                PanelScreens.ModulesTitle, PanelScreens.FirstModuleTitle, PanelScreens.NextModuleTitle, PanelScreens.CardsTitle,
                PanelScreens.RevRingTitle, PanelScreens.ZonesTitle,
            })
            {
                Assert.Contains(PanelScreens.Search, entry => entry.Label == label);
            }
            // The rows' titles, which the panes draw and search lists by the same constants.
            Assert.Equal("Zones", PanelScreens.ZonesTitle);
            Assert.Equal("Modules", PanelScreens.ModulesTitle);
            Assert.Equal("First module", PanelScreens.FirstModuleTitle);
            Assert.Equal("Next module", PanelScreens.NextModuleTitle);
            // The round pane's heading is the artboard's noun, never the settings model's "Slots".
            Assert.Equal("Cards", PanelScreens.CardsTitle);
            // "slots" still finds the round screen's cards, and "revbar" the rev bar.
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelScreens.CardsTitle && System.Array.IndexOf(entry.Keywords, "slots") >= 0);
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelScreens.RevBarTitle && System.Array.IndexOf(entry.Keywords, "revbar") >= 0);
            // The round pane's Rev ring writes the rig-wide setting, and its caption names everything that
            // follows it: the round screens, the phone's speedo, and a face that never set its own.
            Assert.Equal("Every round screen, the phone's speedo, and any screen whose own rev bar you have not set.", PanelScreens.RigRevBarCaption);
            var round = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.Round.cs"));
            Assert.Contains("PanelScreens.RigRevBarCaption,", round);
            Assert.Contains("Settings.SetRevBar(value);", round);
            // The note an upgrading user meets, in the noun and verbs Home uses.
            Assert.Equal("Keep or remove each screen an older OpenDash made.", PanelScreens.UnclaimedNote);
        }

        /// <summary>
        /// A card's three states and the fix box under the two that need one: one phrase for one state, a
        /// missing folder before a waiting restart, since restarting will not bring a folder back (ruling 27).
        /// </summary>
        [Fact]
        public void A_card_says_whether_SimHub_has_its_screen_in_the_words_the_fix_box_uses()
        {
            Assert.Equal(ScreenState.InSimHub, PanelScreens.StateOf(true, false));
            Assert.Equal(ScreenState.Restart, PanelScreens.StateOf(true, true));
            Assert.Equal(ScreenState.Missing, PanelScreens.StateOf(false, true));
            Assert.Equal(ScreenState.Missing, PanelScreens.StateOf(false, false));
            Assert.Equal("In SimHub", PanelScreens.StateLabel(ScreenState.InSimHub));
            Assert.Equal("Restart SimHub to load it", PanelScreens.StateLabel(ScreenState.Restart));
            Assert.Equal("Missing", PanelScreens.StateLabel(ScreenState.Missing));
            Assert.Equal(Theme.StatusUpToDate, PanelScreens.StateHex(ScreenState.InSimHub));
            Assert.Equal(Theme.Caution, PanelScreens.StateHex(ScreenState.Restart));
            Assert.Equal(Theme.StatusFailed, PanelScreens.StateHex(ScreenState.Missing));
            Assert.Equal(PanelScreens.RestartToLoad, PanelScreens.StateLabel(ScreenState.Restart));
            Assert.Equal("Then assign \"Rim\" to this display in Dash Studio.", PanelScreens.RestartDetail("Rim"));
            Assert.Equal("This screen's dashboard is missing from SimHub", PanelScreens.MissingTitle);
            Assert.Equal("Its settings are kept.", PanelAttention.MissingDetail);
            Assert.Equal("Install it again", PanelAttention.InstallAgain);
            Assert.Equal("Puts this screen's dashboard back into SimHub.", PanelScreens.InstallAgainTooltip);
        }

        /// <summary>A card says the kind, and the header beside the name says the kind and the size.</summary>
        [Fact]
        public void A_card_names_the_kind_and_the_header_the_kind_and_size()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var rim = settings.AddScreen(Entry("OpenDash 1280x480", Contract.KindFace, 1280, 480), "Rim");
            var round = settings.AddScreen(Entry("OpenDash 480 round", Contract.KindSlots, 480, 480), "Round");
            Assert.Equal("Face", PanelScreens.CardMeta(rim));
            Assert.Equal("Face · 1280 × 480", PanelScreens.Facts(rim));
            Assert.Equal("Round", PanelScreens.CardMeta(round));
            Assert.Equal("Round · 480 × 480", PanelScreens.Facts(round));
            var gone = new ScreenInstance { Kind = Contract.KindPitWall, Name = "Wall" };
            Assert.Equal("Pit wall", PanelScreens.Facts(gone));
            Assert.Equal(string.Empty, PanelScreens.Facts(null));
        }

        /// <summary>The header's presses and the remove sheet: what removing costs, and the bound buttons
        /// that stop working where the kind has actions of its own.</summary>
        [Fact]
        public void Removing_says_what_it_costs()
        {
            Assert.Equal("Edit", PanelScreens.EditButton);
            Assert.Equal("Duplicate", PanelScreens.DuplicateButton);
            Assert.Equal("Remove", PanelScreens.RemoveButton);
            Assert.Equal("Adds a second screen set up like this one.", PanelScreens.DuplicateTooltip);
            Assert.Equal("Change this screen's name or size, or install its dashboard again.", PanelScreens.EditTooltip);
            Assert.Equal("Removes this screen, its settings and its dashboard.", PanelScreens.RemoveTooltip);
            Assert.Equal("Leaves this screen alone.", PanelScreens.KeepTooltip);
            Assert.Equal("Remove Rim", PanelScreens.RemoveTitle("Rim"));
            Assert.Equal("Removes the screen, its dashboard and its settings. Any wheel button you bound to it stops working.", PanelScreens.RemoveBody(true));
            Assert.Equal("Removes the screen, its dashboard and its settings.", PanelScreens.RemoveBody(false));
            Assert.True(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.True(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindPitWall }));
            Assert.True(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindCompanion }));
            Assert.False(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindSlots }));
            Assert.Equal("Keep it", PanelScreens.KeepButton);
            Assert.Equal("Remove it", PanelScreens.RemoveItButton);
            Assert.Equal("Removed Rim. SimHub still lists its dashboard until you restart it.", PanelScreens.Removed("Rim"));
            Assert.Equal("Removed Rim, but its dashboard could not be deleted: locked", PanelScreens.RemoveFailed("Rim", "locked"));
        }

        /// <summary>A face's rows, in voice.md's words where the artboard's differ (findings 10 to 15).</summary>
        [Fact]
        public void A_faces_rows_are_named_as_voice_md_names_them()
        {
            Assert.Equal("Rev bar", PanelScreens.RevBarTitle);
            Assert.Equal("Flag display", PanelScreens.FlagDisplayTitle);
            Assert.Equal(new[] { "Band D", "Full screen" }, PanelScreens.FlagLabels);
            Assert.Equal(Contract.FlagFormats.Length, PanelScreens.FlagLabels.Length);
            Assert.Equal(new[] { "Off", "Bar", "Full screen" }, PanelScreens.BarFlagLabels);
            Assert.Equal(Contract.CompanionFlagFormats.Length, PanelScreens.BarFlagLabels.Length);
            Assert.Equal("Lap review", PanelScreens.LapReviewTitle);
            Assert.Equal(new[] { "Off", "Races", "Always" }, PanelScreens.LapReviewLabels);
            Assert.Equal(Contract.LapReviewModes.Length, PanelScreens.LapReviewLabels.Length);
            Assert.Equal("Shows your last lap for four seconds after the line.", PanelScreens.LapReviewCaption);
            Assert.Equal("OpenDash no longer ships a 1024 × 600 face. Your settings are kept.", PanelScreens.NoLongerShipped("1024 × 600"));
            // The seven greyed rows are the registry's, in the artboard's order, with voice.md's noun phrases.
            Assert.Equal(
                new[] { "Rev fill under the lights", "Spotter at the rev bar ends", "Pit page in the pit lane", "Pop-ups", "Edge lights for the delta", "Screen care", "Fit" },
                new[] { PanelSoon.RevFill, PanelSoon.SpotterAtRevBarEnds, PanelSoon.PitPageInPitLane, PanelSoon.PopUps, PanelSoon.DeltaEdgeLights, PanelSoon.ScreenCare, PanelSoon.Fit }.Select(item => item.Title));
            var face = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.Face.cs"));
            var order = new[] { "PanelSoon.RevFill", "PanelSoon.SpotterAtRevBarEnds", "PanelSoon.PitPageInPitLane", "PanelSoon.PopUps", "PanelSoon.DeltaEdgeLights", "PanelSoon.ScreenCare", "PanelSoon.Fit" }
                .Select(name => face.IndexOf(name, System.StringComparison.Ordinal)).ToArray();
            Assert.All(order, at => Assert.True(at >= 0));
            Assert.Equal(order.OrderBy(at => at), order);
        }

        /// <summary>The picture's bar and its aside: two fields an end on a wide face, one on the portrait, none
        /// on the nano; the middle is the car's settings, and the aside opens on what was last picked.</summary>
        [Fact]
        public void The_info_bar_lists_the_fields_the_face_has()
        {
            var reference = Contract.ReferenceFace;
            var portrait = Contract.FaceSizes.Single(f => f.Body == Contract.FaceBody.Column);
            var nano = Contract.FaceSizes.Single(f => !f.HasBar);
            Assert.Equal(new[] { "Left1", "Left2", "Right1", "Right2" }, PanelScreens.BarRows(reference).Select(r => r.Slot));
            Assert.Equal(new[] { "Left, first", "Left, second", "Right, first", "Right, second" }, PanelScreens.BarRows(reference).Select(r => r.Label));
            Assert.Equal(new[] { "Left1", "Right1" }, PanelScreens.BarRows(portrait).Select(r => r.Slot));
            Assert.Equal(new[] { "Left", "Right" }, PanelScreens.BarRows(portrait).Select(r => r.Label));
            Assert.Empty(PanelScreens.BarRows(nano));
            Assert.All(PanelScreens.BarRows(reference), row => Assert.Contains(row.Slot, Contract.BarSlots));

            var settings = new FaceSettings();
            settings.Normalise();
            Assert.Equal("Race time · Lap", PanelScreens.BarEnd(settings, reference, true));
            Assert.Equal("Position · Class", PanelScreens.BarEnd(settings, reference, false));
            Assert.Equal("Race time", PanelScreens.BarEnd(settings, portrait, true));
            Assert.Equal("Info bar", PanelScreens.InfoBarTitle);
            Assert.Equal("Car settings", PanelScreens.InfoBarMiddle);

            Assert.Equal("B", PanelScreens.AsideKey(null, reference));
            Assert.Equal("A", PanelScreens.AsideKey(null, portrait));
            Assert.Equal("C", PanelScreens.AsideKey("C", reference));
            Assert.Equal(PanelScreens.BarKey, PanelScreens.AsideKey(PanelScreens.BarKey, reference));
            Assert.Equal("B", PanelScreens.AsideKey(PanelScreens.BarKey, nano));
            Assert.Equal("B", PanelScreens.AsideKey("Z", reference));
        }

        /// <summary>A zone in the picture: how many pages it cycles, the page it opens on, and the button that
        /// advances it -- or "No button", or nothing when the bindings cannot be read.</summary>
        [Fact]
        public void A_zone_in_the_picture_counts_its_pages_and_names_its_button()
        {
            var settings = new FaceSettings();
            settings.Normalise();
            var zoneA = FacePages.For("A").Count;
            Assert.Equal(zoneA + " of " + zoneA, PanelScreens.ZoneCount(settings, "A"));
            settings.SetPageEnabled("A", 1, false);
            Assert.Equal((zoneA - 1) + " of " + zoneA, PanelScreens.ZoneCount(settings, "A"));
            Assert.Equal(string.Empty, PanelScreens.ZoneButtonLine(null));
            Assert.Equal("No button", PanelScreens.ZoneButtonLine(new string[0]));
            Assert.Equal(PanelBindings.ChipText(new[] { "Keyboard.F5" }), PanelScreens.ZoneButtonLine(new[] { "Keyboard.F5" }));
        }

        /// <summary>
        /// A zone's list: the ticked pages in the zone's cycle from the page it opens on, which is the First,
        /// then under Show all the rest; the last ticked page locked; Energy, Damage and Track rivals said to
        /// be empty in iRacing and still tickable (rulings 29 and 30).
        /// </summary>
        [Fact]
        public void A_zones_list_is_its_ticked_pages_in_order_and_the_first_is_where_it_opens()
        {
            var settings = new FaceSettings();
            settings.Normalise();
            settings.SetOrder("A", new[] { 2, 0, 1, 3 });
            settings.SetPageEnabled("A", 1, false);
            // The order's head is 2, but the zone opens on 0: the cycle is read from there, and wraps.
            Assert.Equal(0, settings.Start("A"));
            var rows = PanelScreens.ZoneRows(settings, "A", false);
            Assert.Equal(new[] { 0, 3, 2 }, rows.Select(r => r.Page));
            Assert.Equal(new[] { true, false, false }, rows.Select(r => r.First));
            Assert.All(rows, r => Assert.True(r.Ticked));
            Assert.All(rows, r => Assert.False(r.Locked));
            Assert.Equal(settings.Start("A"), PanelScreens.FirstTicked(settings, "A"));

            var all = PanelScreens.ZoneRows(settings, "A", true);
            Assert.Equal(new[] { 0, 3, 2, 1 }, all.Select(r => r.Page));
            Assert.False(all[3].Ticked);
            Assert.Equal("Gear alone", all[3].Name);

            // Unticking the start moves it on to the next ticked page in the cycle, which the list then draws first.
            settings.SetPageEnabled("A", 0, false);
            Assert.Equal(3, settings.Start("A"));
            Assert.Equal(3, PanelScreens.ZoneRows(settings, "A", false)[0].Page);
            settings.SetPageEnabled("A", 3, false);
            var one = PanelScreens.ZoneRows(settings, "A", false);
            Assert.Single(one);
            Assert.True(one[0].Locked);
            Assert.Equal(settings.Start("A"), one[0].Page);
            Assert.Equal("A zone keeps at least one page.", PanelScreens.LastPageTooltip);

            var zoneB = PanelScreens.ZoneRows(settings, "B", true);
            Assert.Equal(new[] { "Energy", "Damage", "Track rivals" }, zoneB.Where(r => r.NotInIracing).Select(r => r.Name));
            Assert.True(PanelScreens.IsNotInIracing("energy"));
            Assert.False(PanelScreens.IsNotInIracing("fuel"));
            Assert.False(PanelScreens.IsNotInIracing(null));
            Assert.True(PanelScreens.ListsSoonModules("B") && PanelScreens.ListsSoonModules("C"));
            Assert.False(PanelScreens.ListsSoonModules("A") || PanelScreens.ListsSoonModules("D"));
            Assert.Equal(new[] { "Show all", "Only ticked", "All", "None", "Drag to reorder", "First", "Not in iRacing", "My class only", "Next page", "Previous page" },
                new[] { PanelScreens.ShowAll, PanelScreens.OnlyTicked, PanelScreens.AllPages, PanelScreens.NoPages, PanelScreens.DragHint, PanelScreens.FirstTag, PanelScreens.NotInIracing, PanelScreens.ClassOnlyTitle, PanelScreens.NextPageTitle, PanelScreens.PreviousPageTitle });
        }

        /// <summary>
        /// The picture and the list name the page the dash opens on. A default face's zone C opens on
        /// Relative while its stored order begins at Lap times; drawing the order's head called Lap times
        /// First, and the first tick in the zone then wrote it into the start and the running zone.
        /// </summary>
        [Fact]
        public void A_zone_is_drawn_opening_on_its_start_and_a_tick_does_not_move_it()
        {
            var settings = new FaceSettings();
            settings.Normalise();
            foreach (var letter in Contract.FaceZoneLetters)
            {
                Assert.Equal(settings.Start(letter), PanelScreens.FirstTicked(settings, letter));
                Assert.Equal(settings.Start(letter), PanelScreens.ZoneRows(settings, letter, false)[0].Page);
                Assert.True(PanelScreens.ZoneRows(settings, letter, true)[0].First);
            }
            Assert.Equal(14, settings.Start("C"));
            Assert.Equal("Relative", FacePages.NameOf("C", PanelScreens.FirstTicked(settings, "C")));

            // Unticking a page other than the first leaves the start, and the page the zone is showing, alone.
            settings.Cycle("C");
            var showing = settings.Zone("C");
            var energy = FacePages.For("C").First(p => p.Id == "energy").Number;
            settings.SetPageEnabled("C", energy, false);
            Assert.Equal(14, settings.Start("C"));
            Assert.Equal(showing, settings.Zone("C"));
            settings.SetPageEnabled("C", energy, true);
            Assert.Equal(14, settings.Start("C"));

            // None keeps the page the zone opens on, and All leaves the start where it was.
            PanelScreens.SetEveryPage(settings, "C", false);
            Assert.Equal(new[] { 14 }, PanelScreens.ZoneRows(settings, "C", false).Select(r => r.Page));
            PanelScreens.SetEveryPage(settings, "C", true);
            Assert.Equal(14, settings.Start("C"));
            Assert.Equal(FacePages.For("C").Count, PanelScreens.ZoneRows(settings, "C", false).Count);
        }

        /// <summary>A drag in the list moves the page it drew, and every page the list did not draw keeps its
        /// place after them, so the zone's order stays whole; the order is written from the first ticked page,
        /// and the start moves only when the drag put another page first.</summary>
        [Fact]
        public void A_drag_in_the_list_reorders_the_zone_and_keeps_its_order_whole()
        {
            var settings = new FaceSettings();
            settings.Normalise();
            settings.SetPageEnabled("A", 1, false);
            // Drawn: 0, 2, 3 (1 is not ticked). Drag the last to the top.
            Assert.Equal(new[] { 3, 0, 2, 1 }, PanelScreens.Reordered(settings, "A", false, 2, 0));
            // Drawn under Show all: 0, 2, 3, 1. An unticked page dragged over the ticked ones cannot open the
            // zone, so the order is still written from the first ticked page and 1 lands before it in the cycle.
            Assert.Equal(new[] { 0, 2, 3, 1 }, PanelScreens.Reordered(settings, "A", true, 3, 0));

            // A drag that keeps the first page first leaves the start and the running zone where they are.
            settings.Cycle("A");
            var showing = settings.Zone("A");
            PanelScreens.Reorder(settings, "A", false, 1, 2);
            Assert.Equal(new[] { 0, 3, 2 }, PanelScreens.ZoneRows(settings, "A", false).Select(r => r.Page));
            Assert.Equal(0, settings.Start("A"));
            Assert.Equal(showing, settings.Zone("A"));

            // One that puts another page first makes it the start, and the order's head.
            PanelScreens.Reorder(settings, "A", false, 2, 0);
            Assert.Equal(2, settings.Start("A"));
            Assert.Equal(2, PanelScreens.FirstTicked(settings, "A"));
            Assert.Equal(2, settings.Order("A")[0]);
            Assert.Equal(new[] { 2, 0, 3 }, PanelScreens.ZoneRows(settings, "A", false).Select(r => r.Page));

            var whole = PanelScreens.Reordered(settings, "B", false, 0, 5);
            Assert.Equal(FacePages.For("B").Count, whole.Length);
            Assert.Equal(whole.Length, whole.Distinct().Count());
        }

        /// <summary>The quick glance is picked zone first, then only the pages that zone carries (ruling 32); a
        /// new zone keeps the page where it has it and opens on its first otherwise.</summary>
        [Fact]
        public void The_glance_offers_only_the_pages_its_zone_carries()
        {
            Assert.Equal(new[] { "Zone A", "Zone B", "Zone C", "Band D" }, PanelScreens.GlanceZoneLabels());
            Assert.Equal(FacePages.ZoneA.Select(p => p.Name), PanelScreens.GlancePageLabels(0));
            Assert.Equal(FacePages.BandD.Select(p => p.Name), PanelScreens.GlancePageLabels(3));
            // Zone C's Track is zone A's Track: one drawing.
            var track = Contract.QuickGlanceValue(2, FacePages.ZoneBC.First(p => p.Id == "track").Number);
            Assert.Equal(Contract.QuickGlanceValue(0, 3), PanelScreens.GlanceWithZone(track, 0));
            // Band D carries no Track, so it opens on its first page.
            Assert.Equal(Contract.QuickGlanceValue(3, 0), PanelScreens.GlanceWithZone(track, 3));
            // Relative is in B and C and in band D.
            var relative = Contract.QuickGlanceValue(1, FacePages.ZoneBC.First(p => p.Id == "relative").Number);
            Assert.Equal(Contract.QuickGlanceValue(3, 6), PanelScreens.GlanceWithZone(relative, 3));
            Assert.Equal(new[] { "Race A", "Race B", "Tower A", "Tower B", "Telemetry A", "Telemetry B", "Telemetry C" }, PanelScreens.PitWallGlanceZoneLabels());
        }

        /// <summary>Details name what a reader could type exactly: the name SimHub lists, the folder as stored,
        /// and the properties by the frozen namespace, never derived from the name (ruling 38).</summary>
        [Fact]
        public void Details_name_the_properties_by_the_namespace()
        {
            Assert.Equal("OpenDash.Face1280x480*", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindFace, Namespace = "Face1280x480", Name = "Rim" }));
            Assert.Equal("OpenDash.PitWall* and OpenDash.WebViewUrl", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindPitWall, Namespace = Contract.PitWallPrefix }));
            Assert.Equal("OpenDash.Garage*", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindPitWall, Namespace = "Garage" }));
            Assert.Equal("OpenDash.Slot01 to OpenDash.Slot02", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 }));
            Assert.Equal("OpenDash.Slot01 to OpenDash.Slot06", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindSlots, Width = 800, Height = 800 }));
            Assert.Equal(string.Empty, PanelScreens.Properties(null));
            Assert.Equal(new[] { "Details", "SimHub name", "Folder", "Properties", "Version", "Not installed" },
                new[] { PanelScreens.DetailsTitle, PanelScreens.SimHubNameLabel, PanelScreens.FolderLabel, PanelScreens.PropertiesLabel, PanelScreens.VersionLabel, PanelScreens.NotInstalled });
        }

        /// <summary>A pit wall: the page on screen picks the zones the list names; a wall on end draws the
        /// portrait layout's four instead (ruling 34), each choice naming its zone.</summary>
        [Fact]
        public void A_pit_wall_lists_the_zones_of_the_page_on_screen()
        {
            Assert.Equal("Page on screen", PanelScreens.PitWallPageTitle);
            Assert.Equal(new[] { "RaceA", "RaceB" }, PanelScreens.PitWallZones(0).Select(s => s.Key));
            Assert.Equal(new[] { "TowerWide", "TowerA", "TowerB" }, PanelScreens.PitWallZones(1).Select(s => s.Key));
            Assert.Equal(new[] { "TelemetryA", "TelemetryB", "TelemetryC" }, PanelScreens.PitWallZones(2).Select(s => s.Key));
            Assert.Equal(new[] { "Wide zone", "Zone A", "Zone B" }, PanelScreens.PitWallZones(1).Select(PanelScreens.PitWallZoneLabel));
            Assert.Equal("Tower zones", PanelScreens.PitWallZonesLabel("Tower"));
            Assert.Equal(new[] { "PortraitA", "PortraitB", "PortraitC", "PortraitD" }, PanelScreens.PortraitZones().Select(s => s.Key));
            Assert.Equal("A · Fuel", PanelScreens.PortraitLabels("A")[0]);
            Assert.Equal(ZonePages.Standard.Count, PanelScreens.PortraitLabels("D").Length);
            Assert.True(PanelScreens.IsPortrait(new ScreenInstance { Width = 1080, Height = 1920 }));
            Assert.False(PanelScreens.IsPortrait(new ScreenInstance { Width = 1920, Height = 1080 }));
            Assert.Equal("Portrait layout", PanelScreens.PortraitTitle);
            Assert.Equal("Web view address", PanelScreens.WebViewTitle);
            Assert.Equal("http or https only.", PanelScreens.WebViewEmptyTooltip);
        }

        /// <summary>A companion's count, and a round screen's cards: one per slot its package reads.</summary>
        [Fact]
        public void A_companion_counts_its_modules_and_a_round_screen_its_cards()
        {
            var modules = Modules.Defaults();
            Assert.Equal("18 of 21", PanelScreens.ModuleCount(modules));
            Assert.Equal("0 of 21", PanelScreens.ModuleCount(null));
            Assert.Equal(Modules.All.Select(m => m.Name), PanelScreens.ModuleNames());
            // A module's hover is its number and what it shows.
            Assert.Equal("01 · " + Modules.All[0].Description, PanelScreens.ModuleTooltip(Modules.All[0]));
            Assert.Equal("21 · " + Modules.All[20].Description, PanelScreens.ModuleTooltip(Modules.All[20]));
            Assert.Equal(string.Empty, PanelScreens.ModuleTooltip(null));
            Assert.Equal(new[] { "Controls and events", "NextScreen" }, PanelScreens.CompanionPagingCrumbs);
            Assert.All(PanelScreens.CompanionPagingCrumbs, crumb => Assert.Contains(crumb, PanelCopy.CompanionPaging));
            Assert.Equal(2, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 }));
            Assert.Equal(6, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 800, Height = 800 }));
            Assert.Equal(Contract.SlotCount, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 1280, Height = 480 }));
            Assert.Equal("Card 1", PanelScreens.CardLabel(1));

            // A 480 round reads two cards, so the line is about those two and says "Card", as its rows do. The
            // default slots are twelve different cards, and picking the third's for Card 1 is not a clash on a
            // screen that never draws the third.
            var settings = new OpenDashSettings();
            settings.Normalise();
            Assert.Equal(string.Empty, PanelScreens.CardClash(settings.Slots, 2));
            settings.SetSlot(1, settings.Slot(3));
            Assert.Equal(string.Empty, PanelScreens.CardClash(settings.Slots, 2));
            Assert.Equal(Cards.DisplayName(settings.Slot(3)) + " is on Card 1 and Card 3.", PanelScreens.CardClash(settings.Slots, 6));
            settings.SetSlot(2, settings.Slot(1));
            Assert.Equal(Cards.DisplayName(settings.Slot(1)) + " is on Card 1 and Card 2.", PanelScreens.CardClash(settings.Slots, 2));
            Assert.Equal(Cards.DisplayName(settings.Slot(1)) + " is on Card 1, Card 2 and Card 3.", PanelScreens.CardClash(settings.Slots, 6));
            Assert.Equal(string.Empty, PanelScreens.CardClash(null, 2));
            Assert.Equal("Rev ring", PanelScreens.RevRingTitle);
            Assert.Equal("Cards are shared by every round screen.", PanelScreens.CardsCaption);
        }

        /// <summary>The greyed rows the page draws: the Screens page's twelve registry entries, two of them only
        /// inside the Add sheet.</summary>
        [Fact]
        public void The_page_draws_every_greyed_row_the_registry_gives_it()
        {
            Assert.Equal(PanelSoon.For(PanelPage.Screens).Select(item => item.Anchor).OrderBy(a => a, System.StringComparer.Ordinal),
                PanelScreens.SoonDrawn.Select(item => item.Anchor).OrderBy(a => a, System.StringComparer.Ordinal));
            Assert.Equal(new[] { PanelSoon.FlagsScreen, PanelSoon.YourDisplays }, PanelScreens.SoonDrawn.Where(item => item.InSheetOnly));
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorCards = screens.cards",
                "AnchorClassOnly = screens.class-only",
                "AnchorDetails = screens.details",
                "AnchorFirstModule = screens.first-module",
                "AnchorFlagDisplay = screens.flag-display",
                "AnchorGlance = screens.glance",
                "AnchorLapReview = screens.lap-review",
                "AnchorModules = screens.modules",
                "AnchorPaging = screens.paging",
                "AnchorPitWallPage = screens.pitwall-page",
                "AnchorPortrait = screens.portrait",
                "AnchorRevBar = screens.revbar",
                "AnchorRevRing = screens.rev-ring",
                "AnchorSlots = screens.slots",
                "AnchorWebView = screens.webview",
                "AnchorZones = screens.zones",
            }, AnchorTable.Of(typeof(PanelScreens)));
        }
    }
}
