// PanelLightRows.cs: which light profiles share a row on the Install tab, what that row is called and
// what its caption says.
//
// Apart from SettingsControl.Install.Lights.cs for the reason PanelCopy.cs is apart from Widgets.cs: the
// section is WPF and the net8.0 test project cannot compile a line of it, so everything a test can hold
// lives here and the section draws from it. Pure: no WPF types.
//
// THE CENSUS IS WHAT THE BUILD EMBEDDED, and nothing here lists a shape. FlagBoxProfile.StripResourceNames
// reads the profile resources out of the assembly, ShapeIdOf gives each one the id the generator wrote it
// under, and the profile's own Name is what the row is called -- so the panel offers exactly the shapes its
// own build carries and a shape added to packages/dash/src/leds/strip.ts arrives here with no edit at all.
// The geometry a row groups on is read back out of the id, which is the generator's own
// `${left}-${centre}-${right}` and `brow-${n}`.
//
// The one thing that IS mirrored is <see cref="NamedShapes"/>, the shapes the canvas captions by
// device family, because the device names are in strip.ts and in no artefact the plugin embeds.
// PanelLightRowsTests holds that mirror against strip.ts in both directions: a caption naming a device the
// generator does not, or a generator row naming a device no caption does, fails there rather than shipping
// a panel that says the wrong hardware.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One embedded profile, reduced to what a row needs: which shape it is for and what the
    /// build called it.</summary>
    public sealed class LightProfile
    {
        public LightProfile(string shapeId, string profileName)
        {
            ShapeId = shapeId;
            ProfileName = profileName;
        }

        /// <summary>The generator's own id, "4-14-4" or "brow-9", from the resource's file name.</summary>
        public string ShapeId { get; private set; }

        /// <summary>The profile's Name field, "openDash 4/14/4". Null when it could not be read.</summary>
        public string ProfileName { get; private set; }
    }

    /// <summary>A shape id read back as the geometry the generator wrote it from.</summary>
    public sealed class LightShape
    {
        private LightShape(string id, string placement, int left, int centre, int right, string wiring)
        {
            Id = id;
            Placement = placement;
            Left = left;
            Centre = centre;
            Right = right;
            Wiring = wiring;
        }

        public string Id { get; private set; }
        public string Placement { get; private set; }
        public int Left { get; private set; }
        public int Centre { get; private set; }
        public int Right { get; private set; }
        /// <summary>The suffix the id carries where the geometry alone does not name the profile: the
        /// generator spells one shape twice when a maker wires it in an order of its own. Null for the
        /// plain wiring.</summary>
        public string Wiring { get; private set; }

        public bool Reversed
        {
            get { return Wiring == PanelLightRows.ReversedSuffix; }
        }

        /// <summary>A bare run: nothing at either end, which is what a brow is as well.</summary>
        public bool Bare
        {
            get { return Left == 0 && Right == 0; }
        }

        /// <summary>
        /// The id as geometry, or null when it is not one of the two shapes the generator writes.
        /// </summary>
        /// <remarks>
        /// The other half of `wheel()` and `fanatec()` in packages/dash/src/leds/strip.ts, which
        /// spell the id `${left}-${centre}-${right}` with a wiring suffix appended and `brow-${n}`. Reading it
        /// back rather than carrying a table is what lets a shape added there be grouped here without an
        /// edit; an id this cannot read is not guessed at, it gets a row of its own.
        /// </remarks>
        public static LightShape Parse(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith(PanelLightRows.BrowPrefix, StringComparison.Ordinal))
            {
                int length;
                if (!Number(id.Substring(PanelLightRows.BrowPrefix.Length), out length)) return null;
                return new LightShape(id, PanelLightRows.Brow, 0, length, 0, null);
            }

            var parts = id.Split('-');
            var wiring = parts.Length == 4 && PanelLightRows.WiringSuffixes.Contains(parts[3]) ? parts[3] : null;
            if (parts.Length != 3 && wiring == null) return null;
            int left, centre, right;
            if (!Number(parts[0], out left) || !Number(parts[1], out centre) || !Number(parts[2], out right)) return null;
            return new LightShape(id, PanelLightRows.Wheel, left, centre, right, wiring);
        }

        private static bool Number(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>One row of the Lights section: what it is called, what it is, and which shapes it installs.</summary>
    public sealed class LightRowPlan
    {
        public LightRowPlan(string name, string caption, IReadOnlyList<string> shapeIds)
        {
            Name = name;
            Caption = caption;
            ShapeIds = shapeIds;
        }

        public string Name { get; private set; }
        public string Caption { get; private set; }

        /// <summary>The shapes one press installs, in the order the row names them. One for most rows;
        /// three, five or seven for the grouped ones.</summary>
        public IReadOnlyList<string> ShapeIds { get; private set; }
    }

    public static class PanelLightRows
    {
        /// <summary>The section's own sentence, which is the rule ADR 0013's amendment records and the
        /// reason nothing is written to SimHub until a button is pressed.</summary>
        public const string SectionCaption = "openDash never installs a profile on its own.";

        /// <summary>What the flag box row is under its name. The profile's own Name carries the name.</summary>
        public const string FlagBoxCaption = "8 × 8 matrix";

        /// <summary>A build that embedded no profile at all has no rows to draw, and says why rather than
        /// leaving the heading over nothing.</summary>
        public const string NoProfiles =
            "This build of openDash carries no light profiles. See plugin/OpenDash/Resources/README.md.";

        /// <summary>
        /// What a strip row says when SimHub's LED driver cannot be reached.
        /// </summary>
        /// <remarks>
        /// Not <see cref="FlagBoxInstallPlan.Summary"/>, which offers the file in the openDash folder: the
        /// only profile ever written there is the flag box (FlagBoxProfile.Extract), so that sentence over a
        /// strip row would send a driver looking for a file that was never written. A state rather than an
        /// error, because nothing the driver does about it is here.
        /// </remarks>
        public const string Unavailable =
            "SimHub's LED settings are not available, so openDash cannot install a strip profile.";

        /// <summary>The id suffixes a wiring adds, which `wheel(..., { reversed: true })` and `fanatec(...)`
        /// in packages/dash/src/leds/strip.ts spell. A suffix this does not know is not guessed at: the shape
        /// falls through to a row of its own.</summary>
        public const string ReversedSuffix = "reversed";

        public const string FanatecSuffix = "fanatec";

        internal static readonly IList<string> WiringSuffixes = new[] { ReversedSuffix, FanatecSuffix };

        public const string Wheel = "wheel";
        public const string Brow = "brow";
        internal const string BrowPrefix = Brow + "-";

        /// <summary>The middle dot the canvas joins facts with. PanelCopy has the same separator and keeps
        /// it private, and one of the two files would have to expose it for the other to share it.</summary>
        private const string Join = " · ";

        /// <summary>The ellipsis a range of lengths is drawn with: "0/8/0 … 0/16/0".</summary>
        private const string Ellipsis = " … ";

        /// <summary>
        /// The shapes the canvas gives a row and a device caption of their own, in the order it draws them.
        /// </summary>
        /// <remarks>
        /// The captions are the canvas's own wording, which abbreviates what strip.ts spells out; the panel
        /// cannot carry the full spelling, because the caption is drawn as tracked uppercase and the 3/9/3's
        /// four device families at their full length are wider than the row is. Every other shape the build
        /// emits is a generic run and joins a grouped row, which is the rule PanelLightRowsTests holds
        /// against strip.ts: a shape that gains a device family there and no caption here fails it.
        /// </remarks>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> NamedShapes =
            new[]
            {
                new KeyValuePair<string, string>("4-14-4", "strip" + Join + "SimRep MLD, Ascher"),
                new KeyValuePair<string, string>("4-14-4-reversed", "strip" + Join + "SimRep MLD, wired from the far end"),
                new KeyValuePair<string, string>("3-9-3-fanatec", "strip" + Join + "Fanatec wheels in SimHub"),
                new KeyValuePair<string, string>("3-10-3", "strip" + Join + "GridSim Lab GTSL Pro"),
            };

        /// <summary>The counts a caption writes as a word. Beyond the table it falls back to the digits,
        /// which is worse to read and better than being wrong.</summary>
        private static readonly string[] Words =
        {
            "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve",
        };

        /// <summary>A count in words: the "five" of "bare runs, five lengths". Derived rather than written,
        /// so that a sixth length added to strip.ts does not leave the caption lying.</summary>
        public static string Word(int count)
        {
            return count >= 0 && count < Words.Length ? Words[count] : count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The rows for one build's profiles, in the order the canvas draws them.
        /// </summary>
        /// <remarks>
        /// The named shapes with a row each, then the generic runs with sides, the bare runs and the brows
        /// as one row apiece. A shape the build did not embed has no row at all, which is the honest answer:
        /// a row for a profile that is not there could only offer a press that does nothing.
        /// </remarks>
        public static IList<LightRowPlan> Rows(IEnumerable<LightProfile> profiles)
        {
            var census = (profiles ?? Enumerable.Empty<LightProfile>())
                .Where(p => p != null && !string.IsNullOrEmpty(p.ShapeId))
                .ToList();

            var rows = new List<LightRowPlan>();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var named in NamedShapes)
            {
                var key = named.Key;
                var profile = census.FirstOrDefault(p => string.Equals(p.ShapeId, key, StringComparison.Ordinal));
                if (profile == null) continue;
                taken.Add(profile.ShapeId);
                rows.Add(new LightRowPlan(Prefixed(Label(profile)), named.Value, new[] { profile.ShapeId }));
            }

            var rest = census
                .Where(p => !taken.Contains(p.ShapeId))
                .Select(p => new KeyValuePair<LightProfile, LightShape>(p, LightShape.Parse(p.ShapeId)))
                .ToList();

            // One row per side length, each a range over that side's centres. The grid is sixty-odd
            // shapes and joining them into one row with middle dots produced a line nobody could read;
            // what a driver picks between is how many LEDs are at the ends, and the centres under it are
            // a range rather than a list.
            var grid = rest.Where(e => e.Value != null).ToList();
            foreach (var side in grid.Select(e => e.Value.Left).Distinct().OrderBy(n => n))
            {
                var group = Sorted(grid.Where(e => e.Value.Left == side));
                rows.Add(Range(group, SideNoun(side, 1), SideNoun(side, group.Count)));
            }

            // Anything whose id this cannot read: its own row, under whatever the build called it. It is
            // still a profile of SimHub's LED driver, which is what the caption says and all it can say.
            foreach (var entry in rest.Where(e => e.Value == null))
            {
                rows.Add(new LightRowPlan(Prefixed(Label(entry.Key)), "strip", new[] { entry.Key.ShapeId }));
            }
            return rows;
        }

        /// <summary>What a row of one side length is called: a side of none is a bare run, which is what a
        /// brow is, and anything else is named by how many LEDs sit at each end.</summary>
        public static string SideNoun(int side, int count)
        {
            if (side == 0) return count == 1 ? "bare run" : "bare runs and brows";
            var each = side == 1 ? "one LED at each end" : Word(side) + " LEDs at each end";
            return count == 1 ? "strip, " + each : "strips, " + each;
        }

        /// <summary>A group of lengths, named by its two ends: "openDash brow 9 … 25".</summary>
        private static LightRowPlan Range(IList<KeyValuePair<LightProfile, LightShape>> group, string one, string many)
        {
            var labels = group.Select(Label).ToList();
            var name = labels.Count == 1
                ? Prefixed(labels[0])
                : Prefixed(labels[0] + Ellipsis + WithoutSharedWords(labels[0], labels[labels.Count - 1]));
            var caption = group.Count == 1 ? one : many + ", " + Word(group.Count) + " lengths";
            return new LightRowPlan(name, caption, Ids(group));
        }

        /// <summary>
        /// The far end of a range with the words it already shares with the near end dropped.
        /// </summary>
        /// <remarks>
        /// One rule for both grouped ranges, so that they cannot drift apart: "brow 9" and "brow 25" share
        /// the word "brow" and read as "brow 9 … 25", while "0/8/0" and "0/16/0" share nothing and keep both
        /// ends whole. The last word is never dropped, or a range of one repeated length would lose its end
        /// altogether.
        /// </remarks>
        private static string WithoutSharedWords(string near, string far)
        {
            var a = near.Split(' ');
            var b = far.Split(' ');
            var shared = 0;
            while (shared < a.Length - 1 && shared < b.Length - 1 && string.Equals(a[shared], b[shared], StringComparison.Ordinal))
            {
                shared++;
            }
            return string.Join(" ", b, shared, b.Length - shared);
        }

        /// <summary>Ascending by geometry, which is the order the canvas reads a range in and the order the
        /// generator emits the lengths in.</summary>
        private static IList<KeyValuePair<LightProfile, LightShape>> Sorted(IEnumerable<KeyValuePair<LightProfile, LightShape>> group)
        {
            return group
                .OrderBy(e => e.Value.Left)
                .ThenBy(e => e.Value.Centre)
                .ThenBy(e => e.Value.Right)
                .ThenBy(e => e.Key.ShapeId, StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> Ids(IEnumerable<KeyValuePair<LightProfile, LightShape>> group)
        {
            return group.Select(e => e.Key.ShapeId).ToList();
        }

        private static string Label(KeyValuePair<LightProfile, LightShape> entry)
        {
            return Label(entry.Key);
        }

        /// <summary>
        /// What the build called this shape, without the prefix every profile of ours carries.
        /// </summary>
        /// <remarks>
        /// The profile's own Name, which rpmStripProfileName() wrote, rather than anything assembled here.
        /// The fallback below repeats that rule for a profile whose Name could not be read, and is the only
        /// place the panel spells a label itself.
        /// </remarks>
        public static string Label(LightProfile profile)
        {
            if (profile == null) return string.Empty;
            var name = profile.ProfileName;
            if (!string.IsNullOrEmpty(name) && name.StartsWith(FlagBoxProfile.FilePrefix, StringComparison.Ordinal))
            {
                return name.Substring(FlagBoxProfile.FilePrefix.Length);
            }
            var shape = LightShape.Parse(profile.ShapeId);
            if (shape == null) return profile.ShapeId;
            if (shape.Placement == Brow) return Brow + " " + Digits(shape.Centre);
            // rpmStripProfileName() writes the reversed suffix in lower case and the Fanatec one as the
            // maker's own name, so the fallback cannot simply append the suffix it read.
            var wiring = shape.Wiring == FanatecSuffix ? " Fanatec" : shape.Wiring == ReversedSuffix ? " reversed" : string.Empty;
            return Digits(shape.Left) + "/" + Digits(shape.Centre) + "/" + Digits(shape.Right) + wiring;
        }

        /// <summary>A shape id as the panel writes it -- "3/9/3", "brow 15" -- without a profile to read
        /// the name off. What the bar list needs: it names a shape before any profile for it exists.</summary>
        public static string ShapeLabel(string shapeId)
        {
            return Label(new LightProfile(shapeId, null));
        }

        private static string Prefixed(string label)
        {
            return FlagBoxProfile.FilePrefix + label;
        }

        private static string Digits(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The colour of a row's status dot.
        /// </summary>
        /// <remarks>
        /// Read off <see cref="PanelCopy.LightRow"/> rather than from a second table, so the dot cannot
        /// disagree with the words beside it. The one pairing a table carrying a single colour per state
        /// cannot say is the uninstalled one: the canvas draws that dot in status.notInstalled and its label
        /// in text.label. SettingsControl.Install.Packages.cs resolves the same pair the same way.
        /// </remarks>
        public static string DotHex(FlagBoxInstallState state)
        {
            var ink = PanelCopy.LightRow(state, null).StateHex;
            return string.Equals(ink, Theme.TextLabel, StringComparison.Ordinal) ? Theme.StatusNotInstalled : ink;
        }

        /// <summary>
        /// The sentence a strip row carries as its tooltip: what is true now, for as many profiles as the
        /// row holds.
        /// </summary>
        /// <remarks>
        /// A grouped row reads as its worst member (FlagBoxInstallPlan.Combine), so the plural wording says
        /// "at least one of these" rather than claiming anything about the rest. The flag box keeps
        /// FlagBoxInstallPlan.Summary, which is written about the one profile openDash also writes to disk.
        /// </remarks>
        public static string Tooltip(int members, FlagBoxInstallState state, string installedVersion)
        {
            var one = members <= 1;
            switch (state)
            {
                case FlagBoxInstallState.NotEmbedded:
                    return "No such profile is embedded in this build.";
                case FlagBoxInstallState.Unavailable:
                    return Unavailable;
                case FlagBoxInstallState.NotInstalled:
                    return (one ? "Not installed." : "At least one of these " + Word(members) + " is not installed.")
                        + " Press the button to add "
                        + (one ? "it" : "them all")
                        + " to SimHub's LED profiles; then pick "
                        + (one ? "it" : "the one for your strip")
                        + " on your device.";
                case FlagBoxInstallState.UpToDate:
                    // Installing adds a profile; it does not switch to one. Same half-told job the flag
                    // box's own summary names, and the same warning before a press that replaces a copy
                    // the user may have edited in SimHub.
                    return (one ? "Installed" : "All " + Word(members) + " are installed")
                        + (installedVersion == null ? ". " : " (" + installedVersion + "). ")
                        + "Select " + (one ? "it" : "the one for your strip") + " on your device to use it. "
                        + FlagBoxInstallPlan.Replaces;
                case FlagBoxInstallState.Outdated:
                    return (one ? "A newer profile is available." : "A newer profile is available for at least one of these " + Word(members) + ".")
                        + " " + FlagBoxInstallPlan.Replaces;
                default:
                    return "Installing failed. SimHub's log says why.";
            }
        }
    }
}
