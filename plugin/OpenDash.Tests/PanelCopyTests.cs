// PanelCopyTests.cs: the panel's words, as a table.
//
// What a user reads is the deliverable as much as what the panel does, so the strings are pinned here
// rather than left in a WPF file no test can compile -- the same reason UpdateWordingTests pins the update
// sentences. The pairing tests are the point of the table: a verb with the wrong button style is a panel
// offering an update as though it were an afterthought, or a removal as though it were the thing to do.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelCopyTests
    {
        /// <summary>The dashed tile beside the screens says what it adds, and is drawn from the constant
        /// pinned here: PanelCopy.AddScreen went with the old add card, and the tile draws
        /// PanelAddScreen.SectionTitle, which is also its sheet's title.</summary>
        [Fact]
        public void The_add_card_says_what_it_adds()
        {
            Assert.Equal("Add a screen", PanelAddScreen.SectionTitle);
            var screens = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Screens.cs"));
            Assert.Contains("Ui.DashedAddCard(PanelAddScreen.SectionTitle,", screens);
        }

        /// <summary>
        /// The empty rig's sentence explains what adding a screen does and does not say the rig is empty,
        /// because the pill above it already has.
        /// </summary>
        [Fact]
        public void The_empty_rig_explains_rather_than_announcing()
        {
            Assert.DoesNotContain("no screens", PanelCopy.EmptyRig, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("empty", PanelCopy.EmptyRig, System.StringComparison.OrdinalIgnoreCase);
            // What adding one does is the card's own job to say, so the sentence is now the instruction
            // and nothing else; docs/design/voice.md is the rule.
            Assert.Equal("Add the screen your rig has.", PanelCopy.EmptyRig);
        }

        /// <summary>
        /// SimHub's new-plugin prompt prints the plugin's [PluginDescription], and printed the resource key
        /// "PluginDescription_OpenDash" while the class carried none (#475). OpenDash.cs references SimHub and
        /// is not compiled here, so the attribute is held as text, as the panel's sources are.
        /// </summary>
        [Fact]
        public void SimHubs_prompt_is_given_a_description_of_the_plugin()
        {
            Assert.Equal("Dashboards for the screens on your rig, and a page to choose what each one shows.", PanelCopy.PluginDescription);
            var plugin = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
            Assert.Contains("[PluginDescription(PanelCopy.PluginDescription)]", plugin);
        }

        [Fact]
        public void Progress_says_the_word_and_the_number_the_canvas_shows()
        {
            Assert.Equal("Installing", PanelCopy.Installing);
            Assert.Equal("62%", PanelCopy.Percent(0.62));
            Assert.Equal("0%", PanelCopy.Percent(0));
            Assert.Equal("100%", PanelCopy.Percent(1));
            Assert.Equal("100%", PanelCopy.Percent(4));
        }

        /// <summary>The kind the canvas gives no icon keeps a textual home on the card's second line,
        /// which is the only place the word "slots" is still said.</summary>
        [Fact]
        public void A_kind_without_an_icon_is_written_on_the_size_line()
        {
            Assert.Equal("Slots", PanelCopy.KindWord(Contract.KindSlots));
            Assert.Null(PanelCopy.KindWord(Contract.KindFace));
            Assert.Null(PanelCopy.KindWord(Contract.KindPitWall));
            Assert.Null(PanelCopy.KindWord(Contract.KindCompanion));

            Assert.Equal("Slots · 1280 × 480", PanelCopy.SizeLine(Contract.KindSlots, "1280 × 480"));
            Assert.Equal("1280 × 480", PanelCopy.SizeLine(Contract.KindFace, "1280 × 480"));
        }

        [Fact]
        public void An_installed_profile_names_the_version_it_is_at()
        {
            Assert.Equal("Installed · 0.4.0", PanelCopy.InstalledAt("0.4.0"));
            Assert.Equal("Installed", PanelCopy.InstalledAt(null));
            Assert.Equal("Installed", PanelCopy.InstalledAt(string.Empty));
        }

        /// <summary>
        /// The pairing the canvas draws: an older profile is the one thing on the page worth an accented
        /// button, and everything else asks with an outline.
        /// </summary>
        [Fact]
        public void An_older_light_profile_offers_an_update_and_it_is_the_primary()
        {
            var row = PanelCopy.LightRow(FlagBoxInstallState.Outdated, "0.4.0");
            Assert.Equal("Installed · 0.4.0", row.State);
            Assert.Equal(Theme.StatusUpToDate, row.StateHex);
            Assert.Equal("Update", row.Button);
            Assert.Equal(PanelButton.Primary, row.Style);
        }

        [Fact]
        public void An_uninstalled_profile_says_so_in_text_label_and_offers_an_outline_install()
        {
            var row = PanelCopy.LightRow(FlagBoxInstallState.NotInstalled, null);
            Assert.Equal("Not installed", row.State);
            Assert.Equal(Theme.TextLabel, row.StateHex);
            Assert.Equal("Install", row.Button);
            Assert.Equal(PanelButton.Outline, row.Style);
        }

        [Fact]
        public void A_profile_already_at_this_version_offers_a_reinstall_rather_than_an_update()
        {
            var row = PanelCopy.LightRow(FlagBoxInstallState.UpToDate, "0.5.0");
            Assert.Equal("Installed · 0.5.0", row.State);
            Assert.Equal("Reinstall", row.Button);
            Assert.Equal(PanelButton.Outline, row.Style);
        }

        [Fact]
        public void A_failed_install_says_it_failed_and_offers_the_same_press_again()
        {
            var row = PanelCopy.LightRow(FlagBoxInstallState.Failed, null);
            Assert.Equal("Install failed", row.State);
            Assert.Equal(Theme.StatusFailed, row.StateHex);
            Assert.Equal("Install", row.Button);
            Assert.Equal(PanelButton.Outline, row.Style);
        }

        /// <summary>Neither has a row of its own on the canvas, the section's own sentence covering both,
        /// so they read as not installed rather than as an error a user cannot act on.</summary>
        [Fact]
        public void Nothing_embedded_and_SimHub_unreachable_read_as_not_installed()
        {
            Assert.Equal("Not installed", PanelCopy.LightRow(FlagBoxInstallState.NotEmbedded, null).State);
            Assert.Equal("Not installed", PanelCopy.LightRow(FlagBoxInstallState.Unavailable, null).State);
        }

        /// <summary>Written and not yet drawn: plugin-63 has not settled whether the Install tab removes a
        /// screen. The table is what the answer will be wired to.</summary>
        [Fact]
        public void A_screen_row_pairs_installed_with_remove_and_neither_is_the_primary()
        {
            var installed = PanelCopy.ScreenRow(true);
            Assert.Equal("Installed", installed.State);
            Assert.Equal(Theme.StatusUpToDate, installed.StateHex);
            Assert.Equal("Remove", installed.Button);
            Assert.Equal(PanelButton.Outline, installed.Style);

            var absent = PanelCopy.ScreenRow(false);
            Assert.Equal("Not installed", absent.State);
            Assert.Equal(Theme.TextLabel, absent.StateHex);
            Assert.Equal("Add", absent.Button);
            Assert.Equal(PanelButton.Outline, absent.Style);
        }

        /// <summary>
        /// A glance row says the binding is a hold, since the binder rewrites whatever press type the
        /// dialog was set to (#435), and every glance binder on the panel sits under that sentence.
        /// </summary>
        [Fact]
        public void A_glance_row_says_it_is_bound_as_a_hold()
        {
            Assert.Equal("Bound as a hold, whatever press type you pick.", PanelCopy.GlanceBoundAsHold);
            Assert.Equal("Hold to show one page, release to return. Bound as a hold, whatever press type you pick.", PanelCopy.FaceGlance);
            Assert.Equal("Hold to show one page, release to put the zone back. Bound as a hold, whatever press type you pick.", PanelCopy.PitWallGlance);
            Assert.Equal("Hold to show one module, release to go back to the one you were on. Bound as a hold, whatever press type you pick.", PanelCopy.CompanionGlance);

            var panel = string.Join("\n", RepoPaths.SettingsControlCode());
            // The correction the sentence announces is still made, and made to the one press type
            // SimHub releases on.
            Assert.Contains("mapping.PressType = PressType.During", panel);
            var holds = Regex.Matches(panel, @"BuildBinder\([^;]*hold: true\)").Count;
            // A face, a pit wall and a companion.
            Assert.Equal(3, holds);
            Assert.Contains("Ui.Caption(PanelCopy.FaceGlance)", panel);
            Assert.Contains("Ui.Caption(PanelCopy.PitWallGlance)", panel);
            Assert.Contains("Ui.Caption(PanelCopy.CompanionGlance)", panel);
            Assert.DoesNotContain("\"Hold to show one page", panel);
        }

        /// <summary>
        /// The companion's paging names the place as well as the action: the device's own Controls and
        /// events, NextScreen and PreviousScreen, and that the binding belongs to the device (#435).
        /// </summary>
        [Fact]
        public void The_companion_paging_names_where_the_button_is_bound()
        {
            Assert.Equal(
                "Tap the left or right half of the screen to change module. For a wheel button, open the "
                + "device or window the companion runs on in SimHub, go to its Controls and events, and bind "
                + "NextScreen, with PreviousScreen to go back. Those bindings belong to that device, so the "
                + "button that pages the companion does not page your dash.",
                PanelCopy.CompanionPaging);
        }

        /// <summary>
        /// The guide says what the pane says. plugin/INSTALL.md is read before the plugin is open, so the
        /// place, the two actions and the per-device scope have to be there too; site/test/copy.test.ts
        /// holds the site's install page to the same.
        /// </summary>
        [Fact]
        public void The_guide_names_the_same_place_the_pane_does()
        {
            // Whitespace folded, because the guide wraps at a hundred columns and a phrase may break.
            var guide = Regex.Replace(File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "INSTALL.md")), @"\s+", " ");
            foreach (var phrase in new[] { "Controls and events", "NextScreen", "PreviousScreen", "does not page your dash", "device or window the companion runs on" })
            {
                Assert.Contains(phrase, PanelCopy.CompanionPaging);
                Assert.Contains(phrase, guide);
            }
        }

        /// <summary>One primary per panel: of every pairing the table holds, exactly one is accented.</summary>
        [Fact]
        public void Only_one_pairing_in_the_table_is_a_primary()
        {
            var primaries = 0;
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                if (PanelCopy.LightRow(state, "0.4.0").Style == PanelButton.Primary) primaries++;
            }
            if (PanelCopy.ScreenRow(true).Style == PanelButton.Primary) primaries++;
            if (PanelCopy.ScreenRow(false).Style == PanelButton.Primary) primaries++;
            Assert.Equal(1, primaries);
        }
    }
}
