// PanelSearchTests.cs: how the sidebar's search ranks what it finds, and that every page's rows are in it.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelSearchTests
    {
        private static readonly PanelSearch.Entry[] Entries =
        {
            new PanelSearch.Entry("Model year", PanelPage.Home, null),
            new PanelSearch.Entry("Delta reference", PanelPage.Settings, "settings.race-data", "lap"),
            new PanelSearch.Entry("Night mode", PanelPage.Settings, "settings.lighting", "dark"),
            new PanelSearch.Entry("Idle display", PanelPage.Matrix, null, "at rest", "gear"),
            new PanelSearch.Entry("Redline flash", PanelPage.Matrix, null, "gear"),
        };

        [Fact]
        public void A_word_that_starts_with_the_query_outranks_a_label_that_contains_it()
        {
            var hits = PanelSearch.Find(Entries, "del");
            // "Delta reference" starts a word with it; "Model year" only contains it.
            Assert.Equal(new[] { "Delta reference", "Model year" }, hits.Select(h => h.Label));
            Assert.Equal(new[] { 0, 1 }, hits.Select(h => h.Rank));
        }

        [Fact]
        public void A_keyword_ranks_last_and_the_pages_order_holds_within_a_rank()
        {
            var hits = PanelSearch.Find(Entries, "gear");
            Assert.Equal(new[] { "Idle display", "Redline flash" }, hits.Select(h => h.Label));
            Assert.All(hits, hit => Assert.Equal(2, hit.Rank));
        }

        [Fact]
        public void A_later_word_of_the_label_counts_as_a_start()
        {
            Assert.Equal(0, PanelSearch.Rank(Entries[2], "mode"));
            Assert.Equal(0, PanelSearch.Rank(Entries[1], "REF"));
        }

        [Fact]
        public void Case_and_the_space_around_a_query_are_ignored_and_an_empty_query_finds_nothing()
        {
            Assert.Single(PanelSearch.Find(Entries, "  NIGHT "));
            Assert.Empty(PanelSearch.Find(Entries, ""));
            Assert.Empty(PanelSearch.Find(Entries, "   "));
            Assert.Empty(PanelSearch.Find(Entries, null));
            Assert.Empty(PanelSearch.Find(Entries, "zzz"));
        }

        [Fact]
        public void At_most_eight_are_listed()
        {
            var many = Enumerable.Range(0, 20).Select(i => new PanelSearch.Entry("Row " + i, PanelPage.Home, null)).ToArray();
            Assert.Equal(8, PanelSearch.Find(many, "row").Count);
            Assert.Equal(3, PanelSearch.Find(many, "row", 3).Count);
        }

        [Fact]
        public void A_hit_reads_as_its_label_and_its_page_and_leads_to_its_row()
        {
            var hit = PanelSearch.Find(Entries, "night").Single();
            Assert.Equal("Night mode · Settings", hit.Text);
            // A page's own title is said once: "Home", not "Home · Home".
            foreach (var title in new[] { PanelHome.Title, PanelRigMap.Title, PanelScreens.Title })
            {
                var own = PanelSearch.All().First(entry => entry.Label == title && PanelNav.Label(entry.Route.Page) == title);
                Assert.Equal(title, new PanelSearch.Hit(own, 0).Text);
            }
            Assert.Equal(PanelRoute.To(PanelPage.Settings, "settings.lighting"), hit.Route);
            Assert.Equal("No setting matches.", PanelSearch.NoMatch);
        }

        [Fact]
        public void Every_page_contributes_and_every_entry_names_its_own_page()
        {
            var all = PanelSearch.All().ToList();
            foreach (var page in Enum.GetValues(typeof(PanelPage)).Cast<PanelPage>())
            {
                Assert.Contains(all, entry => entry.Route.Page == page);
            }
            Assert.All(PanelHome.Search, e => Assert.Equal(PanelPage.Home, e.Route.Page));
            Assert.All(PanelRigMap.Search, e => Assert.Equal(PanelPage.Rig, e.Route.Page));
            Assert.All(PanelScreens.Search, e => Assert.Equal(PanelPage.Screens, e.Route.Page));
            Assert.All(PanelLeds.Search, e => Assert.Equal(PanelPage.Leds, e.Route.Page));
            Assert.All(PanelMatrix.Search, e => Assert.Equal(PanelPage.Matrix, e.Route.Page));
            Assert.All(PanelShortcuts.Search, e => Assert.Equal(PanelPage.Shortcuts, e.Route.Page));
            Assert.All(PanelSettings.Search, e => Assert.Equal(PanelPage.Settings, e.Route.Page));
            Assert.All(PanelUpdates.Search, e => Assert.Equal(PanelPage.Updates, e.Route.Page));
        }

        [Fact]
        public void The_settings_that_moved_off_the_old_tabs_are_found_where_they_went()
        {
            Assert.Equal(PanelPage.Settings, PanelSearch.Find(PanelSearch.All(), "delta precision").First().Route.Page);
            Assert.Equal(PanelPage.Settings, PanelSearch.Find(PanelSearch.All(), "flags in the pit lane").First().Route.Page);
            Assert.Equal(PanelPage.Leds, PanelSearch.Find(PanelSearch.All(), "centre display").First().Route.Page);
            Assert.Equal(PanelPage.Updates, PanelSearch.Find(PanelSearch.All(), "put mine back").First().Route.Page);
        }

        [Fact]
        public void An_entry_needs_a_label_and_a_route()
        {
            Assert.Throws<ArgumentException>(() => new PanelSearch.Entry(" ", PanelRoute.Home));
            Assert.Throws<ArgumentNullException>(() => new PanelSearch.Entry("Row", (PanelRoute)null));
        }

        /// <summary>A greyed row a page draws is listed, so somebody looking for it finds that it is coming,
        /// and a hit lands on its page and its row; a row no page draws yet is not, since its hit would land
        /// on nothing.</summary>
        [Fact]
        public void Every_drawn_greyed_row_is_found_by_its_title_and_leads_to_its_own_row()
        {
            var all = PanelSearch.All().ToList();
            foreach (var item in PanelSoon.All)
            {
                var listed = all.Any(entry => entry.Label == item.Title && entry.Route.Page == item.Page && entry.Route.Anchor == item.Anchor);
                Assert.True(listed == PanelSearch.Lists(item), item.Title + (listed ? " is listed and drawn nowhere" : " is drawn and not listed"));
            }
            foreach (var query in new[] { "real hardware", "rig test", "alert dismissal" })
            {
                Assert.NotEmpty(PanelSearch.Find(all, query));
            }
            // #739 and #85 are only ever drawn inside the Add sheet, which search cannot open: not listed,
            // however many pages draw them.
            Assert.True(PanelSoon.FlagsScreen.InSheetOnly && PanelSoon.YourDisplays.InSheetOnly);
            Assert.False(PanelSearch.Lists(PanelSoon.FlagsScreen));
            Assert.False(PanelSearch.Lists(PanelSoon.YourDisplays));
            Assert.Empty(PanelSearch.Find(all, "flags screen"));
            Assert.Empty(PanelSearch.Find(all, "your displays"));
            // Listed once: the Rig page no longer types its greyed switch in beside the registry's entry.
            Assert.Single(all, entry => entry.Label == PanelRigMap.RealHardwareTitle);
        }

        /// <summary>
        /// Search lists only what the rig draws: a row of the selected strip, of the selected matrix, of a kind
        /// of screen, or of a screen's shortcut card is left out of a rig without one, since its hit would land at
        /// the top of a page with no such row. A rig with one of everything lists every entry.
        /// </summary>
        [Fact]
        public void Search_lists_only_the_rows_the_rig_draws()
        {
            var empty = new OpenDashSettings();
            empty.Normalise();
            var bare = PanelSearch.For(empty).ToList();
            var stripRows = new[]
            {
                PanelLeds.RevLightsTitle, PanelLeds.CarRevLightsTitle, PanelLeds.CentreDisplayTitle, PanelLights.BarDeviceTitle,
                PanelLeds.BrightnessTitle, PanelLeds.ReverseTitle, PanelLeds.FlagAnimationTitle, PanelLeds.SpotterTitle,
                PanelLeds.MirrorFitTitle, PanelSoon.EachLedInTurn.Title, PanelSoon.PitLimiterLights.Title,
            }.Concat(PanelLeds.Effects.Select(effect => effect.Label));
            foreach (var label in stripRows) Assert.DoesNotContain(bare, entry => entry.Route.Page == PanelPage.Leds && entry.Label == label);
            foreach (var label in new[] { PanelLights.AddBar, PanelLeds.EveryStripTitle, PanelLights.CarTablesTitle, PanelSoon.IdleSweep.Title })
            {
                Assert.Contains(bare, entry => entry.Route.Page == PanelPage.Leds && entry.Label == label);
            }
            var matrixRows = new[]
            {
                PanelMatrix.PriorityTitle, PanelMatrix.FlagsTitle, PanelMatrix.PitLaneTitle, PanelMatrix.SpotterTitle, PanelMatrix.SpotterAnimationTitle,
                PanelMatrix.WarningsTitle, PanelMatrix.IdleDisplayTitle, PanelMatrix.ShiftColoursTitle, PanelMatrix.RedlineFlashTitle, PanelSoon.SimHubDevice.Title,
            };
            foreach (var label in matrixRows) Assert.DoesNotContain(bare, entry => entry.Route.Page == PanelPage.Matrix && entry.Label == label);
            Assert.Contains(bare, entry => entry.Label == PanelMatrix.AddPanel);
            Assert.Contains(bare, entry => entry.Label == FlagBoxProfile.ProfileName);
            foreach (var label in new[] { PanelScreens.ModulesTitle, PanelScreens.FirstModuleTitle, PanelScreens.PortraitTitle, PanelScreens.WebViewTitle, PanelScreens.RevRingTitle, PanelScreens.RevBarTitle })
            {
                Assert.DoesNotContain(bare, entry => entry.Route.Page == PanelPage.Screens && entry.Label == label);
            }
            Assert.Contains(bare, entry => entry.Route.Page == PanelPage.Screens && entry.Route.Anchor == PanelScreens.AnchorCards);
            Assert.DoesNotContain(bare, entry => entry.Route.Anchor == PanelShortcuts.AnchorScreens);
            Assert.Contains(bare, entry => entry.Route.Anchor == PanelShortcuts.AnchorRig);
            Assert.Empty(PanelSearch.Find(bare, "reverse direction"));

            // One of everything: every entry is drawn somewhere.
            var full = new OpenDashSettings();
            full.Normalise();
            full.AddLedBar("3-9-3", "Rim", null).RpmStyle = Contract.LedRpmStyleCar;
            full.AddLedBar("0-15-0", "Bar", null);
            full.AddMatrixPanel("Flags");
            full.Rig = new List<ScreenInstance>
            {
                new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Namespace = "Face" },
                new ScreenInstance { Kind = Contract.KindPitWall, Width = 1920, Height = 1080, Namespace = "Wall" },
                new ScreenInstance { Kind = Contract.KindPitWall, Width = 1080, Height = 1920, Namespace = "Tall" },
                new ScreenInstance { Kind = Contract.KindCompanion, Width = 850, Height = 480, Namespace = "Phone" },
                new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480, Namespace = "Round" },
            };
            var missing = PanelSearch.All().Where(entry => !PanelSearch.Drawn(entry, full)).Select(entry => entry.Label + " · " + entry.Route.Page).ToList();
            Assert.True(missing.Count == 0, "rows a full rig does not draw: " + string.Join(", ", missing));
            Assert.Equal(PanelSearch.All().Count(), PanelSearch.For(null).Count());
        }

        /// <summary>A route to a row of one strip opens a strip that draws it, as ScreenFor does for a screen:
        /// the selected one where it does, the first that does otherwise.</summary>
        [Fact]
        public void A_route_to_a_strips_row_opens_a_strip_that_draws_it()
        {
            var plain = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3", RpmStyle = Contract.LedRpmStyleLeftToRight };
            var fanatec = new LedBar { Name = "Wheel", Namespace = "LedWheel", Shape = "3-9-3-fanatec", RpmStyle = Contract.LedRpmStyleLeftToRight };
            var bare = new LedBar { Name = "Bar", Namespace = "LedBar", Shape = "0-15-0", RpmStyle = Contract.LedRpmStyleCar };
            var bars = new List<LedBar> { fanatec, bare, plain };
            // A Fanatec wheel's wiring is its own; a bare run has a reversed twin, and is the first that does.
            Assert.Same(bare, PanelLeds.StripFor(PanelLeds.AnchorReverse, PanelLeds.ReverseTitle, bars, fanatec));
            Assert.Same(plain, PanelLeds.StripFor(PanelLeds.AnchorReverse, PanelLeds.ReverseTitle, bars, plain));
            Assert.Same(fanatec, PanelLeds.StripFor(PanelLeds.AnchorSpotter, PanelLeds.SpotterTitle, bars, fanatec));
            Assert.Same(fanatec, PanelLeds.StripFor(PanelLeds.AnchorSpotter, PanelLeds.SpotterTitle, bars, bare));
            Assert.Same(bare, PanelLeds.StripFor(PanelLeds.AnchorMirrorFit, PanelLeds.MirrorFitTitle, bars, plain));
            Assert.Same(plain, PanelLeds.StripFor(PanelLeds.AnchorBrightness, PanelLeds.BrightnessTitle, bars, plain));
            // An effect a bare run does not carry opens a strip with ends; one it carries keeps the selection.
            var aid = PanelLeds.Effects.First(effect => !PanelLeds.EffectsFor("0-15-0").Any(carried => carried.Label == effect.Label));
            Assert.Same(fanatec, PanelLeds.StripFor(PanelLeds.AnchorEffects, aid.Label, bars, bare));
            Assert.Same(bare, PanelLeds.StripFor(PanelLeds.AnchorEffects, PanelLeds.EffectsTitle, bars, bare));
            // A row outside the selected strip's section never moves the selection.
            Assert.Same(bare, PanelLeds.StripFor(PanelLeds.AnchorCarTables, PanelLights.CarTablesTitle, bars, bare));
            Assert.False(PanelLeds.SearchDrawn(PanelLeds.AnchorReverse, PanelLeds.ReverseTitle, new[] { fanatec }));
            Assert.True(PanelLeds.SearchDrawn(PanelLeds.AnchorReverse, PanelLeds.ReverseTitle, new[] { fanatec, plain }));
            Assert.False(PanelLeds.SearchDrawn(PanelLeds.AnchorSpotter, PanelLeds.SpotterTitle, new[] { bare }));
        }

        /// <summary>A result is labelled by the row or heading it lands on, so the words in the list are the
        /// words on the page; other words for it are keywords.</summary>
        [Fact]
        public void A_result_names_what_the_page_draws()
        {
            var labels = PanelSearch.All().Select(entry => entry.Label).ToList();
            foreach (var invented in new[] { "Rig layout", "Your screens", "Flag box profile", "Wheel buttons", "Night mode button", "Brightness buttons", "Car-specific thresholds" })
            {
                Assert.DoesNotContain(invented, labels);
            }
            Assert.Contains(PanelShortcuts.RigActionLabel(Contract.BrightnessUpAction), labels);
            Assert.Contains(PanelMatrix.CarShiftPointsTitle, labels);
            Assert.Contains(FlagBoxProfile.ProfileName, labels);
            // The words that were the labels still find them.
            Assert.NotEmpty(PanelSearch.Find(PanelSearch.All(), "wheel buttons"));
            Assert.NotEmpty(PanelSearch.Find(PanelSearch.All(), "flag box profile"));
        }
            /// <summary>The files that build each page, which a label has to be drawn from.</summary>
        private static readonly Dictionary<PanelPage, string> PageSources = new Dictionary<PanelPage, string>
        {
            { PanelPage.Home, "SettingsControl.Home" },
            { PanelPage.Rig, "SettingsControl.Rig" },
            { PanelPage.Screens, "SettingsControl.Screens" },
            { PanelPage.Leds, "SettingsControl.Lights" },
            { PanelPage.Matrix, "SettingsControl.Matrix" },
            { PanelPage.Shortcuts, "SettingsControl.Shortcuts" },
            { PanelPage.Settings, "SettingsControl.Settings" },
            { PanelPage.Updates, "SettingsControl.Updates" },
        };

        /// <summary>The page model whose SearchDrawnOtherwise lists the labels that page draws through
        /// something other than the constant: each page keeps its own, with the reason.</summary>
        private static readonly Dictionary<PanelPage, Type> PageModels = new Dictionary<PanelPage, Type>
        {
            { PanelPage.Home, typeof(PanelHome) },
            { PanelPage.Rig, typeof(PanelRigMap) },
            { PanelPage.Screens, typeof(PanelScreens) },
            { PanelPage.Leds, typeof(PanelLeds) },
            { PanelPage.Matrix, typeof(PanelMatrix) },
            { PanelPage.Shortcuts, typeof(PanelShortcuts) },
            { PanelPage.Settings, typeof(PanelSettings) },
            { PanelPage.Updates, typeof(PanelUpdates) },
        };

        private static IReadOnlyDictionary<string, string> DrawnOtherwise(PanelPage page)
        {
            return (IReadOnlyDictionary<string, string>)PageModels[page].GetField("SearchDrawnOtherwise").GetValue(null);
        }

        /// <summary>
        /// Every search label on a page is a constant that page's own files draw, so a page agent who rewords
        /// a row rewords its search entry with it: a label typed a second time in Panel&lt;Page&gt;.Search drifts
        /// from the page and no other test notices.
        /// </summary>
        [Fact]
        public void Every_search_label_is_a_constant_its_page_draws()
        {
            var sources = PageSources.ToDictionary(
                pair => pair.Key,
                pair => string.Join("\n", Directory.GetFiles(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash"), pair.Value + "*.cs").Select(RepoPaths.Code)));
            var constants = typeof(PanelSearch).Assembly.GetTypes()
                .Where(type => type.Namespace == "OpenDashPlugin" && type.IsAbstract && type.IsSealed)
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.FieldType == typeof(string) && (field.IsLiteral || field.IsInitOnly))
                    .Select(field => new { Name = type.Name + "." + field.Name, Value = (string)field.GetValue(null) }))
                .ToList();
            var missing = new List<string>();
            foreach (var entry in PanelSearch.All().Except(PanelSearch.Soon))
            {
                var page = entry.Route.Page;
                var text = sources[page];
                string otherwise;
                if (DrawnOtherwise(page).TryGetValue(entry.Label, out otherwise) && text.Contains(otherwise)) continue;
                var drawn = constants.Where(c => c.Value == entry.Label).Any(c => text.Contains(c.Name));
                if (!drawn) missing.Add(entry.Label + " · " + page);
            }
            Assert.True(missing.Count == 0, "search labels no page draws from a constant:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
        }
    }
}
