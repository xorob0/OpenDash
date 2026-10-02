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
            var plugin = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
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
            // By its one name, as the card and the Add tile write it; never the settings model's "Slots".
            Assert.Equal("Round", PanelCopy.KindWord(Contract.KindSlots));
            Assert.Equal(PanelAddScreen.KindName(Contract.KindSlots), PanelCopy.KindWord(Contract.KindSlots));
            Assert.Null(PanelCopy.KindWord(Contract.KindFace));
            Assert.Null(PanelCopy.KindWord(Contract.KindPitWall));
            Assert.Null(PanelCopy.KindWord(Contract.KindCompanion));

            Assert.Equal("Round · 1280 × 480", PanelCopy.SizeLine(Contract.KindSlots, "1280 × 480"));
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
        /// The Updates table's words for an older profile: "Update available" in the update ink, as the
        /// artboard draws "Out of date", with the version in a column of its own. Its Update is the one press
        /// the table carries.
        /// </summary>
        [Fact]
        public void An_older_light_profile_says_an_update_is_available_and_offers_its_update()
        {
            var row = PanelCopy.LightRow(FlagBoxInstallState.Outdated, "0.4.0");
            Assert.Equal("Update available", row.State);
            Assert.Equal(Theme.StatusUpdateAvailable, row.StateHex);
            Assert.Equal("Update", row.Button);
            Assert.Equal(PanelUpdates.RowUpdate, row.Button);
        }

        /// <summary>Only an older profile has a press on the Updates table: a missing, failed or current one
        /// is Reinstall everything's to write, and the table offers no Install or Reinstall of its own.</summary>
        [Fact]
        public void Every_other_light_state_is_said_in_its_ink_and_offers_no_press()
        {
            var none = PanelCopy.LightRow(FlagBoxInstallState.NotInstalled, null);
            Assert.Equal("Not installed", none.State);
            Assert.Equal(Theme.TextLabel, none.StateHex);
            Assert.Null(none.Button);

            var current = PanelCopy.LightRow(FlagBoxInstallState.UpToDate, "0.5.0");
            Assert.Equal("Up to date", current.State);
            Assert.Equal(Theme.StatusUpToDate, current.StateHex);
            Assert.Null(current.Button);

            var failed = PanelCopy.LightRow(FlagBoxInstallState.Failed, null);
            Assert.Equal("Install failed", failed.State);
            Assert.Equal(Theme.StatusFailed, failed.StateHex);
            Assert.Null(failed.Button);
        }

        /// <summary>One word per thing (voice.md): a light profile's state is said in the words a
        /// dashboard's is, InstallStatus.Label, and the version is not part of it.</summary>
        [Fact]
        public void A_light_row_says_its_state_in_the_dashboards_words()
        {
            Assert.Equal(InstallStatus.UpdateAvailable.Label(), PanelCopy.LightRow(FlagBoxInstallState.Outdated, "0.4.0").State);
            Assert.Equal(InstallStatus.UpToDate.Label(), PanelCopy.LightRow(FlagBoxInstallState.UpToDate, "0.4.0").State);
            Assert.Equal(InstallStatus.Failed.Label(), PanelCopy.LightRow(FlagBoxInstallState.Failed, "0.4.0").State);
            Assert.Equal(InstallStatus.NotInstalled.Label(), PanelCopy.LightRow(FlagBoxInstallState.NotInstalled, "0.4.0").State);
            Assert.DoesNotContain("0.4.0", PanelCopy.LightRow(FlagBoxInstallState.Outdated, "0.4.0").State);
        }

        /// <summary>The table's one press is drawn from PanelUpdates' OffersUpdate, never from this row's Button or
        /// Style, and every state's style stays an outline: the page's one primary is Download.</summary>
        [Fact]
        public void No_light_state_is_a_primary()
        {
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                Assert.Equal(PanelButton.Outline, PanelCopy.LightRow(state, "0.4.0").Style);
            }
        }

        /// <summary>What the page cannot know it does not claim: with SimHub's settings out of reach, or no
        /// profile in this build to compare with, the row reads "Unknown", the table's word for a version it
        /// cannot read, and the row's hover says why. A strip is the exception for a profile the build does not
        /// ship, which reads "Not installed" as the LEDs card and Home say it (#524, ruling 2).</summary>
        [Fact]
        public void Nothing_embedded_and_SimHub_unreachable_read_as_unknown()
        {
            Assert.Equal("Unknown", PanelCopy.LightRow(FlagBoxInstallState.NotEmbedded, null).State);
            Assert.Equal(PanelCopy.NotInstalled, PanelCopy.StripRow(FlagBoxInstallState.NotEmbedded, null).State);
            Assert.Equal("Unknown", PanelCopy.StripRow(FlagBoxInstallState.Unavailable, null).State);
            Assert.Equal("Unknown", PanelCopy.LightRow(FlagBoxInstallState.Unavailable, null).State);
            Assert.Equal(PanelUpdates.Unknown, PanelCopy.LightRow(FlagBoxInstallState.Unavailable, null).State);
            Assert.Equal(Theme.TextLabel, PanelCopy.LightRow(FlagBoxInstallState.Unavailable, null).StateHex);
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

        /// <summary>The screen package row nobody drew is gone (#523): the Screens page's cards and the Updates
        /// table say a screen's state, and a table of words no page reads only drifts from theirs.</summary>
        [Fact]
        public void No_undrawn_screen_row_is_kept()
        {
            Assert.Null(typeof(PanelCopy).GetMethod("ScreenRow"));
        }
    }
}
