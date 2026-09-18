// PanelPackageRow.cs: what a package row on the Install tab says, and which screens count against it.
//
// Apart from the WPF file for the reason PanelCopy.cs is apart from Widgets.cs: the panel is net48 and
// the net8.0 test project cannot compile a line of it, so the wording and the counting live where
// PanelPackageRowTests can pin them. Pure: no WPF types.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelPackageRow
    {
        /// <summary>
        /// The row's first line.
        /// </summary>
        /// <remarks>
        /// The canvas names six of the packages, "Main DDU", "Rim", "Pit wall" and the rest, and that
        /// table is a fact about a package rather than about a row, so it lives on the entry. The other
        /// eight keep the folder, which is the name SimHub's own dashboard list shows, so somebody
        /// looking for the thing they installed finds the same word in both places.
        /// </remarks>
        public static string Name(PackageEntry entry)
        {
            return entry == null ? string.Empty : entry.DisplayName ?? entry.Folder ?? string.Empty;
        }

        /// <summary>
        /// The row's second line: the size, behind the kind when the kind has no icon to be carried by.
        /// </summary>
        /// <remarks>
        /// The same rule the screen card follows, so that the one kind the canvas draws no icon for is
        /// written rather than lost. A package whose size could not be read has no size to show and
        /// "0 × 0" is worse than nothing, so it keeps the word alone.
        /// </remarks>
        public static string Caption(PackageEntry entry)
        {
            if (entry == null) return string.Empty;
            // The unreadable size is answered first, because the design's caption is a fixed string per
            // package and would otherwise state a size for a package that has none to state.
            if (entry.Width <= 0 || entry.Height <= 0) return PanelCopy.KindWord(entry.Kind) ?? string.Empty;
            // Then the design's own caption where it gives one, so that the round face reads
            // "480 round" as the product writes it rather than as its kind and its pixels.
            return entry.SizeCaption ?? PanelCopy.SizeLine(entry.Kind, entry.SizeLabel);
        }

        /// <summary>
        /// How many screens on the rig were made from this package.
        /// </summary>
        /// <remarks>
        /// Keyed on the package rather than on the kind and the size, so that a second screen made from a
        /// package attributes to its own row: two packages can share a size, and the row that offers one
        /// of them must not count the other's screens as its own.
        ///
        /// A screen saved before ADR 0017 has no package recorded, and there is nothing better to match
        /// it on than the kind and the size it was created at. That is the old rule, kept for the old
        /// records only.
        /// </remarks>
        public static int Uses(PackageEntry entry, IEnumerable<ScreenInstance> rig)
        {
            if (entry == null || rig == null) return 0;
            var uses = 0;
            foreach (var screen in rig)
            {
                if (screen == null) continue;
                if (!string.IsNullOrEmpty(screen.Package))
                {
                    if (string.Equals(screen.Package, entry.Package, StringComparison.Ordinal)) uses++;
                    continue;
                }
                if (string.Equals(screen.Kind, entry.Kind, StringComparison.Ordinal)
                    && screen.Width == entry.Width
                    && screen.Height == entry.Height)
                {
                    uses++;
                }
            }
            return uses;
        }

        /// <summary>
        /// What the row's tooltip says: where the package is written, and how much of the rig is made of it.
        /// </summary>
        /// <remarks>
        /// The folder is here because it is what somebody reads when an install goes wrong, and because
        /// the row's first line will stop being the folder as soon as the packages carry names of their
        /// own. The count is here because the pill answers a different question: the pill says whether
        /// DashTemplates has the folder, the count says how many screens were made from it, and under ADR
        /// 0017 neither answer covers the other.
        /// </remarks>
        public static string Tooltip(string folder, int uses)
        {
            var where = "Installed into DashTemplates\\" + (folder ?? string.Empty) + ". ";
            if (uses <= 0) return where + "Nothing on your rig uses it yet.";
            if (uses == 1) return where + "1 screen on your rig uses it.";
            return where + uses + " screens on your rig use it.";
        }

        /// <summary>
        /// The verb on the row's button.
        /// </summary>
        /// <remarks>
        /// "Add another" rather than "Add" once the rig has one, because a package that is already a
        /// screen can be made into a second and the button has to say that it will not replace the first.
        /// The canvas pairs an installed row with "Remove" instead, which PanelCopy.ScreenRow already
        /// names; whether the Install tab removes a screen at all is XOR's plugin-63 and is not settled,
        /// so the row keeps the verbs it has until it is.
        /// </remarks>
        public static string Verb(int uses)
        {
            return uses == 0 ? "Add" : "Add another";
        }
    }
}
