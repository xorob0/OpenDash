// PanelCopyTests.cs: the panel's words, as a table.
//
// What a user reads is the deliverable as much as what the panel does, so the strings are pinned here
// rather than left in a WPF file no test can compile -- the same reason UpdateWordingTests pins the update
// sentences. The pairing tests are the point of the table: a verb with the wrong button style is a panel
// offering an update as though it were an afterthought, or a removal as though it were the thing to do.
using System;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelCopyTests
    {
        [Fact]
        public void The_add_card_says_what_it_adds()
        {
            Assert.Equal("Add a screen", PanelCopy.AddScreen);
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
            Assert.Equal("slots", PanelCopy.KindWord(Contract.KindSlots));
            Assert.Null(PanelCopy.KindWord(Contract.KindFace));
            Assert.Null(PanelCopy.KindWord(Contract.KindPitWall));
            Assert.Null(PanelCopy.KindWord(Contract.KindCompanion));

            Assert.Equal("slots · 1280 × 480", PanelCopy.SizeLine(Contract.KindSlots, "1280 × 480"));
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
