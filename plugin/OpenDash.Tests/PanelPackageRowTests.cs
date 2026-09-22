// PanelPackageRowTests.cs: what a package row on the Install tab says, and what it counts.
//
// The wording is pinned verbatim for the reason PanelCopyTests pins the rest of the panel's words: what a
// user reads is the deliverable as much as what the panel does. The counting is pinned because it is the
// one place where a row can be made to speak for a package that is not its own.
using System.Collections.Generic;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelPackageRowTests
    {
        private static PackageEntry Package(string folder, string kind, int width, int height, string package = null)
        {
            return new PackageEntry
            {
                Package = package ?? "OpenDashPlugin.Resources." + folder + ".simhubdash",
                Folder = folder,
                Kind = kind,
                Width = width,
                Height = height,
            };
        }

        private static ScreenInstance Screen(string kind, int width, int height, string package)
        {
            return new ScreenInstance { Kind = kind, Width = width, Height = height, Package = package };
        }

        /// <summary>A package the design names reads by that name; one it does not keeps its folder.</summary>
        [Fact]
        public void The_row_names_the_package_and_captions_it_with_the_size()
        {
            var named = Package("openDash 850x480", Contract.KindFace, 850, 480);
            Assert.Equal("Rim", PanelPackageRow.Name(named));
            Assert.Equal("850 × 480", PanelPackageRow.Caption(named));

            var unnamed = Package("openDash 1280x720", Contract.KindFace, 1280, 720);
            Assert.Equal("openDash 1280x720", PanelPackageRow.Name(unnamed));
            Assert.Equal("1280 × 720", PanelPackageRow.Caption(unnamed));
        }

        /// <summary>The same rule the screen card follows: a kind the canvas draws no icon for is written
        /// on the size line instead, so it is never neither drawn nor written.</summary>
        [Fact]
        public void A_kind_without_an_icon_is_written_on_the_caption()
        {
            // The round face is the one package whose caption the design writes itself, so it reads
            // "480 round" rather than its kind and its pixels; the kind is carried by that word.
            Assert.Equal("480 round", PanelPackageRow.Caption(Package("openDash 480 round", Contract.KindSlots, 480, 480)));
            Assert.Equal("slots · 800 × 800", PanelPackageRow.Caption(Package("openDash 800 round", Contract.KindSlots, 800, 800)));
            Assert.Equal("1920 × 1080", PanelPackageRow.Caption(Package("openDash Pit wall", Contract.KindPitWall, 1920, 1080)));
        }

        /// <summary>"0 × 0" is worse than nothing for a package whose size could not be read.</summary>
        [Fact]
        public void A_package_with_no_readable_size_shows_no_size()
        {
            Assert.Equal(string.Empty, PanelPackageRow.Caption(Package("openDash Companion", Contract.KindCompanion, 0, 0)));
            Assert.Equal("slots", PanelPackageRow.Caption(Package("openDash 800 round", Contract.KindSlots, 0, 0)));
            Assert.Equal(string.Empty, PanelPackageRow.Caption(null));
            Assert.Equal(string.Empty, PanelPackageRow.Name(null));
        }

        /// <summary>
        /// The count is keyed on the package, so two packages that share a size keep their screens apart.
        /// </summary>
        [Fact]
        public void A_screen_counts_against_the_package_it_was_made_from()
        {
            var face = Package("openDash 850x480", Contract.KindFace, 850, 480);
            var companion = Package("openDash Companion", Contract.KindCompanion, 850, 480);
            var rig = new List<ScreenInstance>
            {
                Screen(Contract.KindFace, 850, 480, face.Package),
                Screen(Contract.KindFace, 850, 480, face.Package),
                Screen(Contract.KindCompanion, 850, 480, companion.Package),
            };

            Assert.Equal(2, PanelPackageRow.Uses(face, rig));
            Assert.Equal(1, PanelPackageRow.Uses(companion, rig));
        }

        /// <summary>Two packages of one kind at one size are the case the old kind-and-size match could not
        /// tell apart, and the case that decided the key.</summary>
        [Fact]
        public void Two_packages_of_one_kind_at_one_size_do_not_share_a_count()
        {
            var first = Package("openDash 850x480", Contract.KindFace, 850, 480, "first");
            var second = Package("openDash Rim", Contract.KindFace, 850, 480, "second");
            var rig = new List<ScreenInstance> { Screen(Contract.KindFace, 850, 480, "second") };

            Assert.Equal(0, PanelPackageRow.Uses(first, rig));
            Assert.Equal(1, PanelPackageRow.Uses(second, rig));
        }

        /// <summary>A screen saved before ADR 0017 has no package recorded, and the kind and the size are
        /// all there is to match it on. Losing it from the count would lose the fact the row carries.</summary>
        [Fact]
        public void A_screen_saved_before_the_package_was_recorded_falls_back_to_kind_and_size()
        {
            var face = Package("openDash 850x480", Contract.KindFace, 850, 480);
            var rig = new List<ScreenInstance>
            {
                Screen(Contract.KindFace, 850, 480, null),
                Screen(Contract.KindFace, 1280, 480, null),
            };

            Assert.Equal(1, PanelPackageRow.Uses(face, rig));
            Assert.Equal(0, PanelPackageRow.Uses(face, null));
            Assert.Equal(0, PanelPackageRow.Uses(null, rig));
        }

        /// <summary>The folder is the only place the panel says where a package lands, and it is what
        /// somebody reads when the install went somewhere they did not expect.</summary>
        [Fact]
        public void The_tooltip_keeps_the_count_the_pill_cannot_carry()
        {
            // The folder is gone from it: the pill already answers whether DashTemplates holds the
            // package, and a path nobody types is not something a tooltip is for. What is left is the
            // question the pill cannot answer, which is how many screens were made from this one.
            Assert.Equal("Nothing on your rig uses it yet.", PanelPackageRow.Tooltip(0));
            Assert.Equal("1 screen on your rig uses it.", PanelPackageRow.Tooltip(1));
            Assert.Equal("3 screens on your rig use it.", PanelPackageRow.Tooltip(3));
        }

        /// <summary>The verb says that a second screen is added rather than the first replaced. The canvas
        /// pairs an installed row with "Remove", which plugin-63 has not settled, so the row keeps these.</summary>
        [Fact]
        public void The_button_says_it_adds_another_once_the_rig_has_one()
        {
            Assert.Equal("Add", PanelPackageRow.Verb(0));
            Assert.Equal("Add another", PanelPackageRow.Verb(1));
            Assert.Equal("Add another", PanelPackageRow.Verb(4));
        }

        /// <summary>The state beside the verb is PanelCopy's, so the row cannot grow words of its own:
        /// "Not installed" in text.label beside a status.notInstalled dot is plugin-64's whole pairing.</summary>
        [Fact]
        public void The_state_the_row_shows_comes_from_the_copy_table()
        {
            Assert.Equal("Not installed", PanelCopy.ScreenRow(false).State);
            Assert.Equal(Theme.TextLabel, PanelCopy.ScreenRow(false).StateHex);
            Assert.Equal("#5A6069", Theme.TextLabel);
            Assert.Equal("#33383F", Theme.StatusNotInstalled);

            Assert.Equal("Installed", PanelCopy.ScreenRow(true).State);
            Assert.Equal("#00D96A", PanelCopy.ScreenRow(true).StateHex);
        }
    }
}
