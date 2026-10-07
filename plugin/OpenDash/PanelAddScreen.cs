// PanelAddScreen.cs: what "add a screen" asks for, in the order it asks -- and what "edit a screen"
// asks once it is on the rig, which is the same two questions over again plus the dashboard itself.
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

    /// <summary>What pressing Save on the edit panel amounts to.</summary>
    public enum ScreenEdit
    {
        /// <summary>Nothing was answered differently, so nothing is written.</summary>
        None,

        /// <summary>
        /// The name alone -- which still writes the dashboard, because SimHub lists one by its title.
        /// </summary>
        Rename,

        /// <summary>The size, which writes the screen from another package and carries the name into it.</summary>
        Resize,
    }

    public static class PanelAddScreen
    {
        public const string SectionTitle = "Add a screen";

        /// <summary>The edit sheet's rows for a screen already on the rig: the size row asks what the Add
        /// sheet's second step asks, so it has the step's name.</summary>
        public const string SizeTitle = SizeStep;

        public const string OrientationTitle = "Orientation";

        public const string NameTitle = "Name";

        public const string NameCaption = "Also shown in SimHub's dashboard list.";

        /// <summary>The words on the orientation control, landscape first.</summary>
        public static readonly string[] OrientationLabels = { "Landscape", "Portrait" };

        public const string AddButton = "Add screen";

        /// <summary>
        /// A Duplicate that made nothing. DuplicateScreen returns null only when no package in this build makes
        /// the screen, and that reason is logged; the line points at the log, as voice.md's failure rule asks.
        /// </summary>
        public static string DuplicateFailed(string name)
        {
            return "Could not duplicate " + name + ". See SimHub's log.";
        }

        /// <summary>
        /// The one place a screen already on the rig is changed.
        /// </summary>
        /// <remarks>
        /// It was two links, Rename and "Change the size", which is two panels for one thought: a driver
        /// who has just found their screen listed under the wrong name is usually about to find it the
        /// wrong size as well, and neither link offered the third thing they came for, which is writing
        /// the dashboard again.
        /// </remarks>
        public const string EditTitle = "Edit";

        /// <summary>The edit sheet's title: "Edit Rim".</summary>
        public static string EditSheetTitle(string name)
        {
            return EditTitle + " " + name;
        }

        /// <summary>
        /// What an edit keeps, which is everything but the name and the pixels.
        /// </summary>
        /// <remarks>
        /// The namespace is frozen at creation (ADR 0017) and neither a rename nor a resize moves it, so
        /// a wheel button bound to this screen goes on working and so does anything bound to its
        /// properties. That is the whole reason this panel exists rather than "remove it and add the
        /// right one", which is what a driver who picked the wrong size had to do.
        /// </remarks>
        public const string EditCaption = "Your settings and bindings are kept.";

        /// <summary>The edit sheet's foot for this screen: <see cref="EditCaption"/> where the screen has
        /// settings of its own, and nothing for a round screen, whose cards are the rig's shared slots and
        /// which no wheel button is bound to, as PanelScreens.RemoveBody leaves them out.</summary>
        public static string EditCaptionFor(ScreenInstance screen)
        {
            return PanelScreens.OwnsSettings(screen) ? EditCaption : null;
        }

        public const string SaveButton = "Save";

        /// <summary>The title of the row the reinstall sits on: it acts on the dashboard rather than on
        /// either of the answers above it.</summary>
        public const string ReinstallTitle = "Dashboard";

        /// <summary>What a reinstall installs, which is the screen as it is saved: a name typed in the box above
        /// and not yet saved is not in it, and a kind that ships one package draws no size row to point at. In
        /// the verb of the button beside it, as a control's description is (voice.md); the button has no hover,
        /// since this caption already says it.</summary>
        public const string ReinstallCaption = "Reinstalls this screen's dashboard, at its saved name and size.";

        public const string ReinstallButton = "Reinstall";

        /// <summary>Save's hover, in Save's own verb and only what every Save does: a Save with nothing changed
        /// installs nothing, and a resize installs another package, so the line after the press (Renamed,
        /// Resized) says what was written.</summary>
        public const string SaveTooltip = "Saves your changes.";

        /// <summary>The edit sheet's Cancel, which has nothing to add and so does not say "adding".</summary>
        public const string EditCancelTooltip = "Goes back without changing anything.";

        /// <summary>
        /// What the caption says instead once this screen's folder has been edited.
        /// </summary>
        /// <remarks>
        /// Updates asks before it replaces authored work and keeps the copy under a name no later
        /// install claims. A reinstall of one screen costs exactly the same thing, so it says the same
        /// thing and keeps the copy the same way; "Put mine back" on the Updates page is what restores it.
        /// </remarks>
        public const string ReinstallEditedCaption = "You have edited this dashboard. Reinstalling replaces your version; a copy is kept, and \"Put mine back\" on the Updates page restores it.";

        /// <summary>
        /// The kinds the build can make a screen of, in the order the page offers them.
        /// </summary>
        /// <remarks>
        /// Derived from the packages rather than written down, so a build carrying no pit wall does not
        /// offer a pit wall: the census is what is embedded, which is the rule the Install tab's rows
        /// already follow. The order is AddScreen.dc.html's -- faces, pit walls, companions, then the round
        /// screens -- which is the order a rig is usually built in.
        /// </remarks>
        public static IReadOnlyList<ScreenType> Types(IEnumerable<PackageEntry> catalogue)
        {
            return Types(catalogue, null);
        }

        /// <summary>
        /// The kinds made by one theme's packages: the default look's for the Add sheet, which asks the theme after
        /// the size, and a themed screen's own for its edit sheet, whose size row offers only the sizes its theme
        /// is drawn at, so that a resize never quietly takes the theme away.
        /// </summary>
        public static IReadOnlyList<ScreenType> Types(IEnumerable<PackageEntry> catalogue, string theme)
        {
            var types = new List<ScreenType>();
            if (catalogue == null) return types;
            var entries = catalogue.Where(e => e != null && string.Equals(e.Theme, theme, StringComparison.Ordinal)).ToList();
            foreach (var kind in new[] { Contract.KindFace, Contract.KindPitWall, Contract.KindCompanion, Contract.KindSlots })
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
            if (string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal)) return KindName(kind);
            return "Dash face";
        }

        /// <summary>
        /// A kind's name where a screen already has one: the card's facts and the header under its name
        /// ("Round · 480 × 480"), as Screens.dc.html writes them. The Add sheet's tile says the same for the
        /// three the sheet asks about in the same words, and calls a face a "Dash face", as AddScreen.dc.html
        /// does. Never the internal kind id: "Slots" is the settings model's.
        /// </summary>
        public static string KindName(string kind)
        {
            if (string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal)) return "Companion";
            if (string.Equals(kind, Contract.KindPitWall, StringComparison.Ordinal)) return "Pit wall";
            if (string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal)) return "Round";
            return "Face";
        }

        /// <summary>The note under a kind's name on its tile: where a screen of that kind is found.</summary>
        public static string CaptionOf(string kind)
        {
            if (string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal)) return "Phone or tablet";
            if (string.Equals(kind, Contract.KindPitWall, StringComparison.Ordinal)) return "Monitor or TV";
            if (string.Equals(kind, Contract.KindSlots, StringComparison.Ordinal)) return "Cards on a round screen";
            return "Wheel or dash";
        }

        /// <summary>The greyed kind tile's note (#116).</summary>
        public const string FlagsScreenCaption = "A second display for flags";

        // --- The sheet's tiles (AddScreen.dc.html) ------------------------------------------------------

        /// <summary>The kind tiles: at least 140 wide, 8 apart, three to a row at most.</summary>
        public const double KindTileLeast = 140;

        public const int KindColumns = 3;

        /// <summary>The size tiles: at least 96 wide, 8 apart, four to a row at most.</summary>
        public const double SizeTileLeast = 96;

        public const int SizeColumns = 4;

        public const double TileGap = 8;

        /// <summary>A kind tile's name at 15 SemiBold over its note.</summary>
        public const double KindTitleSize = 15;

        /// <summary>A size tile: the screen's outline in a 44 px band (<see cref="TileShape"/> fits it) over its
        /// size at 14 in the display family.</summary>
        public const double SizeBand = 44;

        public const double SizeLabelSize = 14;

        // --- The sheet's three steps and its foot -----------------------------------------------------

        public const string KindStep = "Kind";

        public const string SizeStep = "Size";

        /// <summary>The second step's title: "Orientation" over a pair that is one screen either way up, as the
        /// edit sheet asks it, and "Size" otherwise.</summary>
        public static string SizeStepTitle(ScreenType type)
        {
            return Question(type) == SizeQuestion.Orientation ? OrientationTitle : SizeStep;
        }

        public const string NameStep = "Name";

        /// <summary>The foot's heading: a noun, not "What happens next".</summary>
        public const string NextStepsTitle = "Next steps";

        /// <summary>The foot's step, built from the name the screen will be added under (<see cref="NameFor"/>):
        /// SimHub lists the dashboard under it.</summary>
        public static string NextStep(string name)
        {
            return "Restart SimHub, then assign \"" + (name ?? string.Empty).Trim() + "\" to this display in Dash Studio.";
        }

        /// <summary>
        /// The foot's step for <paramref name="entry"/>: a themed screen is not assigned by hand, since the plugin
        /// binds it to its cars on every display showing a face of its size once SimHub lists it (PanelCarPlaylist).
        /// </summary>
        public static string NextStep(string name, PackageEntry entry)
        {
            if (entry == null || entry.Theme == null) return NextStep(name);
            return "Restart SimHub. A display showing a " + entry.SizeLabel + " face then switches to \"" + (name ?? string.Empty).Trim() + "\" in the cars it is drawn for.";
        }

        /// <summary>
        /// The name Add gives the screen for what is in the box: the box trimmed, or the size when it is empty,
        /// made distinct from every name on the rig -- what OpenDashSettings.AddScreen does with it.
        /// </summary>
        /// <remarks>
        /// The foot names this and not the box: a rig that already has a Rim adds "Rim (2)", and a cleared box
        /// adds "850 × 480", so a step built from the box sent the driver to assign another screen's
        /// dashboard, or one called "".
        /// </remarks>
        public static string NameFor(string typed, PackageEntry entry, IEnumerable<string> rig)
        {
            var wanted = string.IsNullOrWhiteSpace(typed) ? (entry == null ? string.Empty : entry.SizeLabel) : typed.Trim();
            return PackageCatalogue.UniqueName(wanted, rig);
        }

        /// <summary>
        /// Whether the name in the box is the driver's, after it changed: what they type while the box has the
        /// keyboard counts, and emptying it gives the box back to the defaults; a default the sheet wrote while
        /// the keyboard was elsewhere leaves the answer as it was.
        /// </summary>
        public static bool Typed(bool wasTyped, bool byDriver, string text)
        {
            return byDriver ? (text ?? string.Empty).Trim().Length > 0 : wasTyped;
        }

        /// <summary>What the box says after a kind or a size is picked: the driver's own name kept, never
        /// overwritten, and otherwise the new package's default made distinct on the rig.</summary>
        public static string FilledName(string current, bool typed, PackageEntry entry, IEnumerable<string> rig)
        {
            return typed ? current : PackageCatalogue.UniqueName(DefaultName(entry), rig);
        }

        /// <summary>Add screen's hover, in its own verb and the one the line after it uses ("Added Rim.").</summary>
        public const string AddTooltip = "Adds the screen and installs its dashboard.";

        public const string CancelButton = "Cancel";

        public const string CancelTooltip = "Goes back without adding anything.";

        /// <summary>What the sheet says in a build carrying no dashboard at all.</summary>
        public const string NothingToAdd = "This build ships no dashboards.";

        /// <summary>The outline a size tile draws inside its 44 px band: AddScreen.dc.html's own formula, a
        /// wide screen squeezed and a tall one stretched so both read as their shape at a glance, and a
        /// round one a circle.</summary>
        public static double[] TileShape(int width, int height)
        {
            if (width <= 0 || height <= 0) return new double[] { 40, 40 };
            var w = width / 2.2;
            var k = 40 / Math.Max(w, height);
            var shapeWidth = Math.Min(Math.Round(w * k * 1.6), 96);
            var shapeHeight = Math.Max(Math.Min(Math.Round(height * k * 0.9), 40), 12);
            if (width == height) shapeWidth = shapeHeight = 36;
            return new[] { shapeWidth, shapeHeight };
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

        /// <summary>
        /// The sizes in the order AddScreen.dc.html's step 2 draws them, width then height: the reference face,
        /// the three 1280s, the 850 x 480, the Nano, the display dash, then the round.
        /// </summary>
        public static readonly int[][] SizeOrder =
        {
            new[] { 1920, 480 }, new[] { 1280, 480 }, new[] { 1280, 400 }, new[] { 1280, 720 },
            new[] { 850, 480 }, new[] { 800, 286 }, new[] { 600, 686 }, new[] { 480, 480 },
        };

        /// <summary>
        /// A type's packages in the order the control offers them: landscape first for a pair that is one
        /// screen either way up, and the artboard's order of sizes otherwise.
        /// </summary>
        /// <remarks>
        /// The catalogue's own order puts the packages the design names first (the Main DDU, the Rim, the
        /// Nano), which is the order of the Install list and not of the Add sheet's tiles. A size the artboard
        /// does not draw keeps its place in the catalogue, after the ones it does.
        /// </remarks>
        public static IReadOnlyList<PackageEntry> Offered(ScreenType type)
        {
            if (type == null) return new PackageEntry[0];
            if (Question(type) == SizeQuestion.Orientation) return type.Entries.OrderByDescending(e => e.Width).ToList();
            return type.Entries.OrderBy(SizeRank).ToList();
        }

        private static int SizeRank(PackageEntry entry)
        {
            for (var i = 0; i < SizeOrder.Length; i++)
            {
                if (entry.Width == SizeOrder[i][0] && entry.Height == SizeOrder[i][1]) return i;
            }
            return SizeOrder.Length;
        }

        /// <summary>
        /// Which of the offered sizes the dialog opens on.
        /// </summary>
        /// <remarks>
        /// The preferred face where this type offers it, and the first entry otherwise. A dialog that
        /// opened on whatever happened to be first in the catalogue offered the 1920 x 480 to everybody,
        /// and the size most of these screens actually are is the 850 x 480: it is what OpenDash is
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
            // writes it -- and every round screen the same way; the pixels otherwise, which is what a
            // driver measures their screen in.
            if (entry.SizeCaption != null) return entry.SizeCaption;
            if (string.Equals(entry.Kind, Contract.KindSlots, StringComparison.Ordinal) && entry.Width > 0 && entry.Width == entry.Height)
            {
                return entry.Width + " round";
            }
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
            // A themed screen is listed in SimHub under this name, beside the default face of its size, so the name
            // says which of the two it is (ADR 0016).
            if (entry.Theme != null) return ThemeName(entry) + " " + entry.SizeLabel;
            // NameFor and not DisplayName: DisplayName falls back to the folder, which is the right
            // answer on the Install tab -- SimHub's own list prints that word -- and the wrong one here,
            // where a folder is a path and a name is what the driver will read on the card.
            var named = PackageCatalogue.NameFor(entry.Folder);
            if (!string.IsNullOrEmpty(named)) return named;
            return entry.Width > 0 ? entry.SizeLabel : entry.Folder ?? string.Empty;
        }

        // --- The third question: the theme, asked only of a size that has one --------------------------------

        public const string ThemeStep = "Theme";

        /// <summary>The name step's number: third, or fourth where the sheet asks the theme.</summary>
        public static int NameStepNumber(bool themeAsked)
        {
            return themeAsked ? 4 : 3;
        }

        /// <summary>
        /// The packages a size can be drawn in, the default look first and then each theme the build carries at that
        /// size, in the catalogue's order; one entry, the size's own, where no theme is drawn at it.
        /// </summary>
        /// <remarks>
        /// From the packages the build carries rather than from the catalogue, as the kinds and the sizes are: a
        /// theme the catalogue lists before its code exists has no package to write, and offering it would offer an
        /// add that fails.
        /// </remarks>
        public static IReadOnlyList<PackageEntry> Themes(IEnumerable<PackageEntry> catalogue, PackageEntry entry)
        {
            if (entry == null) return new PackageEntry[0];
            var themed = (catalogue ?? Enumerable.Empty<PackageEntry>())
                .Where(e => e != null && e.Theme != null
                    && string.Equals(e.Kind, entry.Kind, StringComparison.Ordinal)
                    && e.Width == entry.Width && e.Height == entry.Height)
                .OrderBy(e => ThemeRank(e.Theme))
                .ToList();
            var all = new List<PackageEntry> { entry };
            all.AddRange(themed);
            return all;
        }

        /// <summary>Whether the sheet asks the theme of a size: only where there is more than the default to pick.</summary>
        public static bool AsksTheme(IReadOnlyList<PackageEntry> themes)
        {
            return themes != null && themes.Count > 1;
        }

        /// <summary>
        /// The theme the sheet holds once a size is picked: the one the driver had picked, where the new size is drawn
        /// in it too, and the default look otherwise.
        /// </summary>
        public static PackageEntry ThemeKept(IReadOnlyList<PackageEntry> themes, string picked)
        {
            if (themes == null || themes.Count == 0) return null;
            return themes.FirstOrDefault(e => picked != null && string.Equals(e.Theme, picked, StringComparison.Ordinal)) ?? themes[0];
        }

        /// <summary>A theme tile's name: the theme's own, and the product's for the default look.</summary>
        public static string ThemeName(PackageEntry entry)
        {
            var id = entry == null || entry.Theme == null ? Contract.DefaultThemeId : entry.Theme;
            var theme = Contract.Themes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
            return theme == null ? id : theme.Name;
        }

        private static int ThemeRank(string id)
        {
            for (var i = 0; i < Contract.Themes.Count; i++)
            {
                if (string.Equals(Contract.Themes[i].Id, id, StringComparison.Ordinal)) return i;
            }
            return Contract.Themes.Count;
        }

        /// <summary>
        /// The line under the questions, saying what pressing the button will do.
        /// </summary>
        /// <remarks>
        /// Another screen of a size is the case worth saying out loud: it gets a copy of the dashboard and its
        /// own settings, which is the whole of ADR 0017 and is invisible from the outside until somebody
        /// wonders why their two rims cycle together. What the driver needs from that is the consequence, that
        /// the two can show different pages (modules, on a companion), never the settings group behind it
        /// (voice.md, What never appears). <paramref name="second"/> says only that the size's own settings are
        /// taken, so the line counts nothing: the third screen at a size is not the second, and a second added
        /// after the first was removed takes the first's place.
        ///
        /// The screen is named the way the sheet asked about it: by its size where the tiles show sizes, and
        /// by its kind where step 2 asked Landscape or Portrait and no size is drawn anywhere -- a second
        /// landscape companion is not "This 850 × 480", which is the Rim face's size.
        /// </remarks>
        public static string Note(ScreenType type, PackageEntry entry, bool second)
        {
            if (entry == null || !second) return string.Empty;
            // Not a card face's: every card face reads the same twelve slots, so a second one gets a dashboard of its
            // own and no settings at all, and the line would promise what it does not do (#474).
            if (string.Equals(entry.Kind, Contract.KindSlots, StringComparison.Ordinal)) return string.Empty;
            var thing = Question(type) == SizeQuestion.Orientation ? KindName(entry.Kind).ToLowerInvariant() : entry.SizeLabel;
            // What it shows in its own editor's word: a companion shows modules, a face and a pit wall pages.
            var shows = string.Equals(entry.Kind, Contract.KindCompanion, StringComparison.Ordinal) ? "modules" : "pages";
            return "This " + thing + " can show different " + shows + " from any other " + thing + " on your rig.";
        }

        /// <summary>
        /// Whether adding <paramref name="entry"/> to <paramref name="rig"/> makes a screen with settings of its
        /// own beside another of its size, which is <see cref="Note"/>'s <c>second</c>: the first screen of a
        /// kind and size takes its stock namespace, so the stock namespace already being on the rig is the case.
        /// </summary>
        public static bool SettingsTaken(PackageEntry entry, IEnumerable<ScreenInstance> rig)
        {
            if (entry == null || rig == null) return false;
            var stock = new ScreenInstance { Kind = entry.Kind, Width = entry.Width, Height = entry.Height, Folder = entry.Folder }.StockNamespace;
            return rig.Any(screen => screen != null && string.Equals(screen.Namespace, stock, StringComparison.Ordinal));
        }

        /// <summary>What the panel says once the screen exists, which is the two steps SimHub does not
        /// take for you.</summary>
        public static string Added(string name, string title)
        {
            return "Added " + name + ". Restart SimHub, then assign \"" + title + "\" to this display in Dash Studio.";
        }

        /// <summary><see cref="Added(string, string)"/> for a themed screen, whose remaining step is the restart alone.</summary>
        public static string Added(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            if (screen.Theme == null) return Added(screen.Name, screen.Name);
            return "Added " + screen.Name + ". Restart SimHub, and a display showing a " + screen.SizeLabel + " face switches to it in the cars it is drawn for.";
        }

        /// <summary>An add whose dashboard was not written: the screen is on the rig, and the reason is in
        /// SimHub's log (voice.md), as <see cref="DuplicateFailed"/> and PanelLights.BarAddFailed say it.</summary>
        public static string AddFailed(string name)
        {
            return "Added " + name + ", but its dashboard could not be installed. See SimHub's log.";
        }

        public static string Resized(string name, string size, string title)
        {
            return name + " is now " + size + ". Restart SimHub, then assign \"" + title + "\" to this display again in Dash Studio.";
        }

        /// <summary>A resize whose dashboard was not installed. The resize itself happened -- the old folder is
        /// gone and the screen holds its new size, which the header and the card then show -- so the line says
        /// so and admits the half that did not land, as <see cref="AddFailed"/> does.</summary>
        public static string ResizeFailed(string name, string size)
        {
            return name + " is now " + size + ", but its dashboard could not be installed. See SimHub's log.";
        }

        /// <summary>
        /// The size the edit sheet's row opens on: the screen's own among <paramref name="offered"/>, or -1 where
        /// this build offers no package at the screen's size.
        /// </summary>
        /// <remarks>
        /// The case is real: a card face a migration brought onto the rig at "OpenDash slots 1280x480" is a size
        /// no build embeds, and a face size a later build drops is another. The row used to open on the first
        /// size offered and hold it as the answer, so pressing Save only to rename such a screen resized it to
        /// a package the driver never picked and removed its dashboard folder on the way.
        /// </remarks>
        public static int OpensOn(IReadOnlyList<PackageEntry> offered, int width, int height)
        {
            if (offered == null) return -1;
            for (var i = 0; i < offered.Count; i++)
            {
                if (offered[i] != null && offered[i].Width == width && offered[i].Height == height) return i;
            }
            return -1;
        }

        /// <summary>
        /// The choices the edit sheet's size row draws: the sizes offered, led by the screen's own size (a null
        /// entry, drawn as its size label) where that is not among them, so the row says what the screen is and
        /// Save keeps it until another size is picked.
        /// </summary>
        public static IReadOnlyList<PackageEntry> EditSizes(IReadOnlyList<PackageEntry> offered, int width, int height)
        {
            if (offered == null) return new PackageEntry[] { null };
            if (OpensOn(offered, width, height) >= 0) return offered;
            return new PackageEntry[] { null }.Concat(offered).ToList();
        }

        /// <summary>Whether Save resizes the screen: only to a size that was picked and that the screen is not.
        /// The screen's own size, and a row nobody touched, keep it.</summary>
        public static bool Resizes(PackageEntry chosen, int width, int height)
        {
            return chosen != null && (chosen.Width != width || chosen.Height != height);
        }

        /// <summary>
        /// What an edit amounts to, from the two answers the panel is holding.
        /// </summary>
        /// <remarks>
        /// A size that moved decides on its own, because the folder is written from the other package and
        /// the name goes into it on the way: there is never a rename left to do separately. A name box
        /// left empty is not a request to call the screen nothing, it is somebody who cleared it and
        /// thought better of it, so it reads as no change rather than as an error to report back.
        /// </remarks>
        public static ScreenEdit Edit(string name, string wanted, bool sizeChanged)
        {
            if (sizeChanged) return ScreenEdit.Resize;
            var trimmed = (wanted ?? string.Empty).Trim();
            if (trimmed.Length == 0) return ScreenEdit.None;
            return string.Equals(trimmed, (name ?? string.Empty).Trim(), StringComparison.Ordinal)
                ? ScreenEdit.None
                : ScreenEdit.Rename;
        }

        /// <summary>
        /// What a rename says, which is that it reached SimHub and when SimHub will read it.
        /// </summary>
        /// <remarks>
        /// A rename used to change the card and nothing else, while its own tooltip promised SimHub's
        /// dashboard list as well. It writes the dashboard now, so the promise is kept -- but SimHub
        /// reads that list once, at startup, so the line has to say what the driver is waiting for.
        /// </remarks>
        public static string Renamed(string title)
        {
            return "Renamed to " + title + ". Restart SimHub to see the new name in Dash Studio.";
        }

        /// <summary>A rename whose dashboard was not installed, from the name the screen now has, as
        /// <see cref="Renamed"/> writes it: "Renamed to Wheel", never "Renamed Wheel".</summary>
        public static string RenameFailed(string title)
        {
            return "Renamed to " + title + ", but its dashboard could not be installed. See SimHub's log.";
        }

        public static string Reinstalled(string name)
        {
            return "Installed " + name + "'s dashboard again. Restart SimHub to load it.";
        }

        /// <summary>
        /// A reinstall that did not write the dashboard, pointing at the log rather than repeating it: the
        /// installer's error can be a sentence of its own naming the settings model's kind ("This build
        /// ships no package for a 1280 × 480 slots."), which is for a contributor.
        /// </summary>
        /// <remarks>
        /// Two arguments still, because the shell's InstallScreenAgain calls it with the error it logs; the
        /// error is not drawn.
        /// </remarks>
        public static string ReinstallFailed(string name, string error)
        {
            return "Could not install " + name + "'s dashboard. See SimHub's log.";
        }
    }
}
