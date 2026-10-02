// PanelPackageRowTests.cs: which screens on the rig count against a dashboard package.
//
// The counting is pinned because it is the one place where a package can be made to speak for screens
// that are not its own.
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

        /// <summary>
        /// The count is keyed on the package, so two packages that share a size keep their screens apart.
        /// </summary>
        [Fact]
        public void A_screen_counts_against_the_package_it_was_made_from()
        {
            var face = Package("OpenDash 850x480", Contract.KindFace, 850, 480);
            var companion = Package("OpenDash Companion", Contract.KindCompanion, 850, 480);
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
            var first = Package("OpenDash 850x480", Contract.KindFace, 850, 480, "first");
            var second = Package("OpenDash Rim", Contract.KindFace, 850, 480, "second");
            var rig = new List<ScreenInstance> { Screen(Contract.KindFace, 850, 480, "second") };

            Assert.Equal(0, PanelPackageRow.Uses(first, rig));
            Assert.Equal(1, PanelPackageRow.Uses(second, rig));
        }

        /// <summary>A screen saved before ADR 0017 has no package recorded, and the kind and the size are
        /// all there is to match it on. Losing it from the count would lose the fact the row carries.</summary>
        [Fact]
        public void A_screen_saved_before_the_package_was_recorded_falls_back_to_kind_and_size()
        {
            var face = Package("OpenDash 850x480", Contract.KindFace, 850, 480);
            var rig = new List<ScreenInstance>
            {
                Screen(Contract.KindFace, 850, 480, null),
                Screen(Contract.KindFace, 1280, 480, null),
            };

            Assert.Equal(1, PanelPackageRow.Uses(face, rig));
            Assert.Equal(0, PanelPackageRow.Uses(face, null));
            Assert.Equal(0, PanelPackageRow.Uses(null, rig));
        }
    }
}
