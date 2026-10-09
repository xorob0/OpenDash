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
            Assert.Equal("No screens yet.", PanelScreens.NoScreens);
            Assert.Equal("Add the screen your rig has.", PanelCopy.EmptyRig);
            // A failure points at the log (voice.md), in the words its siblings use.
            Assert.Equal("Could not duplicate Rim. See SimHub's log.", PanelAddScreen.DuplicateFailed("Rim"));
            var anchors = AnchorTable.Of(typeof(PanelScreens)).Select(line => line.Substring(line.IndexOf(" = ", System.StringComparison.Ordinal) + 3)).ToArray();
            Assert.All(anchors, anchor => Assert.StartsWith("screens.", anchor, System.StringComparison.Ordinal));
            Assert.Equal(anchors.Length, anchors.Distinct().Count());
            Assert.All(PanelScreens.Search, entry => Assert.Contains(entry.Route.Anchor, anchors));
            Assert.Equal(PanelScreens.Search.Length, PanelScreens.Search.Select(entry => entry.Label).Distinct().Count());
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelScreens.Title && entry.Route.Anchor == PanelScreens.AnchorCards);
            // Adding a screen is found by the dashed tile's words and lands where Home's empty-rig tile does, on
            // the add tile, which opens the sheet; the sheet's own button, which search cannot press, is no label.
            Assert.Contains(PanelScreens.Search, entry => entry.Label == PanelAddScreen.SectionTitle && entry.Route.Anchor == PanelScreens.AnchorAdd);
            Assert.DoesNotContain(PanelScreens.Search, entry => entry.Label == PanelAddScreen.AddButton);
            Assert.Equal(PanelScreens.AnchorAdd, PanelSearch.Find(PanelScreens.Search, "add screen").First().Route.Anchor);
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
            // follows it: the round screens, a face that never set its own, and the Speedo module wherever it
            // is drawn, a face's zone B or C included.
            Assert.Equal("Every round screen, the speedo wherever it is shown and any face whose own rev bar you have not set.", PanelScreens.RigRevBarCaption);
            Assert.Contains(FacePages.For("B"), page => page.Name == "Speedo");
            Assert.Contains(Modules.All, module => module.Name == "Speedo");
            var round = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.Round.cs"));
            Assert.Contains("caption: PanelScreens.RigRevBarCaptionFor(Settings.RigScreens()),", round);
            Assert.Contains("Ui.Prose(PanelScreens.CardsCaptionFor(Settings.RigScreens()))", round);
            // A card face reads the same cards and the same rev bar, and draws this editor: where the rig holds
            // one, both captions name it, and a rig with none is not told about a kind it cannot add.
            var disc = new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 };
            var cardFace = new ScreenInstance { Kind = Contract.KindSlots, Width = 1280, Height = 480 };
            Assert.Equal(PanelScreens.RigRevBarCaption, PanelScreens.RigRevBarCaptionFor(new[] { disc }));
            Assert.Equal(PanelScreens.CardsCaption, PanelScreens.CardsCaptionFor(new[] { disc }));
            Assert.Equal("Every round screen and card face, the speedo wherever it is shown and any face whose own rev bar you have not set.",
                PanelScreens.RigRevBarCaptionFor(new[] { disc, cardFace }));
            Assert.Equal("Cards are shared by every round screen and card face.", PanelScreens.CardsCaptionFor(new[] { cardFace }));
            Assert.Contains(PanelScreens.CardFace.ToLowerInvariant(), PanelScreens.CardsCaptionFor(new[] { cardFace }));
            Assert.Equal(PanelScreens.CardsCaption, PanelScreens.CardsCaptionFor(null));
            Assert.Contains("Settings.SetRevBar(value);", round);
            // The note an upgrading user meets, in the noun and verbs Home uses.
            Assert.Equal("Keep or remove each screen an older OpenDash made.", PanelScreens.UnclaimedNote);
        }

        /// <summary>
        /// A card's three states and the fix box under the two that need one: one phrase for one state, a
        /// missing folder before a waiting restart, since restarting will not bring a folder back (#791).
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
            Assert.Equal(PanelCopy.RestartToLoad, PanelScreens.StateLabel(ScreenState.Restart));
            Assert.Equal("Then assign \"Rim\" to its display in Dash Studio.", PanelScreens.RestartDetail("Rim"));
            // The fix box under each state is titled with the card's phrase.
            Assert.Equal(PanelScreens.MissingTitle, PanelScreens.StateLabel(ScreenState.Missing));
            Assert.Equal("Its settings are kept.", PanelAttention.MissingDetail);
            Assert.Equal("Install it again", PanelAttention.InstallAgain);
            // The hover says the button's verb, and a round screen, which has no settings of its own, is not
            // told they are kept.
            Assert.Equal("Installs this screen's dashboard again.", PanelScreens.InstallAgainTooltip);
            // The box's title is one word, so its detail names whose settings it means; Home's issue, whose title
            // names the screen, keeps "Its".
            Assert.Equal("This screen's settings are kept.", PanelScreens.MissingDetailFor(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.Null(PanelScreens.MissingDetailFor(new ScreenInstance { Kind = Contract.KindSlots, Width = 800, Height = 800 }));
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

            // A card face a migration left at a rectangular size is not called round, nor drawn as a ring, nor
            // given a disc: its editor draws its rows alone, and its rev row names the bar it draws.
            var legacy = new ScreenInstance { Kind = Contract.KindSlots, Width = 1280, Height = 480, Name = "Round" };
            Assert.Equal("Card face", PanelScreens.CardMeta(legacy));
            Assert.Equal("Card face · 1280 × 480", PanelScreens.Facts(legacy));
            Assert.False(PanelScreens.IsRound(legacy));
            Assert.True(PanelScreens.IsRound(round));
            Assert.Equal("round", PanelScreens.ThumbKind(round));
            Assert.Equal(Contract.KindSlots, PanelScreens.ThumbKind(legacy));
            Assert.Equal(Contract.KindFace, PanelScreens.ThumbKind(rim));
            Assert.True(PanelScreens.DrawsDisc(round));
            Assert.False(PanelScreens.DrawsDisc(legacy));
            Assert.False(PanelScreens.DrawsDisc(new ScreenInstance { Kind = Contract.KindSlots, Width = 850, Height = 480 }));
            Assert.Equal(PanelScreens.RevRingTitle, PanelScreens.RevRingTitleFor(round));
            Assert.Equal(PanelScreens.RevBarTitle, PanelScreens.RevRingTitleFor(legacy));
            // So a search for "Rev ring" opens a round screen, whose row is called that, and not a card face's
            // Rev bar; a rig with no round screen keeps the card face it has.
            Assert.False(PanelScreens.Draws(PanelScreens.AnchorRevRing, legacy));
            Assert.True(PanelScreens.Draws(PanelScreens.AnchorSlots, legacy));
            Assert.Same(round, PanelScreens.ScreenFor(PanelScreens.AnchorRevRing, new[] { legacy, round }, legacy));
            Assert.Same(legacy, PanelScreens.ScreenFor(PanelScreens.AnchorRevRing, new[] { legacy }, legacy));
            var page = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.cs"));
            Assert.Contains("Ui.Thumb(PanelScreens.ThumbKind(captured)", page);
            var roundEditor = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.Round.cs"));
            Assert.Contains("PanelScreens.DrawsDisc(screen)", roundEditor);
        }

        /// <summary>
        /// The slots each card face reads are its layout's, as the build writes them into each package's
        /// description ("1280 x 480, 8 slots"), read here from the snapshot the build's test records.
        /// </summary>
        [Fact]
        public void A_card_face_reads_the_slots_its_layout_has()
        {
            var snapshot = System.IO.Path.Combine(RepoPaths.Root(), "packages", "dash", "test", "__snapshots__", "snapshots.test.ts.snap");
            var text = System.IO.File.ReadAllText(snapshot);
            var found = new Dictionary<string, int>();
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,
                "\"Description\": \"(?:\\d+ x \\d+, )?(\\d+) slots(?:, round)?\",\\s*\"Author\": \"[^\"]*\",\\s*\"Width\": (\\d+),\\s*\"Height\": (\\d+)"))
            {
                found[match.Groups[2].Value + "x" + match.Groups[3].Value] = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            Assert.Equal(10, found.Count);
            Assert.Equal(found.OrderBy(p => p.Key, System.StringComparer.Ordinal), PanelScreens.CardsReadBySize.OrderBy(p => p.Key, System.StringComparer.Ordinal));
            foreach (var pair in found)
            {
                var size = pair.Key.Split('x');
                var screen = new ScreenInstance { Kind = Contract.KindSlots, Width = int.Parse(size[0], System.Globalization.CultureInfo.InvariantCulture), Height = int.Parse(size[1], System.Globalization.CultureInfo.InvariantCulture) };
                Assert.Equal(pair.Value, PanelScreens.CardsRead(screen));
                Assert.True(pair.Value <= Contract.SlotCount);
            }
        }

        /// <summary>The selected screen's block and its header and Details, at the artboard's numbers: 20 under
        /// the rule, 18 between its parts, 12 between the name and the facts, Details' labels 140 wide, and a
        /// zone list's page 32 high.</summary>
        [Fact]
        public void The_selected_screen_is_laid_out_as_the_canvas_draws_it()
        {
            Assert.Equal(20, PanelScreens.SelectedTop);
            Assert.Equal(18, PanelScreens.SelectedGap);
            Assert.Equal(12, PanelScreens.HeaderGap);
            Assert.Equal(140, PanelScreens.DetailsLabelWidth);
            Assert.Equal(32, PanelScreens.PageRowHeight);
            var page = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.cs"));
            Assert.Contains("Padding = new Thickness(0, PanelScreens.SelectedTop, 0, 0)", page);
            Assert.Contains("Ui.VStack(PanelScreens.SelectedGap, selected.ToArray())", page);
            Assert.Contains("new ScreensNameLine(PanelScreens.HeaderGap)", page);
            Assert.Contains("new GridLength(PanelScreens.DetailsLabelWidth)", page);
        }

        /// <summary>The header's presses and the remove sheet: what removing costs, and the bound buttons
        /// that stop working where the kind has actions of its own.</summary>
        [Fact]
        public void Removing_says_what_it_costs()
        {
            Assert.Equal("Edit", PanelScreens.EditButton);
            Assert.Equal("Duplicate", PanelScreens.DuplicateButton);
            Assert.Equal("Remove", PanelScreens.RemoveButton);
            Assert.Equal("Adds another screen set up like this one.", PanelScreens.DuplicateTooltip);
            Assert.DoesNotContain("second", PanelScreens.DuplicateTooltip);
            Assert.Equal("Changes this screen's name, size or orientation, or reinstalls its dashboard.", PanelScreens.EditTooltip);
            Assert.Contains(PanelAddScreen.ReinstallButton.ToLowerInvariant(), PanelScreens.EditTooltip);
            // The hover and the sheet list what goes in one order, and a round screen, whose cards are the
            // rig's shared slots, has no settings to lose.
            var face = new ScreenInstance { Kind = Contract.KindFace };
            var round = new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 };
            Assert.Equal("Removes this screen, its dashboard and its settings.", PanelScreens.RemoveTooltip);
            Assert.Equal(PanelScreens.RemoveTooltip, PanelScreens.RemoveTooltipFor(face));
            Assert.Equal("Removes this screen and its dashboard.", PanelScreens.RemoveTooltipFor(round));
            // Keep answers the line over the cards, and says keep as the button does.
            Assert.Equal("Keeps this screen.", PanelScreens.KeepTooltip);
            Assert.Equal("Remove Rim", PanelScreens.RemoveTitle("Rim"));
            Assert.Equal("Removes the screen, its dashboard and its settings. Any wheel button you bound to it stops working.", PanelScreens.RemoveBody(face));
            Assert.Equal("Removes the screen and its dashboard.", PanelScreens.RemoveBody(round));
            Assert.Equal("Removes the screen, its dashboard and its settings. Any wheel button you bound to it stops working.",
                PanelScreens.RemoveBody(new ScreenInstance { Kind = Contract.KindCompanion }));
            Assert.True(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.True(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindPitWall }));
            Assert.True(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindCompanion }));
            Assert.False(PanelScreens.HasActions(new ScreenInstance { Kind = Contract.KindSlots }));
            Assert.Equal("Keep it", PanelScreens.KeepButton);
            Assert.Equal("Remove it", PanelScreens.RemoveItButton);
            Assert.Equal("Removed Rim. Restart SimHub to take its dashboard out of Dash Studio.", PanelScreens.Removed("Rim"));
            Assert.Equal("Removed Rim, but its dashboard could not be removed. See SimHub's log.", PanelScreens.RemoveFailed("Rim"));
        }

        /// <summary>A face's rows, in voice.md's words where the artboard's differ, which voice.md wins (#791).</summary>
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
            // The caption's duration is the dash's: design/tokens.json's indicator.lapReview.durationMs, in words.
            using (var tokens = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(RepoPaths.TokensJson())))
            {
                var duration = tokens.RootElement.GetProperty("indicator").GetProperty("lapReview").GetProperty("durationMs").GetDouble();
                Assert.Equal(PanelScreens.LapReviewSeconds * 1000.0, duration);
            }
            var words = new[] { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
            Assert.Contains(" " + words[PanelScreens.LapReviewSeconds] + " seconds ", PanelScreens.LapReviewCaption);
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

            // Zone C before anything is picked, on every face, as the artboard opens.
            Assert.Equal("C", PanelScreens.AsideKey(null, reference));
            Assert.Equal("C", PanelScreens.AsideKey(null, portrait));
            Assert.All(Contract.FaceSizes, face => Assert.Contains(PanelScreens.AsideKey(null, face), face.BodyOrder));
            Assert.Equal("B", PanelScreens.AsideKey("B", reference));
            Assert.Equal(PanelScreens.BarKey, PanelScreens.AsideKey(PanelScreens.BarKey, reference));
            Assert.Equal("C", PanelScreens.AsideKey(PanelScreens.BarKey, nano));
            Assert.Equal("C", PanelScreens.AsideKey("Z", reference));
        }

        /// <summary>A zone in the picture: how many pages it cycles, the page it opens on, and the button that
        /// advances it -- or "Not bound", the chip's own words beside the aside, or nothing when the bindings
        /// cannot be read.</summary>
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
            // The chip beside the aside's Next page says the same state in the same words.
            Assert.Equal("Not bound", PanelScreens.ZoneButtonLine(new string[0]));
            Assert.Equal(PanelBindings.NotBound, PanelScreens.NoButton);
            Assert.Equal(PanelBindings.ChipText(new[] { "Keyboard.F5" }), PanelScreens.ZoneButtonLine(new[] { "Keyboard.F5" }));
        }

        /// <summary>
        /// A zone's list: the ticked pages in the zone's cycle from the page it opens on, which is the First,
        /// then under Show all the rest; the last ticked page locked; Energy, Damage and Track rivals said to
        /// be empty in iRacing and still tickable, since other sims fill them.
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
            // All and None sit beside Show all, so each says what it does to the ticks.
            Assert.Equal("Ticks every page.", PanelScreens.AllPagesTooltip);
            Assert.Equal("Unticks every page but the first.", PanelScreens.NoPagesTooltip);
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

        /// <summary>A page ticked in the list joins the cycle after the last ticked page, where the list drew
        /// it, as the artboard appends it; the start, the running zone and the order's head stay.</summary>
        [Fact]
        public void A_page_ticked_in_the_list_joins_the_end_of_the_cycle()
        {
            var settings = new FaceSettings();
            settings.Normalise();
            var head = settings.Order("A")[0];
            settings.SetPageEnabled("A", 1, false);
            // Gear alone, unticked, is drawn last under Show all; ticked, it stays last rather than going
            // back to second.
            Assert.Equal(new[] { 0, 2, 3, 1 }, PanelScreens.ZoneRows(settings, "A", true).Select(r => r.Page));
            settings.Cycle("A");
            var showing = settings.Zone("A");
            PanelScreens.Tick(settings, "A", 1, true);
            Assert.Equal(new[] { 0, 2, 3, 1 }, PanelScreens.ZoneRows(settings, "A", false).Select(r => r.Page));
            Assert.Equal(0, settings.Start("A"));
            Assert.Equal(showing, settings.Zone("A"));
            Assert.Equal(head, settings.Order("A")[0]);

            // Zone C opens on Relative, which is not the order's head: the page goes after the last ticked
            // page of the cycle read from there, and the order keeps its head.
            var zoneC = new FaceSettings();
            zoneC.Normalise();
            var cHead = zoneC.Order("C")[0];
            var energy = FacePages.For("C").First(p => p.Id == "energy").Number;
            var track = FacePages.For("C").First(p => p.Id == "track").Number;
            zoneC.SetPageEnabled("C", energy, false);
            zoneC.SetPageEnabled("C", track, false);
            PanelScreens.Tick(zoneC, "C", energy, true);
            var ticked = PanelScreens.ZoneRows(zoneC, "C", false).Select(r => r.Page).ToList();
            Assert.Equal(energy, ticked[ticked.Count - 1]);
            Assert.Equal(14, ticked[0]);
            Assert.Equal(cHead, zoneC.Order("C")[0]);
            Assert.Equal(FacePages.For("C").Count, zoneC.Order("C").Distinct().Count());

            // An untick only takes the page out, and a tick of a page already on moves nothing.
            var order = zoneC.Order("C");
            PanelScreens.Tick(zoneC, "C", energy, true);
            Assert.Equal(order, zoneC.Order("C"));
            PanelScreens.Tick(zoneC, "C", energy, false);
            Assert.False(zoneC.PageEnabled("C", energy));
            Assert.Equal(order, zoneC.Order("C"));
        }

        /// <summary>A row's controls wrap before the title beside them is squeezed below its least: a row measures
        /// its control unbounded, so the wrap is given the column less the gap and the title's room.</summary>
        [Fact]
        public void A_rows_controls_wrap_before_its_title_is_squeezed()
        {
            Assert.Equal(140, PanelScreens.RowTitleLeast);
            // The glance's two choices and the portrait wall's four, each as wide as its longest label.
            Assert.Equal(110, PanelScreens.GlanceZoneWidth);
            Assert.Equal(150, PanelScreens.GlancePageWidth);
            Assert.Equal(96, PanelScreens.PortraitChoiceWidth);
            Assert.Equal(15, PanelScreens.HeadCountSize);
            Assert.Equal(420 - PanelShell.RowGap - 140, PanelScreens.ControlsWidth(420));
            Assert.Equal(0, PanelScreens.ControlsWidth(100));

            // The widest line of controls is a glance's: its two choices and its chip, cut short at 240 with the
            // whole binding in its hover, each after its 8 px gap. Every other line is narrower, so no wrap on
            // the page changes past the column that holds it.
            Assert.Equal(8, PanelScreens.WrapGap);
            Assert.Equal(240, PanelScreens.GlanceChipMax);
            Assert.Equal(524, PanelScreens.ControlsMost);
            Assert.Equal(688, PanelScreens.ControlsColumnMost);
            Assert.True(4 * (PanelScreens.PortraitChoiceWidth + PanelScreens.WrapGap) <= PanelScreens.ControlsMost);
            Assert.True(PanelScreens.GlancePageWidth + PanelScreens.GlanceChipMax + 2 * PanelScreens.WrapGap <= PanelScreens.ControlsMost);
            Assert.Equal(PanelScreens.ControlsMost, PanelScreens.ControlsWidth(PanelScreens.ControlsColumnMost));
            // A chip cut short says the whole binding in its hover, then where it goes; a whole one says only
            // where it goes, since its own words are on it.
            Assert.Equal("FANATEC Podium Wheel Base DD1 · 12" + System.Environment.NewLine + PanelBindings.ChipTooltip, PanelScreens.ChipTooltip("FANATEC Podium Wheel Base DD1 · 12", true));
            Assert.Equal(PanelBindings.ChipTooltip, PanelScreens.ChipTooltip("Not bound", false));
            Assert.Equal(PanelBindings.ChipTooltip, PanelScreens.ChipTooltip(null, true));
            // A zone's hover carries the page and the button line where its cell cuts them, and the info bar's
            // the ends it cuts; the page passes null for a line drawn whole.
            Assert.Equal("Zone A · Gear, speed, revs · FANATEC Podium Wheel Base DD1 · 12", PanelScreens.ZoneCellTooltip("A", "Gear, speed, revs", "FANATEC Podium Wheel Base DD1 · 12"));
            Assert.Equal("Band D · Relative · Not bound", PanelScreens.ZoneCellTooltip("D", "Relative", PanelScreens.NoButton));
            Assert.Equal("Zone C", PanelScreens.ZoneCellTooltip("C", null, string.Empty));
            var nl = System.Environment.NewLine;
            Assert.Equal("Info bar" + nl + "Left: Air temperature · Track temperature" + nl + "Right: Fuel", PanelScreens.InfoBarTooltip("Air temperature · Track temperature", "Fuel"));
            Assert.Equal("Info bar" + nl + "Left: Air temperature · Track temperature", PanelScreens.InfoBarTooltip("Air temperature · Track temperature", null));
            Assert.Equal("Info bar", PanelScreens.InfoBarTooltip(null, null));
            var faceSource = ScreensSource("SettingsControl.Screens.Face.cs");
            AssertOnce(faceSource, "ScreensHover(cell, () => PanelScreens.ZoneCellTooltip(letter, ScreensIsCut(page) ? pageName : null, ScreensIsCut(button) ? buttonLine : null));", "Face.cs");
            AssertOnce(faceSource, "ScreensHover(cell, () => PanelScreens.ZoneCellTooltip(\"D\", ScreensIsCut(page) ? pageName : null, ScreensIsCut(button) ? buttonLine : null));", "Face.cs");
            AssertOnce(faceSource, "ScreensHover(cell, () => PanelScreens.InfoBarTooltip(ScreensIsCut(left) ? leftFields : null, ScreensIsCut(right) ? rightFields : null));", "Face.cs");
            AssertOnce(faceSource, "ScreensHover(chip, () => PanelScreens.ChipTooltip(label.Text, ScreensIsCut(label)));", "Face.cs");
            // The hover is worked out as it opens, when the lines are laid out, and one that would only repeat a
            // whole line does not open.
            AssertOnce(faceSource, "owner.ToolTipOpening += (sender, args) => owner.ToolTip = hover();", "Face.cs");
            AssertOnce(faceSource, "if (!ScreensIsCut(text())) args.Handled = true;", "Face.cs");
            AssertOnce(faceSource, "return whole.WidthIncludingTrailingWhitespace > block.ActualWidth + 0.5;", "Face.cs");

            // A card's name, which the kit trims with no hover, and a card on the disc, whose name two columns
            // cut short ("Tyre pressures" in 70 px), say themselves in a hover where they are cut.
            AssertOnce(ScreensSource("SettingsControl.Screens.cs"), "ScreensHoverWhenCut(card, captured.Name, () => ScreensTextIn(card, captured.Name)); cards.Add(card);", "Screens.cs");
            var roundSource = ScreensSource("SettingsControl.Screens.Round.cs");
            AssertOnce(roundSource, "var cardName = PanelScreens.CardShown(Settings.Slot(slot));", "Round.cs");
            AssertOnce(roundSource, "ScreensHoverWhenCut(cell, PanelScreens.CardHover(slot, cardName), () => name);", "Round.cs");
            Assert.Equal(Cards.All[0].DisplayName, PanelScreens.CardShown(-1));
            Assert.Equal(Cards.All[1].DisplayName, PanelScreens.CardShown(1));
            Assert.Equal(Cards.All[Cards.All.Count - 1].DisplayName, PanelScreens.CardShown(Cards.All.Count + 4));
            Assert.Equal("Card 3 · Tyre pressures", PanelScreens.CardHover(3, "Tyre pressures"));
            foreach (var source in System.IO.Directory.GetFiles(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash"), "SettingsControl.Screens*.cs"))
            {
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(RepoPaths.Code(source), @"cell\.ToolTip ="), source + " gives a cell a hover that does not ask whether its lines are cut");
            }
        }

        /// <summary>
        /// Every width the page reads is capped where what it draws stops changing (hooks 4.0 rule 11): the
        /// live preview at BodyWidth, each editor at its own bound. An uncapped read made every settled resize
        /// on a wide window rebuild the page, reload the live dashboard and close an open list.
        /// </summary>
        [Fact]
        public void The_page_reads_the_width_only_as_far_as_its_drawing_changes()
        {
            var dir = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            foreach (var source in System.IO.Directory.GetFiles(dir, "SettingsControl.Screens*.cs"))
            {
                var code = RepoPaths.Code(source);
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(code, @"\bContentWidth\b(?!UpTo)"), source + " reads the whole content width");
            }
            var page = RepoPaths.Code(System.IO.Path.Combine(dir, "SettingsControl.Screens.cs"));
            // The preview is fitted inside the column with its frame, never 2 px past it.
            Assert.Contains("BuildScreenPreview(screen, ContentWidthUpTo(BodyWidth) - 2 * PanelMetrics.BorderWeight)", page);
            var preview = RepoPaths.Code(System.IO.Path.Combine(dir, "ScreenPreview.cs"));
            Assert.Contains("BorderThickness = new Thickness(1),", preview);
            Assert.Equal(1, PanelMetrics.BorderWeight);
            var companion = RepoPaths.Code(System.IO.Path.Combine(dir, "SettingsControl.Screens.Companion.cs"));
            Assert.Contains("ContentWidthUpTo(PanelScreens.ControlsColumnMost)", companion);
        }

        /// <summary>The quick glance is picked zone first, then only the pages that zone carries, in the order a driver decides; a
        /// new zone keeps the page where it has it and opens on its first otherwise.</summary>
        [Fact]
        public void The_glance_offers_only_the_pages_its_zone_carries()
        {
            Assert.Equal(new[] { "Zone A", "Zone B", "Zone C", "Band D" }, PanelScreens.GlanceZoneLabels());
            Assert.Equal(FacePages.ZoneA.Select(p => p.Name), PanelScreens.GlancePageLabels(0));
            Assert.Equal(FacePages.BandD.Where(p => p.Id != "car").Select(p => p.Name), PanelScreens.GlancePageLabels(3));
            // Zone C's Track is zone A's Track: one drawing.
            var track = Contract.QuickGlanceValue(2, FacePages.ZoneBC.First(p => p.Id == "track").Number);
            Assert.Equal(Contract.QuickGlanceValue(0, 3), PanelScreens.GlanceWithZone(track, 0));
            // Band D carries no Track, so it opens on its first page.
            Assert.Equal(Contract.QuickGlanceValue(3, 0), PanelScreens.GlanceWithZone(track, 3));
            // Relative is in B and C and in band D.
            var relative = Contract.QuickGlanceValue(1, FacePages.ZoneBC.First(p => p.Id == "relative").Number);
            Assert.Equal(Contract.QuickGlanceValue(3, 6), PanelScreens.GlanceWithZone(relative, 3));
            Assert.Equal(new[] { "Race A", "Race B", "Tower A", "Tower B", "Telemetry A", "Telemetry B", "Telemetry C" }, PanelScreens.PitWallGlanceZoneLabels());

            // The clash line under the zone aside names band D as the picker does; it is drawn there, where the
            // zones are listed, and not among the rows under the picture.
            var editor = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Screens.Face.cs"));
            var rowsAt = editor.IndexOf("private FrameworkElement BuildFaceRows(", System.StringComparison.Ordinal);
            var clashAt = editor.IndexOf("PanelScreens.PageClash(screen.Face)", System.StringComparison.Ordinal);
            Assert.True(clashAt > 0 && clashAt < rowsAt, "the clash line is not drawn in the face editor's aside");
            Assert.Equal(clashAt, editor.LastIndexOf("PanelScreens.PageClash(screen.Face)", System.StringComparison.Ordinal));
            Assert.Contains("Ui.VStack(0, card, BuildScreensWarning(clash))", editor);
            var face = new FaceSettings();
            face.Normalise();
            Assert.Equal(string.Empty, PanelScreens.PageClash(face));
            face.SetStart("D", FacePages.BandD.First(p => p.Id == "relative").Number);
            Assert.Equal("Zone C and band D both show the relative.", PanelScreens.PageClash(face));
            face.QuickGlance = Contract.QuickGlanceValue(2, 14);
            Assert.Equal("Zone C, band D and the quick glance all show the relative.", PanelScreens.PageClash(face));
        }

        /// <summary>Details name what a reader could type exactly: the name SimHub lists, the folder as stored,
        /// and the properties by the frozen namespace, never derived from the name, which is not the namespace (ADR 0017).</summary>
        [Fact]
        public void Details_name_the_properties_by_the_namespace()
        {
            Assert.Equal("OpenDash.Face1280x480*", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindFace, Namespace = "Face1280x480", Name = "Rim" }));
            Assert.Equal("OpenDash.PitWall* and OpenDash.WebViewUrl", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindPitWall, Namespace = Contract.PitWallPrefix }));
            Assert.Equal("OpenDash.Garage*", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindPitWall, Namespace = "Garage" }));
            Assert.Equal("OpenDash.Slot01 to OpenDash.Slot02", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 }));
            Assert.Equal("OpenDash.Slot01 to OpenDash.Slot06", PanelScreens.Properties(new ScreenInstance { Kind = Contract.KindSlots, Width = 800, Height = 800 }));
            Assert.Equal(string.Empty, PanelScreens.Properties(null));
            Assert.Equal(new[] { "Details", "SimHub name", "Folder", "Properties", "Version", "Not installed", "Unknown" },
                new[] { PanelScreens.DetailsTitle, PanelScreens.SimHubNameLabel, PanelScreens.FolderLabel, PanelScreens.PropertiesLabel, PanelScreens.VersionLabel, PanelScreens.NotInstalled, PanelScreens.VersionUnknown });
            // The version as a value in sentence case: never the "(unknown version)" written to follow a name.
            Assert.Equal(PanelScreens.NotInstalled, PanelScreens.VersionShown(null));
            Assert.Equal("Unknown", PanelScreens.VersionShown(Versioning.UnknownVersion));
            Assert.Equal("Unknown", PanelScreens.VersionShown(string.Empty));
            Assert.Equal("0.3.0", PanelScreens.VersionShown("0.3.0"));
        }

        /// <summary>A pit wall: the page on screen picks the zones the list names; a wall on end draws the
        /// portrait layout's four instead, each choice naming its zone.</summary>
        [Fact]
        public void A_pit_wall_lists_the_zones_of_the_page_on_screen()
        {
            Assert.Equal("Page on screen", PanelScreens.PitWallPageTitle);
            // The race page's board is fixed in pitwall.ts and has no slot, so its list names only A and B: the
            // artboard's Board row is a recorded departure, not an oversight.
            Assert.Equal(new[] { "RaceA", "RaceB" }, PanelScreens.PitWallZones(0).Select(s => s.Key));
            Assert.DoesNotContain(Contract.PitWallZoneSlots, slot => slot.Page == "Race" && slot.Slot != "A" && slot.Slot != "B");
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
            Assert.Equal("Sets what the web view shows, from an http or https address.", PanelScreens.WebViewEmptyTooltip);
            Assert.DoesNotContain("page", PanelScreens.WebViewEmptyTooltip);
        }

        /// <summary>A companion's count, and a round screen's cards: one per slot its package reads.</summary>
        [Fact]
        public void A_companion_counts_its_modules_and_a_round_screen_its_cards()
        {
            var modules = Modules.Defaults();
            Assert.Equal("19 of 22", PanelScreens.ModuleCount(modules));
            Assert.Equal("0 of 22", PanelScreens.ModuleCount(null));
            Assert.Equal(Modules.All.Select(m => m.Name), PanelScreens.ModuleNames());
            // First module offers the modules the rotation has on, since Save moves the start past one that
            // is off: Energy, off by default, is not offered, and nothing ticked offers everything.
            var choices = PanelScreens.FirstModuleChoices(modules);
            Assert.Equal(19, choices.Length);
            Assert.DoesNotContain(Modules.All.ToList().FindIndex(m => m.Id == "energy"), choices);
            Assert.All(choices, i => Assert.True(modules[i]));
            Assert.Contains(Contract.DefaultCompanionStart, choices);
            Assert.Equal(Modules.Count, PanelScreens.FirstModuleChoices(new bool[Modules.Count]).Length);
            Assert.Equal(Modules.Count, PanelScreens.FirstModuleChoices(null).Length);
            // A module's hover is its number and what it shows.
            Assert.Equal("01 · " + Modules.All[0].Description, PanelScreens.ModuleTooltip(Modules.All[0]));
            Assert.Equal("21 · " + Modules.All[20].Description, PanelScreens.ModuleTooltip(Modules.All[20]));
            Assert.Equal("22 · " + Modules.All[21].Description, PanelScreens.ModuleTooltip(Modules.All[21]));
            Assert.Equal(string.Empty, PanelScreens.ModuleTooltip(null));
            Assert.Equal(new[] { "Controls and events", "NextScreen" }, PanelScreens.CompanionPagingCrumbs);
            Assert.All(PanelScreens.CompanionPagingCrumbs, crumb => Assert.Contains(crumb, PanelCopy.CompanionPaging));
            Assert.Equal(2, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480 }));
            Assert.Equal(6, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 800, Height = 800 }));
            // A migrated 1280 x 480 card face reads its layout's eight, and a size no layout has all twelve.
            Assert.Equal(8, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 1280, Height = 480 }));
            Assert.Equal(Contract.SlotCount, PanelScreens.CardsRead(new ScreenInstance { Kind = Contract.KindSlots, Width = 1024, Height = 600 }));
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

        /// <summary>
        /// Which kind of screen draws each row a search hit can scroll to. A row is drawn only under a screen
        /// of its kind, so the route opens the first such screen on the rig when the selected one is not, and
        /// on a face opens the zone and the whole list the two greyed pages are drawn in.
        /// </summary>
        [Fact]
        public void A_route_to_a_row_opens_a_screen_that_draws_it()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var face = settings.AddScreen(Entry("OpenDash 1280x480", Contract.KindFace, 1280, 480), "Rim");
            var wall = settings.AddScreen(Entry("OpenDash Pit wall", Contract.KindPitWall, 1920, 1080), "Wall");
            var portrait = settings.AddScreen(Entry("OpenDash Pit wall portrait", Contract.KindPitWall, 1080, 1920), "Tall wall");
            var companion = settings.AddScreen(Entry("OpenDash Companion", Contract.KindCompanion, 850, 480), "Phone");
            var round = settings.AddScreen(Entry("OpenDash 480 round", Contract.KindSlots, 480, 480), "Round");
            settings.Normalise();
            var rig = settings.RigScreens();
            var named = new Dictionary<ScreenInstance, string> { { face, "face" }, { wall, "wall" }, { portrait, "portrait" }, { companion, "companion" }, { round, "round" } };
            System.Func<string, string> kinds = anchor => string.Join(" ", rig.Where(screen => PanelScreens.Draws(anchor, screen)).Select(screen => named[screen]));

            var expected = new Dictionary<string, string>
            {
                { PanelScreens.AnchorCards, "face wall portrait companion round" },
                { PanelScreens.AnchorAdd, "face wall portrait companion round" },
                { PanelScreens.AnchorDetails, "face wall portrait companion round" },
                { PanelScreens.AnchorRevBar, "face" },
                { PanelScreens.AnchorLapReview, "face" },
                { PanelScreens.AnchorZones, "face wall" },
                { PanelScreens.AnchorInfoBar, "face" },
                { PanelScreens.AnchorZonePaging, "face" },
                { PanelScreens.AnchorFlagDisplay, "face wall portrait companion" },
                { PanelScreens.AnchorGlance, "face wall companion" },
                { PanelScreens.AnchorClassOnly, "face wall portrait" },
                { PanelScreens.AnchorPitWallPage, "wall" },
                { PanelScreens.AnchorWebView, "wall portrait" },
                { PanelScreens.AnchorPortrait, "portrait" },
                { PanelScreens.AnchorModules, "companion" },
                { PanelScreens.AnchorFirstModule, "companion" },
                { PanelScreens.AnchorPaging, "companion" },
                { PanelScreens.AnchorSlots, "round" },
                { PanelScreens.AnchorRevRing, "round" },
                { PanelSoon.ZonesInsteadOfCards.Anchor, "round" },
                { PanelSoon.CircleTracker.Anchor, "face" },
                { PanelSoon.Launch.Anchor, "face" },
            };
            foreach (var item in new[] { PanelSoon.RevFill, PanelSoon.SpotterAtRevBarEnds, PanelSoon.PitPageInPitLane, PanelSoon.PopUps, PanelSoon.DeltaEdgeLights, PanelSoon.ScreenCare, PanelSoon.Fit })
            {
                expected.Add(item.Anchor, "face");
            }
            foreach (var pair in expected) Assert.True(pair.Value == kinds(pair.Key), pair.Key + " is drawn on " + kinds(pair.Key));

            // Every row search can land on is in the map, the greyed ones included; the sheet's are not
            // searchable.
            foreach (var entry in PanelScreens.Search) Assert.True(expected.ContainsKey(entry.Route.Anchor), entry.Route.Anchor);
            foreach (var item in PanelScreens.SoonDrawn.Where(item => !item.InSheetOnly)) Assert.True(expected.ContainsKey(item.Anchor), item.Anchor);
            Assert.False(PanelScreens.Draws("leds.strips", face));

            // The selected screen stays where it draws the row, and the first that does is opened otherwise.
            Assert.Same(face, PanelScreens.ScreenFor(PanelScreens.AnchorFlagDisplay, rig, face));
            Assert.Same(companion, PanelScreens.ScreenFor(PanelScreens.AnchorFlagDisplay, rig, companion));
            Assert.Same(companion, PanelScreens.ScreenFor(PanelScreens.AnchorModules, rig, face));
            Assert.Same(portrait, PanelScreens.ScreenFor(PanelScreens.AnchorPortrait, rig, face));
            Assert.Same(face, PanelScreens.ScreenFor(PanelSoon.Fit.Anchor, rig, round));
            // A pit wall draws its zones as well, so "Zones" stays on it; Info bar, Next page and Previous
            // page are a face's alone.
            Assert.Same(wall, PanelScreens.ScreenFor(PanelScreens.AnchorZones, rig, wall));
            Assert.Same(face, PanelScreens.ScreenFor(PanelScreens.AnchorInfoBar, rig, wall));
            Assert.Same(face, PanelScreens.ScreenFor(PanelScreens.AnchorZonePaging, rig, wall));
            Assert.Equal(PanelScreens.AnchorInfoBar, PanelScreens.Search.Single(entry => entry.Label == PanelScreens.InfoBarTitle).Route.Anchor);
            Assert.Equal(PanelScreens.AnchorZonePaging, PanelScreens.Search.Single(entry => entry.Label == PanelScreens.NextPageTitle).Route.Anchor);
            Assert.Equal(PanelScreens.AnchorZonePaging, PanelScreens.Search.Single(entry => entry.Label == PanelScreens.PreviousPageTitle).Route.Anchor);
            // The nano has no bar, so it does not draw the Info bar's row.
            var nanoFace = Contract.FaceSizes.Single(f => !f.HasBar);
            var nano = settings.AddScreen(Entry("OpenDash " + nanoFace.Width + "x" + nanoFace.Height, Contract.KindFace, nanoFace.Width, nanoFace.Height), "Nano");
            Assert.True(nano.FaceSize.HasValue);
            Assert.False(PanelScreens.Draws(PanelScreens.AnchorInfoBar, nano));
            Assert.True(PanelScreens.Draws(PanelScreens.AnchorZonePaging, nano));
            // A rig with nothing that draws the row keeps its selection, and the route lands on the cards.
            var faces = new[] { face };
            Assert.Same(face, PanelScreens.ScreenFor(PanelScreens.AnchorRevRing, faces, face));
            Assert.Null(PanelScreens.ScreenFor(PanelScreens.AnchorRevRing, new ScreenInstance[0], null));

            // The two greyed pages are drawn in zone B or C under Show all; My class only in B, C or band D.
            var reference = Contract.ReferenceFace;
            Assert.True(PanelScreens.ShowsEveryPage(PanelSoon.CircleTracker.Anchor));
            Assert.True(PanelScreens.ShowsEveryPage(PanelSoon.Launch.Anchor));
            Assert.False(PanelScreens.ShowsEveryPage(PanelScreens.AnchorZones));
            Assert.Equal("C", PanelScreens.AsideFor(PanelSoon.CircleTracker.Anchor, null, reference));
            Assert.Equal("C", PanelScreens.AsideFor(PanelSoon.Launch.Anchor, "A", reference));
            Assert.Equal("B", PanelScreens.AsideFor(PanelSoon.Launch.Anchor, "B", reference));
            Assert.Equal("C", PanelScreens.AsideFor(PanelSoon.Launch.Anchor, PanelScreens.BarKey, reference));
            Assert.Equal("C", PanelScreens.AsideFor(PanelScreens.AnchorClassOnly, "A", reference));
            Assert.Equal("D", PanelScreens.AsideFor(PanelScreens.AnchorClassOnly, "D", reference));
            Assert.Equal("A", PanelScreens.AsideFor(PanelScreens.AnchorRevBar, "A", reference));
            Assert.Null(PanelScreens.AsideFor(PanelScreens.AnchorRevBar, null, reference));
            // The Info bar opens the bar's aside; Next page and Previous page, drawn in a zone's, leave the bar
            // for zone C and keep a zone that was picked.
            Assert.Equal(PanelScreens.BarKey, PanelScreens.AsideFor(PanelScreens.AnchorInfoBar, "A", reference));
            Assert.Equal("C", PanelScreens.AsideFor(PanelScreens.AnchorZonePaging, PanelScreens.BarKey, reference));
            Assert.Equal("B", PanelScreens.AsideFor(PanelScreens.AnchorZonePaging, "B", reference));
            Assert.Null(PanelScreens.AsideFor(PanelScreens.AnchorZonePaging, null, reference));
        }

        /// <summary>
        /// Each anchor a Screens file sets is one the model says a screen of that file's kind draws: the model
        /// test above checks the map against itself, and this holds it to where the page puts each anchor, so a
        /// pit wall picture anchored on screens.zones while the model called the zones a face's alone fails here.
        /// </summary>
        [Fact]
        public void Every_anchor_the_page_sets_is_drawn_where_the_model_says()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var face = settings.AddScreen(Entry("OpenDash 1280x480", Contract.KindFace, 1280, 480), "Rim");
            var wall = settings.AddScreen(Entry("OpenDash Pit wall", Contract.KindPitWall, 1920, 1080), "Wall");
            var portrait = settings.AddScreen(Entry("OpenDash Pit wall portrait", Contract.KindPitWall, 1080, 1920), "Tall wall");
            var companion = settings.AddScreen(Entry("OpenDash Companion", Contract.KindCompanion, 850, 480), "Phone");
            var round = settings.AddScreen(Entry("OpenDash 480 round", Contract.KindSlots, 480, 480), "Round");
            settings.Normalise();
            var kinds = new Dictionary<string, ScreenInstance[]>
            {
                { "SettingsControl.Screens.cs", new[] { face, wall, portrait, companion, round } },
                { "SettingsControl.Screens.Face.cs", new[] { face } },
                { "SettingsControl.Screens.PitWall.cs", new[] { wall, portrait } },
                { "SettingsControl.Screens.Companion.cs", new[] { companion } },
                { "SettingsControl.Screens.Round.cs", new[] { round } },
            };
            var dir = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            Assert.Equal(kinds.Keys.OrderBy(k => k, System.StringComparer.Ordinal), System.IO.Directory.GetFiles(dir, "SettingsControl.Screens*.cs").Select(System.IO.Path.GetFileName).OrderBy(k => k, System.StringComparer.Ordinal));
            var set = 0;
            foreach (var pair in kinds)
            {
                var code = RepoPaths.Code(System.IO.Path.Combine(dir, pair.Key));
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(code, @"Ui\.Anchor\((?:[^;]*?), PanelScreens\.(Anchor\w+)\)"))
                {
                    var anchor = (string)typeof(PanelScreens).GetField(match.Groups[1].Value).GetValue(null);
                    Assert.True(pair.Value.Any(screen => PanelScreens.Draws(anchor, screen)), pair.Key + " sets " + anchor + ", which the model says no screen of its kind draws");
                    set++;
                }
            }
            Assert.True(set >= 20, "found only " + set + " anchors");
        }

        /// <summary>
        /// What a press on the page redraws, decided here and held in the page's sources: a change saved to a
        /// screen the migration made rebuilds the page once the press is released, nothing holds the mouse and
        /// none of the page's own sheets is open, the first one only, and never once the panel is off screen or
        /// the page left; a card pressed is a
        /// selection and rebuilds in place without asking SimHub again or clearing the line a press left; a
        /// zone picked opens on its ticked pages; and the page follows a route's anchor on the way in through
        /// OnLeave rather than the shell's own rebuild flag or focus walk, which are not hooks.
        /// </summary>
        [Fact]
        public void A_press_redraws_what_it_changed_and_no_more()
        {
            var migrated = new ScreenInstance { Kind = Contract.KindFace, Unclaimed = true };
            Assert.True(PanelScreens.RebuildsPageAfterSave(migrated));
            migrated.Keep();
            Assert.False(PanelScreens.RebuildsPageAfterSave(migrated));
            Assert.False(PanelScreens.RebuildsPageAfterSave(new ScreenInstance { Kind = Contract.KindFace, Unclaimed = false }));
            Assert.False(PanelScreens.RebuildsPageAfterSave(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.False(PanelScreens.RebuildsPageAfterSave(null));
            Assert.False(PanelScreens.ShowAllAfterPick);
            Assert.Equal("pressed", PanelScreens.ZoneStatus(true));
            Assert.Equal("not pressed", PanelScreens.ZoneStatus(false));

            var dir = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            var page = RepoPaths.Code(System.IO.Path.Combine(dir, "SettingsControl.Screens.cs"));
            var face = RepoPaths.Code(System.IO.Path.Combine(dir, "SettingsControl.Screens.Face.cs"));
            // The save asks the model before it keeps the screen, redraws the editor, and leaves the rest to a
            // refresh that waits for the press to end and for a sheet to close, and runs only while the panel
            // is on screen and no Go has come since it was asked for.
            Assert.Contains("var rebuilds = PanelScreens.RebuildsPageAfterSave(screen);\n            Save(screen);", page.Replace("\r\n", "\n"));
            Assert.Contains("if (!rebuilds) return;\n            ScreensRefreshAfterKeep();", page.Replace("\r\n", "\n"));
            Assert.Contains("OnLeave(\"Screens.keepRefresh\", () => screensKeepRefresh++);", page);
            Assert.Contains("if (ticket != screensKeepRefresh || !IsLoaded) return;", page);
            // It waits while the button is down or anything holds the capture (an open list does), re-checked
            // after every input, and for this page's own sheet, which it tracks through ShowSheet's closed.
            Assert.Contains("return Mouse.LeftButton == MouseButtonState.Pressed || Mouse.Captured != null;", page);
            Assert.Contains("if (ScreensMouseBusy) return;\n                        InputManager.Current.PostProcessInput -= released;", page.Replace("\r\n", "\n"));
            Assert.Contains("InputManager.Current.PostProcessInput += released;", page);
            Assert.Contains("if (screensSheetOpen)\n                {\n                    screensAfterSheet = () => ScreensRefreshWhenFree(ticket);", page.Replace("\r\n", "\n"));
            Assert.Contains("ShowSheet(title, body, footer, ScreensSheetClosed);\n            screensSheetOpen = true;", page.Replace("\r\n", "\n"));
            Assert.Contains("screensSheetOpen = false;\n            var after = screensAfterSheet;", page.Replace("\r\n", "\n"));
            // The rebuild is reached only from the refresh: never inside the press that raised the save.
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(page, @"RefreshAttention\(\);\s*RebuildPage\(\);\s*RefreshSidebar\(\);"));
            // A card press selects and rebuilds in place.
            Assert.Contains("Select(PanelPage.Screens, captured.Namespace);\n                        RebuildPage();", page.Replace("\r\n", "\n"));
            Assert.Contains("OnLeave(\"Screens.follow\"", page);
            Assert.Contains("screensShowAll = PanelScreens.ShowAllAfterPick;", face);
            Assert.Contains("PanelScreens.ZoneStatus(selected)", face);
            foreach (var source in System.IO.Directory.GetFiles(dir, "SettingsControl.Screens*.cs"))
            {
                var code = RepoPaths.Code(source);
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(code, @"\brebuilding\b"), source + " reads the shell's rebuild flag");
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(code, @"(?<![A-Za-z])FocusPath\("), source + " calls the shell's FocusPath");
                // The shell's sheet is reached only through its hooks: every sheet goes through ScreensShowSheet,
                // whose closed callback is how the page knows one is open.
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(code, @"\b(SheetOpen|sheetLayer|sheetPanel|sheetClosed|sheetOpener)\b"), source + " reads the shell's sheet internals");
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(code, @"(?<![A-Za-z])ShowSheet\((?!title, body, footer, ScreensSheetClosed\))"), source + " opens a sheet the page does not track");
                Assert.DoesNotContain("\"pressed\"", code);
            }
        }

        /// <summary>
        /// Every setting a screen owns is written from the Screens page, the only place a screen's own settings
        /// are edited, as PanelDataTabTests holds the rig-wide ones to the panel: each property a kind
        /// publishes, less the four the plugin publishes rather than stores (a face zone's page and position, a
        /// companion's page and the module it is forced onto), and the stored settings that are not properties
        /// (the three quick glances outside a face, the companion's start, the shared slots).
        /// </summary>
        /// <remarks>
        /// Each property is mapped to the start of a writer here, and the map must cover the kind's names
        /// exactly, so a property added to the contract turns this red until someone says where the page writes
        /// it. This test holds only that each writer appears somewhere in the page's sources; which control
        /// writes which setting, and from which reading, is held statement by statement in
        /// Each_control_writes_and_reads_its_own_setting.
        /// </remarks>
        [Fact]
        public void Every_setting_a_screen_owns_is_written_from_the_screens_page()
        {
            const string ns = "Rim";
            var published = new HashSet<string>();
            var writers = new Dictionary<string, string>();
            foreach (var letter in Contract.FaceZoneLetters)
            {
                published.Add(Contract.ZonePageProperty(ns, letter));
                published.Add(Contract.ZonePositionProperty(ns, letter));
                writers[Contract.ZoneMaskProperty(ns, letter)] = "PanelScreens.Tick(";
                writers[Contract.ZoneStartProperty(ns, letter)] = "PanelScreens.Reorder(";
                writers[Contract.ZoneClassOnlyProperty(ns, letter)] = "face.SetClassOnly(";
            }
            foreach (var slot in Contract.BarSlots) writers[Contract.BarFieldProperty(ns, slot)] = "screen.Face.SetBarField(";
            writers[Contract.QuickGlanceProperty(ns)] = "screen.Face.QuickGlance =";
            writers[Contract.FlagFormatProperty(ns)] = "screen.FlagFormat =";
            writers[Contract.LapReviewProperty(ns)] = "screen.LapReview =";
            writers[Contract.RevBarProperty(ns)] = "Settings.SetScreenRevBar(";
            AssertCovers(Contract.ScreenPropertyNames(Contract.KindFace, ns), writers, published);

            published.Clear();
            writers.Clear();
            for (var module = 1; module <= Modules.Count; module++) writers[Contract.ModuleProperty(ns, module)] = "screen.Modules[index] = on;";
            published.Add(Contract.CompanionPageProperty(ns));
            published.Add(Contract.CompanionOpenOnProperty(ns));
            writers[Contract.CompanionFlagFormatProperty(ns)] = "screen.CompanionFlagFormat =";
            AssertCovers(Contract.ScreenPropertyNames(Contract.KindCompanion, ns), writers, published);

            published.Clear();
            writers.Clear();
            foreach (var slot in Contract.PitWallZoneSlots) writers[Contract.ZoneProperty(ns, slot)] = "screen.SetZonePage(";
            writers[Contract.PitWallPageProperty(ns)] = "screen.PitWallPage =";
            writers[Contract.WebViewUrlProperty(ns)] = "screen.WebViewUrl =";
            writers[Contract.PitWallClassOnlyProperty(ns)] = "screen.PitWallClassOnly =";
            writers[Contract.PitWallFlagFormatProperty(ns)] = "screen.PitWallFlagFormat =";
            AssertCovers(Contract.ScreenPropertyNames(Contract.KindPitWall, ns), writers, published);
            Assert.Empty(Contract.ScreenPropertyNames(Contract.KindSlots, ns));

            // The stored settings that are not properties, and the whole list of writers, in the page's own
            // sources: the four editors, and the zone list's other presses beside Tick and Reorder.
            var dir = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            var sources = string.Concat(System.IO.Directory.GetFiles(dir, "SettingsControl.Screens*.cs").Select(RepoPaths.Code));
            foreach (var writer in new[]
            {
                "PanelScreens.Tick(", "PanelScreens.SetEveryPage(", "PanelScreens.Reorder(", "face.SetClassOnly(", "screen.Face.SetBarField(",
                "screen.Face.QuickGlance =", "screen.FlagFormat =", "screen.LapReview =", "Settings.SetScreenRevBar(",
                "screen.Modules[index] = on;", "screen.CompanionFlagFormat =", "screen.CompanionStart =", "screen.CompanionQuickGlance =",
                "screen.OpenOnStartModule(", "screen.SetZonePage(", "screen.PitWallPage =", "screen.WebViewUrl =", "screen.PitWallClassOnly =",
                "screen.PitWallFlagFormat =", "screen.PitWallQuickGlance =", "Settings.SetSlot(", "Settings.SetRevBar(",
            })
            {
                Assert.True(sources.Contains(writer), "no Screens file writes " + writer);
            }
        }

        /// <summary>
        /// A screen written after SimHub started is said one way on every page: the Screens
        /// card's state and its fix box's title, Home's screen line, the first step under Home's issue and the
        /// Updates table's state are all PanelCopy.RestartToLoad. Home's issue title names the screen ("Rim is
        /// not in SimHub yet", the voice ruling on Main.dc.html's "Rim isn't in SimHub yet") and so does not
        /// repeat the phrase its step says.
        /// </summary>
        [Fact]
        public void A_screen_waiting_for_the_restart_is_said_one_way_on_every_page()
        {
            const string phrase = "Restart SimHub to load it";
            Assert.Equal(phrase, PanelCopy.RestartToLoad);

            // Screens: the card, and the fix box under it.
            Assert.Equal(phrase, PanelScreens.StateLabel(ScreenState.Restart));
            Assert.Contains("return Ui.FixBox(PanelCopy.RestartToLoad,", ScreensSource("SettingsControl.Screens.cs"));

            // Home: the screen's line, and the issue's step.
            var rim = new ScreenInstance { Name = "Rim", Namespace = "Rim", Kind = Contract.KindFace, Width = 1280, Height = 480 };
            rim.Normalise();
            Assert.Equal(phrase, PanelHome.ScreenLine(new OpenDashSettings(), rim, true, true).Text);
            var waiting = new AttentionInput();
            waiting.Screens.Add(new AttentionScreen { Name = "Rim", Namespace = "Rim", Installed = true, AddedSinceStart = true });
            var issue = System.Linq.Enumerable.Single(PanelAttention.Find(waiting));
            Assert.Equal("Rim is not in SimHub yet", issue.Title);
            Assert.StartsWith(phrase + ". ", issue.Detail);
            Assert.EndsWith(PanelScreens.RestartDetail("Rim"), issue.Detail);
            Assert.DoesNotContain("Restart SimHub", issue.Title);

            // Updates: the dashboard's row in the In SimHub table, whatever the installer last found.
            var package = new PackageStatus { FolderName = "Rim", Status = InstallStatus.UpToDate, InstalledVersion = "0.5.0", EmbeddedVersion = "0.5.0" };
            Assert.Equal(phrase, PanelUpdates.DashboardRow(rim, package, true, true).State);
            Assert.Null(typeof(PanelUpdates).GetField("WaitingForRestart"));

            // The step after the restart is one phrasing on all three: the fix box's detail, Home's issue and the
            // Updates row's hover, with no "this display" where Home and Updates have no display to point at.
            var step = PanelScreens.RestartDetail("Rim");
            Assert.Contains("return Ui.FixBox(PanelCopy.RestartToLoad, PanelScreens.RestartDetail(screen.Name),", ScreensSource("SettingsControl.Screens.cs"));
            Assert.Equal(phrase + ". " + step, issue.Detail);
            Assert.Equal(step, PanelUpdates.DashboardRow(rim, package, true, true).Tooltip);
            Assert.DoesNotContain("this display", step);
        }

        /// <summary>
        /// An empty Screens page says so over its add tile, as LEDs and Matrix do, in the words Home's card and the
        /// Updates table read from it: the three device pages lay out their empty state one way, and nothing
        /// quotes a phrase the page it points at does not draw.
        /// </summary>
        [Fact]
        public void An_empty_rig_is_said_over_the_add_tile_as_LEDs_and_Matrix_say_theirs()
        {
            var cards = ScreensSource("SettingsControl.Screens.cs");
            Assert.Contains("if (rig.Count > 0) return grid; return Ui.VStack(12, Ui.Prose(PanelScreens.NoScreens, Theme.SizeBody), grid);", cards);
            foreach (var empty in new[] { PanelScreens.NoScreens, PanelLeds.NoStrips, PanelMatrix.NoPanels })
            {
                Assert.EndsWith(".", empty);
                Assert.Equal(empty, PanelHome.EmptyLine(empty));
            }
            Assert.StartsWith(PanelScreens.NoScreens + " ", PanelUpdates.NothingInSimHub);
        }

        /// <summary>The page's own source file, its whitespace collapsed so a pin can span statements.</summary>
        private static string ScreensSource(string file)
        {
            var path = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", file);
            return System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(path), @"\s+", " ");
        }

        private static void AssertOnce(string code, string pin, string file)
        {
            var count = System.Text.RegularExpressions.Regex.Matches(code, System.Text.RegularExpressions.Regex.Escape(pin)).Count;
            Assert.True(count == 1, file + " has " + count + " of: " + pin);
        }

        /// <summary>
        /// Each control writes its own setting and reads the one it writes: the control's reading and its
        /// handler's statement are pinned together in the file that draws it, so a control that writes the
        /// wrong key, every row writing one slot, or a face's rev bar reading the rig's turns this red.
        /// </summary>
        [Fact]
        public void Each_control_writes_and_reads_its_own_setting()
        {
            var face = ScreensSource("SettingsControl.Screens.Face.cs");
            foreach (var pin in new[]
            {
                "ScreensSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.ScreenRevBar(ns), value => { Settings.SetScreenRevBar(ns, value); ScreensSave(screen, redraw); });",
                "ScreensSegmented(Contract.FlagFormats, PanelScreens.FlagLabels, Settings.ScreenFlagFormat(ns), value => { screen.FlagFormat = value;",
                "ScreensSegmented(Contract.LapReviewModes, PanelScreens.LapReviewLabels, Settings.ScreenLapReview(ns), value => { screen.LapReview = value;",
                "Ui.ChoiceButton(fields, screen.Face.BarField(slot), index => { screen.Face.SetBarField(slot, index); ScreensSave(screen, redraw); });",
                "var glance = screen.Face.NormalisedQuickGlance(); var zoneIndex = Contract.QuickGlanceZone(glance);",
                "Ui.ChoiceButton(PanelScreens.GlanceZoneLabels(), zoneIndex, chosen => { screen.Face.QuickGlance = PanelScreens.GlanceWithZone(screen.Face.QuickGlance, chosen, screen.Theme); ScreensSave(screen, redraw); }",
                "var pages = PanelScreens.GlancePages(zoneIndex, screen.Theme);",
                "Ui.ChoiceButton(PanelScreens.GlancePageLabels(zoneIndex, screen.Theme), Array.IndexOf(pages, Contract.QuickGlancePage(glance)), chosen => { screen.Face.QuickGlance = Contract.QuickGlanceValue(zoneIndex, pages[chosen]); ScreensSave(screen, redraw); }",
                // A tick, a drag, All and None each settle, and settling redraws: the count, the First tag and
                // the cell's page follow.
                "Action settle = () => { ScreensSave(screen, redraw); };",
                "PanelScreens.Tick(face, letter, page, on);",
                "PanelScreens.Reorder(face, letter, showAll, from, to);",
                "PanelScreens.AllPages, () => { PanelScreens.SetEveryPage(face, letter, true);",
                "PanelScreens.NoPages, () => { PanelScreens.SetEveryPage(face, letter, false);",
                "Ui.Switch(face.IsClassOnly(letter), on => { face.SetClassOnly(letter, on);",
            })
            {
                AssertOnce(face, pin, "Face.cs");
            }

            var wall = ScreensSource("SettingsControl.Screens.PitWall.cs");
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(wall,
                System.Text.RegularExpressions.Regex.Escape("screen.ZonePage(captured.Key), index => { screen.SetZonePage(captured.Key, index); ScreensSave(screen, redraw); }")).Count);
            foreach (var pin in new[]
            {
                // Page on screen also drives the picture and the zone list, so it redraws.
                "screen.PitWallPage = Contract.NormalisePitWallPage(chosen); ScreensSave(screen, redraw);",
                // The glance's two choices, each keeping the other half of the value it read.
                "var glance = Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance); var zoneIndex = Contract.QuickGlanceZone(glance); var page = Contract.QuickGlancePage(glance);",
                "Ui.ChoiceButton(PanelScreens.PitWallGlanceZoneLabels(), zoneIndex, chosen => { screen.PitWallQuickGlance = Contract.PitWallQuickGlanceValue(chosen, Contract.QuickGlancePage(Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance))); ScreensSave(screen, redraw); }",
                "Ui.ChoiceButton(ZonePages.Standard.Select(p => p.Name).ToArray(), page, chosen => { screen.PitWallQuickGlance = Contract.PitWallQuickGlanceValue(Contract.QuickGlanceZone(Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance)), chosen); ScreensSave(screen, redraw); }",
                // The web view commits on blur or Enter.
                "box.LostFocus += (sender, args) => commit();",
                "if (args.Key == Key.Enter) commit();",
                "Ui.Switch(screen.PitWallClassOnly, on => { screen.PitWallClassOnly = on;",
                "Settings.ScreenPitWallFlagFormat(screen.Namespace), value => { screen.PitWallFlagFormat = Contract.NormalisePitWallFlagFormat(value);",
                "screen.WebViewUrl = normalised;",
            })
            {
                AssertOnce(wall, pin, "PitWall.cs");
            }

            var round = ScreensSource("SettingsControl.Screens.Round.cs");
            foreach (var pin in new[]
            {
                "Ui.ChoiceButton(cards, Settings.Slot(captured), index => { Settings.SetSlot(captured, index); ScreensSave(screen, redraw); });",
                "ScreensSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.RevBarMode(), value => { Settings.SetRevBar(value);",
            })
            {
                AssertOnce(round, pin, "Round.cs");
            }

            var companion = ScreensSource("SettingsControl.Screens.Companion.cs");
            foreach (var pin in new[]
            {
                "Array.IndexOf(choices, Settings.ScreenCompanionStart(screen.Namespace)), index =>",
                "var value = choices[index]; screen.CompanionStart = value; screen.OpenOnStartModule(DateTime.UtcNow); ScreensSave(screen, redraw);",
                "ScreensSegmented(Contract.CompanionFlagFormats, PanelScreens.BarFlagLabels, Settings.ScreenCompanionFlagFormat(screen.Namespace), value => { screen.CompanionFlagFormat = Contract.NormaliseCompanionFlagFormat(value);",
                "Ui.ChoiceButton(PanelScreens.ModuleNames(), Settings.ScreenCompanionQuickGlance(screen.Namespace), value => { screen.CompanionQuickGlance = value; ScreensSave(screen, redraw); });",
                // A module tick reads its own module and redraws, so the count and First module follow it.
                "IsChecked = screen.Modules != null && index < screen.Modules.Length && screen.Modules[index],",
                "screen.Modules[index] = on; ScreensSave(screen, redraw);",
            })
            {
                AssertOnce(companion, pin, "Companion.cs");
            }
        }

        /// <summary>
        /// Each binding a control shows is the action it says: a zone's Next page chip and button line are its
        /// forward action, Previous page its back action, band D's line its own, and each glance chip the
        /// screen's hold.
        /// </summary>
        [Fact]
        public void Each_chip_and_button_line_shows_its_own_action()
        {
            var face = ScreensSource("SettingsControl.Screens.Face.cs");
            foreach (var pin in new[]
            {
                "var nextChip = ScreensCutChip(BindingChipFor(Contract.CycleZoneAction(screen.Namespace, letter)), PanelFacePlan.AsideChipMax);",
                "var backChip = ScreensCutChip(BindingChipFor(Contract.CycleZoneBackAction(screen.Namespace, letter)), PanelFacePlan.AsideChipMax);",
                "ScreensAsideLine(Ui.Text(PanelScreens.NextPageTitle, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), nextChip), ScreensAsideLine(previous, backChip)",
                "var buttonLine = PanelScreens.ZoneButtonLine(TriggersOf(Contract.CycleZoneAction(screen.Namespace, letter)));",
                "var buttonLine = PanelScreens.ZoneButtonLine(TriggersOf(Contract.CycleZoneAction(screen.Namespace, \"D\")));",
                "CycleZoneBackAction",
            })
            {
                AssertOnce(face, pin, "Face.cs");
            }
            foreach (var file in new[] { "SettingsControl.Screens.Face.cs", "SettingsControl.Screens.PitWall.cs", "SettingsControl.Screens.Companion.cs" })
            {
                var code = ScreensSource(file);
                AssertOnce(code, "var chip = ScreensCutChip(BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace)), PanelScreens.GlanceChipMax);", file);
                AssertOnce(code, "Contract.HoldQuickGlanceActionFor(", file);
            }
        }

        /// <summary>
        /// What the picture and the asides draw is read from the model that decides it: a cell's page is the page
        /// its zone opens on, the strip is lit by the face's own rev bar, the bar's middle is the car's settings,
        /// a pit wall zone names its own page, each card on the disc its own slot, the list's greyed pages only
        /// under Show all, and the cards and the header the screen's own state and facts.
        /// </summary>
        [Fact]
        public void The_picture_and_the_asides_draw_what_their_models_read()
        {
            // The composite readings, decided in the models.
            var settings = new FaceSettings();
            settings.Normalise();
            Assert.Equal("Relative", PanelScreens.OpensOnName(settings, "C"));
            foreach (var letter in Contract.FaceZoneLetters)
            {
                Assert.Equal(FacePages.NameOf(letter, PanelScreens.FirstTicked(settings, letter)), PanelScreens.OpensOnName(settings, letter));
            }
            Assert.False(PanelScreens.RevStripOn(Contract.RevBarOff));
            foreach (var value in PanelDataTab.RevBarValues.Where(v => v != Contract.RevBarOff)) Assert.True(PanelScreens.RevStripOn(value), value);
            var wall = new ScreenInstance { Kind = Contract.KindPitWall, Width = 1920, Height = 1080, Namespace = "PitWall" };
            wall.Normalise();
            var raceA = Contract.PitWallZoneSlotByKey("RaceA");
            var towerWide = Contract.PitWallZoneSlotByKey("TowerWide");
            wall.SetZonePage(raceA.Key, 3);
            Assert.Equal(ZonePages.StandardName(3), PanelScreens.PitWallZonePageName(wall, raceA));
            Assert.Equal(ZonePages.WideName(wall.ZonePage(towerWide.Key)), PanelScreens.PitWallZonePageName(wall, towerWide));
            Assert.NotEqual(PanelScreens.PitWallZonePageName(wall, raceA), PanelScreens.PitWallZonePageName(wall, Contract.PitWallZoneSlotByKey("RaceB")));

            var face = ScreensSource("SettingsControl.Screens.Face.cs");
            foreach (var pin in new[]
            {
                "rows.Children.Add(BuildRevStrip(plan.RevBar, PanelScreens.RevStripOn(Settings.ScreenRevBar(screen.Namespace))));",
                "var leftFields = PanelScreens.BarEnd(screen.Face, face, true); var rightFields = PanelScreens.BarEnd(screen.Face, face, false);",
                "var left = ScreensCellText(leftFields,",
                "var middle = ScreensCellText(PanelScreens.InfoBarMiddle,",
                "var right = ScreensCellText(rightFields,",
                "var pageName = PanelScreens.OpensOnName(screen.Face, letter);",
                "var pageName = PanelScreens.OpensOnName(screen.Face, \"D\");",
                "ScreensCellText(PanelScreens.ZoneCount(screen.Face, letter),",
                "ScreensCellText(PanelScreens.ZoneCount(screen.Face, \"D\"),",
                "Ui.Text(PanelScreens.ZoneCount(face, letter), PanelScreens.HeadCountSize,",
                "if (showAll && PanelScreens.ListsSoonModules(letter))",
                "ToolTip = row.Locked ? PanelScreens.LastPageTooltip : null,",
                "if (row.NotInIracing) {",
                "if (row.First) {",
            })
            {
                AssertOnce(face, pin, "Face.cs");
            }
            Assert.DoesNotContain("FirstTicked(", face);
            var wallSource = ScreensSource("SettingsControl.Screens.PitWall.cs");
            AssertOnce(wallSource, "var slot = Contract.PitWallZoneSlotByKey(page.Title + panel.Name);", "PitWall.cs");
            AssertOnce(wallSource, "slot == null ? panel.Shows ?? string.Empty : PanelScreens.PitWallZonePageName(screen, slot)", "PitWall.cs");
            AssertOnce(ScreensSource("SettingsControl.Screens.Companion.cs"), "if (PanelScreens.IsNotInIracing(module.Id)) {", "Companion.cs");
            AssertOnce(ScreensSource("SettingsControl.Screens.Companion.cs"), "Ui.Text(PanelScreens.ModuleCount(screen.Modules), PanelScreens.HeadCountSize,", "Companion.cs");

            var page = ScreensSource("SettingsControl.Screens.cs");
            foreach (var pin in new[]
            {
                // A card's state is the screen's own, its dot in that state's colour; the header's facts are
                // the screen's kind and size.
                "var state = ScreensStateOf(captured);",
                "PanelScreens.CardMeta(captured), PanelScreens.StateLabel(state), PanelScreens.StateHex(state),",
                "var facts = Ui.Prose(PanelScreens.Facts(screen));",
                "title.ToolTip = screen.Name;",
                // A screen written since SimHub started reads Restart SimHub to load it.
                "return PanelScreens.StateOf(Installed(screen), PanelAttention.Has(issues, PanelAttention.ScreenRestart, screen.Namespace));",
            })
            {
                AssertOnce(page, pin, "Screens.cs");
            }
            Assert.NotEqual(PanelScreens.StateHex(ScreenState.InSimHub), PanelScreens.StateHex(ScreenState.Missing));
            Assert.NotEqual(PanelScreens.StateHex(ScreenState.InSimHub), PanelScreens.StateHex(ScreenState.Restart));
        }

        /// <summary>
        /// The sheets' presses hand the models what the driver set: the name box only counts typing as the
        /// driver's and a pick fills it only until then, Add and Save take the box and the size picked, the
        /// second-copy note asks the model, a reinstall caption follows an edited folder, Duplicate selects the
        /// copy, and Remove says the result it had and hovers in the screen's own words.
        /// </summary>
        [Fact]
        public void The_sheets_hand_their_models_what_was_set()
        {
            var page = ScreensSource("SettingsControl.Screens.cs");
            foreach (var pin in new[]
            {
                "typed = PanelAddScreen.Typed(typed, name.IsKeyboardFocusWithin, name.Text);",
                "name.Text = PanelAddScreen.FilledName(name.Text, typed, entry, Settings.RigScreens().Select(s => s.Name));",
                "var second = PanelAddScreen.SettingsTaken(entry, Settings.RigScreens()); note.Text = PanelAddScreen.Note(type, entry, second);",
                "size = option; entry = PanelAddScreen.ThemeKept(PanelAddScreen.Themes(catalogue, size), entry.Theme); drawSizes(); drawThemes(); fillName(); refreshNote();",
                "entry = option; drawThemes(); fillName(); refreshNote();",
                "if (number != null) number.Text = PanelAddScreen.NameStepNumber(asked).ToString(CultureInfo.InvariantCulture);",
                "CloseSheet(); AddScreen(entry, name.Text);",
                "sizeRow = BuildSizeRow(type, screen, choices, question, e => chosen = e);",
                "save.Click += (sender, args) => SaveEdit(screen, name.Text, chosen);",
                "var edited = Edited(screen);",
                "edited ? PanelAddScreen.ReinstallEditedCaption : PanelAddScreen.ReinstallCaption",
                "reinstall.Click += (sender, args) => ReinstallScreen(screen);",
                "Select(PanelPage.Screens, copy.Namespace);",
                "Say(result.Ok ? PanelScreens.Removed(screen.Name) : PanelScreens.RemoveFailed(screen.Name), result.Ok);",
            })
            {
                AssertOnce(page, pin, "Screens.cs");
            }
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(page, System.Text.RegularExpressions.Regex.Escape("remove.ToolTip = PanelScreens.RemoveTooltipFor(screen);")).Count);
            Assert.DoesNotContain("RemoveTooltip;", page);
        }

        /// <summary>
        /// The page uses the decisions its models make: each press opens what it says, each state draws its own
        /// fix box, a result is said as the result it was, the empty rig stops before a header it has no screen
        /// for, and each editor draws what the model says its kind and orientation have.
        /// </summary>
        [Fact]
        public void The_page_draws_what_its_models_decide()
        {
            var page = ScreensSource("SettingsControl.Screens.cs");
            foreach (var pin in new[]
            {
                "if (PanelScreens.ShowsUnclaimedNote(rig)) sections.Add(BuildUnclaimedNote());",
                "if (rig.Count == 0) { sections.Add(Ui.Prose(PanelCopy.EmptyRig, Theme.SizeBody)); return PageLayout(PanelScreens.Title, null, sections.ToArray()); }",
                "var fix = BuildScreenFix(screen); if (fix != null) selected.Add(fix);",
                "cards.Add(Ui.Anchor(Ui.DashedAddCard(PanelAddScreen.SectionTitle, ShowAddScreen), PanelScreens.AnchorAdd));",
                // A route to the add tile opens the Add sheet on the way in, after the page's own focus, and only
                // while the build it follows is showing (#792): Home's empty-rig tile routes here.
                "if (to != null && to.Anchor == PanelScreens.AnchorAdd) ScreensOpenAddOnArrival();",
                "var dropped = false; OnDrop(() => dropped = true); Dispatcher.BeginInvoke(new Action(() => { if (!dropped) ShowAddScreen(); }), DispatcherPriority.Input);",
                "Ui.CardGrid(PanelKit.CardMinWidth, PanelKit.CardGridGap, PanelScreens.CardColumns, cards.ToArray())",
                "edit.Click += (sender, args) => ShowEdit(screen);",
                "duplicate.Click += (sender, args) => DuplicateScreen(screen);",
                "remove.Click += (sender, args) => ShowRemove(screen);",
                "write.Click += (sender, args) => InstallScreenAgain(screen); return Ui.FixBox(PanelScreens.MissingTitle, PanelScreens.MissingDetailFor(screen), null, write);",
                "case ScreenState.Restart:",
                "return Ui.FixBox(PanelCopy.RestartToLoad, PanelScreens.RestartDetail(screen.Name), null, null, PanelIcons.Restart);",
                "ShowSheet(PanelScreens.RemoveTitle(screen.Name), Ui.Prose(PanelScreens.RemoveBody(screen), Theme.SizeBody),",
                // Keep it keeps a migrated screen as well as going back.
                "keep.Click += (sender, args) => { Save(screen); Redraw(); };",
                "Say(result.Ok ? PanelAddScreen.Added(screen) : PanelAddScreen.AddFailed(screen.Name), result.Ok);",
                "Say(result.Ok ? PanelAddScreen.Added(copy) : PanelAddScreen.AddFailed(copy.Name), result.Ok);",
                "Say(result.Ok ? PanelAddScreen.Renamed(screen.Name) : PanelAddScreen.RenameFailed(screen.Name), result.Ok);",
                "Say(result.Ok ? PanelAddScreen.Reinstalled(screen.Name) : PanelAddScreen.ReinstallFailed(screen.Name, result.Error), result.Ok);",
                "Say(result.Ok ? PanelAddScreen.Resized(screen.Name, screen.SizeLabel, screen.Name) : PanelAddScreen.ResizeFailed(screen.Name, screen.SizeLabel), result.Ok);",
            })
            {
                AssertOnce(page, pin, "Screens.cs");
            }
            // The empty rig returns before anything that needs a selected screen.
            TextOrder.Before(page, "Ui.Prose(PanelCopy.EmptyRig", "BuildScreenHeader(screen)");
            Assert.Equal(6, PanelScreens.CardColumns);

            var round = ScreensSource("SettingsControl.Screens.Round.cs");
            AssertOnce(round, "var read = PanelScreens.CardsRead(screen); var rows = new List<UIElement>(); for (var slot = 1; slot <= read; slot++)", "Round.cs");
            AssertOnce(round, "PanelScreens.CardClash(Settings.Slots, read)", "Round.cs");
            var companion = ScreensSource("SettingsControl.Screens.Companion.cs");
            AssertOnce(companion, "var choices = PanelScreens.FirstModuleChoices(screen.Modules);", "Companion.cs");

            // A portrait wall draws only what matches its package: Page on screen and the page's picture are
            // the landscape wall's, and the portrait layout and the glance are one or the other.
            var wall = ScreensSource("SettingsControl.Screens.PitWall.cs");
            var landscapeFrom = wall.IndexOf("if (!portrait) {", System.StringComparison.Ordinal);
            var landscapeTo = wall.IndexOf("rows.Add(Ui.Anchor(Ui.SettingRow(PanelScreens.WebViewTitle", System.StringComparison.Ordinal);
            Assert.True(landscapeFrom >= 0 && landscapeTo > landscapeFrom);
            var landscape = wall.Substring(landscapeFrom, landscapeTo - landscapeFrom);
            Assert.Contains("Ui.SettingRow(PanelScreens.PitWallPageTitle, pages)", landscape);
            Assert.Contains("blocks.Add(BuildPitWallLayout(screen, page, redraw, content));", landscape);
            AssertOnce(wall, "PanelScreens.PitWallPageTitle", "PitWall.cs");
            AssertOnce(wall, "BuildPitWallLayout(screen, page, redraw, content)", "PitWall.cs");
            AssertOnce(wall, "if (portrait) { rows.Add(Ui.Anchor(Ui.SettingRow(PanelScreens.PortraitTitle, BuildPortraitLayout(screen, redraw, content), null, Ui.NewTag()), PanelScreens.AnchorPortrait)); } else { rows.Add(Ui.Anchor(Ui.SettingRow(PanelShortcuts.QuickGlanceTitle, BuildPitWallGlance(screen, redraw, content), PanelCopy.PitWallGlance), PanelScreens.AnchorGlance)); }", "PitWall.cs");
        }

        /// <summary>
        /// What the artboard and the rulings lay out, held where the page draws it: the face's four live rows in
        /// the artboard's order, First on the opening page only, the class filter only on a zone that lists
        /// cars, the face's flag display as two labels over its two values, and the NEW tags this release
        /// carries -- six, counted per file, so one is added or taken away on purpose.
        /// </summary>
        [Fact]
        public void The_page_lays_out_what_the_artboard_draws()
        {
            var face = ScreensSource("SettingsControl.Screens.Face.cs");
            var order = new[]
            {
                "Ui.SettingRow(PanelScreens.RevBarTitle, revBar)",
                "Ui.SettingRow(PanelScreens.FlagDisplayTitle, flags)",
                "Ui.SettingRow(PanelScreens.LapReviewTitle, lapReview, PanelScreens.LapReviewCaption)",
                "Ui.SettingRow(PanelShortcuts.QuickGlanceTitle, BuildFaceGlance(screen, redraw, column), PanelCopy.FaceGlance)",
                "rows.Add(Ui.SoonRow(PanelSoon.RevFill));",
            };
            var at = order.Select(pin => face.IndexOf(pin, System.StringComparison.Ordinal)).ToArray();
            Assert.DoesNotContain(-1, at);
            Assert.Equal(at.OrderBy(i => i), at);
            AssertOnce(face, "if (row.First) {", "Face.cs");
            AssertOnce(face, "if (FacePages.OffersClassFilter(letter)) {", "Face.cs");
            Assert.Equal(PanelScreens.FlagLabels.Length, Contract.FlagFormats.Length);
            Assert.Equal(PanelScreens.BarFlagLabels.Length, Contract.CompanionFlagFormats.Length);

            // Duplicate; the drag hint and Previous page; the portrait layout; the companion's Flag display and
            // Quick glance (the glance has no artboard: it is new on the companion in this release); and a
            // Porsche face's Crest row (#714), which has no artboard either.
            var tags = new Dictionary<string, int>
            {
                { "SettingsControl.Screens.cs", 1 },
                { "SettingsControl.Screens.Face.cs", 3 },
                { "SettingsControl.Screens.PitWall.cs", 1 },
                { "SettingsControl.Screens.Companion.cs", 2 },
                { "SettingsControl.Screens.Round.cs", 0 },
            };
            foreach (var file in tags)
            {
                var count = System.Text.RegularExpressions.Regex.Matches(ScreensSource(file.Key), System.Text.RegularExpressions.Regex.Escape("Ui.NewTag()")).Count;
                Assert.True(file.Value == count, file.Key + " draws " + count + " NEW tags, not " + file.Value);
            }
            Assert.Contains("Ui.HStack(8, Ui.Text(PanelScreens.DuplicateButton, Theme.SizeSmall, FontWeights.Medium, Theme.TextPrimary), Ui.NewTag())", ScreensSource("SettingsControl.Screens.cs"));
            Assert.Contains("Ui.Text(PanelScreens.DragHint, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary), Ui.NewTag()", face);
            Assert.Contains("Ui.Text(PanelScreens.PreviousPageTitle, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), Ui.NewTag()", face);
            var companion = ScreensSource("SettingsControl.Screens.Companion.cs");
            Assert.Contains("Ui.SettingRow(PanelScreens.FlagDisplayTitle, flags, null, Ui.NewTag())", companion);
            Assert.Contains("Ui.SettingRow(PanelShortcuts.QuickGlanceTitle, ScreensWrap(controls, glance, chip), PanelCopy.CompanionGlance, Ui.NewTag())", companion);
        }

        private static void AssertCovers(IEnumerable<string> names, Dictionary<string, string> writers, HashSet<string> published)
        {
            var all = names.ToList();
            Assert.Equal(all.OrderBy(n => n, System.StringComparer.Ordinal), writers.Keys.Concat(published).OrderBy(n => n, System.StringComparer.Ordinal));
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorAdd = screens.add",
                "AnchorCards = screens.cards",
                "AnchorClassOnly = screens.class-only",
                "AnchorDetails = screens.details",
                "AnchorFirstModule = screens.first-module",
                "AnchorFlagDisplay = screens.flag-display",
                "AnchorGlance = screens.glance",
                "AnchorInfoBar = screens.info-bar",
                "AnchorLapReview = screens.lap-review",
                "AnchorModules = screens.modules",
                "AnchorPaging = screens.paging",
                "AnchorPitWallPage = screens.pitwall-page",
                "AnchorPortrait = screens.portrait",
                "AnchorRevBar = screens.revbar",
                "AnchorRevRing = screens.rev-ring",
                "AnchorSlots = screens.slots",
                "AnchorWebView = screens.webview",
                "AnchorZonePaging = screens.zone-paging",
                "AnchorZones = screens.zones",
            }, AnchorTable.Of(typeof(PanelScreens)));
        }
    }
}
