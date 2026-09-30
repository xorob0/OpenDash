// PanelPackageRow.cs: which screens on the rig count against a dashboard package.
//
// The Install tab's package rows, whose words this file held, are gone (#503): the Updates page draws a row
// per screen on the rig (PanelUpdates.DashboardRow). What is left is the counting, which a package row and
// the settings both lean on and PanelPackageRowTests pins. Pure: no WPF types.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelPackageRow
    {
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
    }
}
