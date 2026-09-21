// PanelAddScreen.cs: what "add a screen" asks for, in the order it asks.
//
// Apart from the WPF file for the reason PanelCopy.cs is apart from Widgets.cs: the panel is net48 and
// the net8.0 test project cannot compile a line of it, so the questions, their wording and the rule
// deciding which of them appear live where PanelAddScreenTests can pin them. Pure: no WPF types.
//
// The shape of the flow is the whole point of this file. It used to be one drop-down of every package
// the build carries -- "1920 × 480 · face", "480 × 850 · companion", "800 × 800 · slots" -- which asks
// somebody to answer two questions at once in a vocabulary they have no reason to know. A driver knows
// what *kind* of screen they are adding before they know anything else, and for two of the four kinds
// there is no resolution to pick at all: a pit wall is a pit wall, and the only thing to say about it is
// which way round it is.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One kind of screen a driver can add, and the packages that make it.</summary>
    public sealed class ScreenType
    {
        public ScreenType(string kind, string label, string caption, IReadOnlyList<PackageEntry> entries)
        {
            Kind = kind;
            Label = label;
            Caption = caption;
            Entries = entries;
        }

        /// <summary>One of Contract.ScreenKinds.</summary>
        public string Kind { get; private set; }

        /// <summary>How the type is written on its own button.</summary>
        public string Label { get; private set; }

        /// <summary>The one line under the row, saying what this kind of screen is.</summary>
        public string Caption { get; private set; }

        /// <summary>The packages of this kind the build carries, in the order they are offered.</summary>
        public IReadOnlyList<PackageEntry> Entries { get; private set; }
    }

    /// <summary>How the second question is asked, once the kind is known.</summary>
    public enum SizeQuestion
    {
        /// <summary>There is only one package of this kind: nothing to ask.</summary>
        None,

        /// <summary>The same screen either way up, so the question is which way up rather than how big.</summary>
        Orientation,

        /// <summary>Genuinely different sizes, and the driver has to say which one their screen is.</summary>
        Size,
    }

    public static class PanelAddScreen
    {
        public const string SectionTitle = "Add a screen";

        public const string TypeTitle = "Screen type";

        public const string TypeCaption = "Pick the kind of display this is.";

        public const string SizeTitle = "Screen size";

        public const string SizeCaption = "Pick your screen's real resolution. SimHub does not scale dashboards, so the wrong size looks wrong.";

        public const string OrientationTitle = "Orientation";

        public const string OrientationCaption = "How the screen is mounted.";

        public const string NameTitle = "Name";

        public const string NameCaption = "Shown here and in SimHub's dashboard list. You can change it later.";

        /// <summary>The words on the orientation control, landscape first.</summary>
        public static readonly string[] OrientationLabels = { "Landscape", "Portrait" };

        public const string AddButton = "Add screen";

        public const string ResizeTitle = "Change the size";

        /// <summary>
        /// What a resize keeps, which is everything but the pixels.
        /// </summary>
        /// <remarks>
        /// The namespace is frozen at creation (ADR 0017) and a resize does not move it either, so a
        /// wheel button bound to this screen goes on working and so does anything bound to its
        /// properties. That is the whole reason a resize exists rather than "remove it and add the
        /// right one", which is what a driver who picked the wrong size had to do.
        /// </remarks>
        public const string ResizeCaption =
            "Your zones, bar and buttons stay as they are, and this screen's properties keep the names they "
            + "have, so nothing you bound to it stops working. Only the dashboard in SimHub is rewritten.";

        /// <summary>
        /// The kinds the build can make a screen of, in the order the page offers them.
        /// </summary>
        /// <remarks>
        /// Derived from the packages rather than written down, so a build carrying no pit wall does not
        /// offer a pit wall: the census is what is embedded, which is the rule the Install tab's rows
        /// already follow. The order is PackageCatalogue's own -- faces, companions, pit walls, then the
        /// card model -- because that is the order a rig is usually built in.
        /// </remarks>
        public static IReadOnlyList<ScreenType> Types(IEnumerable<PackageEntry> catalogue)
        {
            var types = new List<ScreenType>();
            if (catalogue == null) return types;
            var entries = catalogue.Where(e => e != null).ToList();
            foreach (var kind in new[] { Contract.KindFace, Contract.KindCompanion, Contract.KindPitWall, Contract.KindSlots })
            {
                var of = entries.Where(e => string.Equals(e.Kind, kind, StringComparison.Ordinal)).ToList();
                if (of.Count == 0) continue;
                types.Add(new ScreenType(kind, LabelOf(kind), CaptionOf(kind), of));
            }
            return types;
        }

        public static string LabelOf(string kind)
        {
            if (string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal)) return "Companion";
            if (string.Equals(kind, Contract.KindPitWall, StringComparison.Ordinal)) return "Pit wall";
            if (string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal)) return "Card face";
            return "Dash or wheel";
        }

        public static string CaptionOf(string kind)
        {
            if (string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal))
            {
                return "A phone or tablet beside the wheel, showing one module at a time.";
            }
            if (string.Equals(kind, Contract.KindPitWall, StringComparison.Ordinal))
            {
                return "A monitor for your engineer: the field, the timing and the telemetry.";
            }
            if (string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal))
            {
                return "Round faces on the older card layout.";
            }
            return "The screen in front of the driver: rev bar, car settings, three zones and a flag band.";
        }

        /// <summary>
        /// How to ask which of a type's packages the driver wants, or whether to ask at all.
        /// </summary>
        /// <remarks>
        /// Two packages that are each other transposed are one screen mounted two ways, which is what
        /// the companion and the pit wall are, and asking a driver to choose between "1920 × 1080" and
        /// "1080 × 1920" makes them do the arithmetic to find out that is what the question was.
        /// </remarks>
        public static SizeQuestion Question(ScreenType type)
        {
            if (type == null || type.Entries.Count <= 1) return SizeQuestion.None;
            if (type.Entries.Count == 2 && Transposed(type.Entries[0], type.Entries[1])) return SizeQuestion.Orientation;
            return SizeQuestion.Size;
        }

        private static bool Transposed(PackageEntry a, PackageEntry b)
        {
            return a.Width > 0 && a.Height > 0 && a.Width == b.Height && a.Height == b.Width && a.Width != a.Height;
        }

        /// <summary>A type's packages in the order the control offers them: landscape first for a pair
        /// that is one screen either way up, the catalogue's own order otherwise.</summary>
        public static IReadOnlyList<PackageEntry> Offered(ScreenType type)
        {
            if (type == null) return new PackageEntry[0];
            if (Question(type) != SizeQuestion.Orientation) return type.Entries;
            return type.Entries.OrderByDescending(e => e.Width).ToList();
        }

        /// <summary>
        /// Which of the offered sizes the dialog opens on.
        /// </summary>
        /// <remarks>
        /// The preferred face where this type offers it, and the first entry otherwise. A dialog that
        /// opened on whatever happened to be first in the catalogue offered the 1920 x 480 to everybody,
        /// and the size most of these screens actually are is the 850 x 480: it is what openDash is
        /// tested on and what its users own. A driver whose screen is something else still has to say
        /// so, which the caption already asks them to do.
        /// </remarks>
        public static int PreferredIndex(ScreenType type)
        {
            var offered = Offered(type);
            for (var i = 0; i < offered.Count; i++)
            {
                if (offered[i].Width == Contract.PreferredFaceWidth && offered[i].Height == Contract.PreferredFaceHeight) return i;
            }
            return 0;
        }

        /// <summary>How one package is written in the size control.</summary>
        public static string SizeLabel(ScreenType type, PackageEntry entry, int index)
        {
            if (entry == null) return string.Empty;
            if (Question(type) == SizeQuestion.Orientation)
            {
                return index >= 0 && index < OrientationLabels.Length ? OrientationLabels[index] : entry.SizeLabel;
            }
            // The design's own caption where a package has one -- "480 round" reads as the product
            // writes it -- and the pixels otherwise, which is what a driver measures their screen in.
            if (entry.SizeCaption != null) return entry.SizeCaption;
            return entry.Width > 0 ? entry.SizeLabel : entry.Folder ?? string.Empty;
        }

        /// <summary>
        /// The name the box is filled with, which a driver may keep or type over.
        /// </summary>
        /// <remarks>
        /// The package's own display name where the design gives it one -- "Rim", "Main DDU" -- because
        /// that is a better answer than its pixels to "what is this screen". The size otherwise, and a
        /// numeral appended by the caller where the rig already holds that name.
        /// </remarks>
        public static string DefaultName(PackageEntry entry)
        {
            if (entry == null) return string.Empty;
            // NameFor and not DisplayName: DisplayName falls back to the folder, which is the right
            // answer on the Install tab -- SimHub's own list prints that word -- and the wrong one here,
            // where a folder is a path and a name is what the driver will read on the card.
            var named = PackageCatalogue.NameFor(entry.Folder);
            if (!string.IsNullOrEmpty(named)) return named;
            return entry.Width > 0 ? entry.SizeLabel : entry.Folder ?? string.Empty;
        }

        /// <summary>
        /// The line under the questions, saying what pressing the button will do.
        /// </summary>
        /// <remarks>
        /// The second screen at a size is the case worth saying out loud: it gets a copy of the
        /// dashboard and a settings group of its own, which is the whole of ADR 0017 and is invisible
        /// from the outside until somebody wonders why their two rims cycle together.
        /// </remarks>
        public static string Note(PackageEntry entry, bool second)
        {
            if (entry == null) return string.Empty;
            return second
                ? "This is your second " + entry.SizeLabel + ", so it gets a dashboard and settings of its own. "
                    + "The two screens page independently."
                : "openDash will install its dashboard into SimHub for you.";
        }

        /// <summary>What the panel says once the screen exists, which is the two steps SimHub does not
        /// take for you.</summary>
        public static string Added(string name, string title)
        {
            return "Added " + name + ". Restart SimHub, then assign \"" + title + "\" to this display in Dash Studio.";
        }

        public static string AddFailed(string name, string error)
        {
            return "Added " + name + ", but its dashboard could not be installed: " + error;
        }

        public static string Resized(string name, string size, string title)
        {
            return name + " is now " + size + ". Restart SimHub, then assign \"" + title + "\" to this display again in Dash Studio.";
        }

        public static string ResizeFailed(string name, string error)
        {
            return "Could not resize " + name + ": " + error;
        }
    }
}
