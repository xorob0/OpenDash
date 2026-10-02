// PanelLeds.cs: the LEDs page's decisions and words -- what a strip's card says, what its preview draws, which
// effect switches its shape carries, the Add LEDs sheet's steps, the lines said after a press, and what search
// finds on the page.
//
// The page is Leds.dc.html and its sheet AddLeds.dc.html: a card per strip, the selected strip's header, its
// preview, its rev lights, what is its own (device, brightness, direction) and a switch for every effect its
// shape carries (#370), then what is the same for every strip. SettingsControl.Lights.cs draws it and decides
// nothing; every string and every rule it reads is here, where PanelLedsTests can hold it. Pure: no WPF and no
// SimHub type.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One effect switch of the Effects grid: the setting it is read through and what it is called.</summary>
    public sealed class LedEffectSwitch
    {
        public LedEffectSwitch(string setting, string label, string role, params string[] keywords)
        {
            Setting = setting;
            Label = label;
            Role = role;
            Keywords = keywords ?? new string[0];
        }

        /// <summary>The words the rest of the panel uses for what the switch silences, which search finds it by.</summary>
        public IReadOnlyList<string> Keywords { get; private set; }

        /// <summary>The setting the switch is (Contract.LedEffectSettings), which the rig stores per strip.</summary>
        public string Setting { get; private set; }

        /// <summary>What the grid calls it.</summary>
        public string Label { get; private set; }

        /// <summary>The generator's role for it (packages/dash/src/leds/effects.ts), which decides which shapes
        /// carry it: side, race, car, aid or strip.</summary>
        public string Role { get; private set; }

        /// <summary>The effect id the switch is stored and read under (Contract.LedEffectPrimaryId).</summary>
        public string Id
        {
            get { return Contract.LedEffectPrimaryId(Setting); }
        }
    }

    /// <summary>One of SimHub's LED devices as the SimHub device row reads it: LedTarget's three facts, without
    /// the SimHub type behind them.</summary>
    public sealed class LedDeviceEntry
    {
        public LedDeviceEntry(string id, string name, bool connected)
        {
            Id = id;
            Name = name;
            Connected = connected;
        }

        public string Id { get; private set; }

        public string Name { get; private set; }

        public bool Connected { get; private set; }
    }

    /// <summary>What the SimHub device row draws: a caption, and a picker's entries where it has one.</summary>
    public sealed class LedDeviceRow
    {
        public LedDeviceRow(string caption, IList<string> ids, IList<string> labels, int selected)
        {
            Caption = caption;
            Ids = ids;
            Labels = labels;
            Selected = selected;
        }

        /// <summary>The line under the row's title, or null.</summary>
        public string Caption { get; private set; }

        /// <summary>The device each entry picks, or null where the row has no picker.</summary>
        public IList<string> Ids { get; private set; }

        /// <summary>What each entry says, in the order of <see cref="Ids"/>.</summary>
        public IList<string> Labels { get; private set; }

        /// <summary>The entry the picker opens on, the strip's own device.</summary>
        public int Selected { get; private set; }

        public bool HasPicker
        {
            get { return Ids != null; }
        }
    }

    public static class PanelLeds
    {
        public const string Title = "LEDs";

        /// <summary>The empty state, here and under Home's LEDs eyebrow: the LEDs page's own, as NoScreens is
        /// Screens'. It was PanelLights.NoBars, shared, which Home's read froze.</summary>
        public const string NoStrips = "No strips yet.";

        public const string AnchorStrips = "leds.strips";
        public const string AnchorPreview = "leds.preview";
        public const string AnchorRevLights = "leds.rev-lights";
        public const string AnchorRevStyle = "leds.rev-style";
        public const string AnchorMirrorFit = "leds.mirror-fit";
        public const string AnchorCentre = "leds.centre";
        public const string AnchorThisStrip = "leds.this-strip";
        public const string AnchorDevice = "leds.device";
        public const string AnchorBrightness = "leds.brightness";
        public const string AnchorReverse = "leds.reverse";
        public const string AnchorEffects = "leds.effects";
        public const string AnchorFlagAnimation = "leds.flag-animation";
        public const string AnchorSpotter = "leds.spotter";
        public const string AnchorEveryStrip = "leds.every-strip";
        public const string AnchorCarTables = "leds.car-tables";

        /// <summary>
        /// Where Home's issue and the sidebar's dot send the driver for a strip profile's update: the LEDs page's
        /// strips, since the strip's header carries the Update press (#523). It was Updates' lights section
        /// while only the Updates table had one.
        /// </summary>
        public static readonly PanelRoute StripUpdateRoute = new PanelRoute(PanelPage.Leds, AnchorStrips);

        // --- Which strip, and which moment ------------------------------------------------------------------

        /// <summary>Which of the rig's strips the page shows: the one selected, else the first; -1 with none.
        /// A selection that is gone (the strip was removed elsewhere) falls back to the first.</summary>
        public static int ShownStrip(IList<string> namespaces, string selected)
        {
            if (namespaces == null || namespaces.Count == 0) return -1;
            for (var i = 0; i < namespaces.Count; i++)
            {
                if (string.Equals(namespaces[i], selected, StringComparison.Ordinal)) return i;
            }
            return 0;
        }

        /// <summary>The preview chip a strip opens on: the one pressed, while it was pressed for this strip, and
        /// Live for any other, as the artboard has it.</summary>
        public static string ScenarioFor(string pressed, string pressedFor, string shown)
        {
            return string.Equals(pressedFor, shown, StringComparison.Ordinal) && pressed != null ? pressed : LiveScenario;
        }

        // --- The cards ------------------------------------------------------------------------------------

        /// <summary>The narrowest a strip's card is laid at, which fits a 3/9/3 at the card's 9 px LEDs; the grid
        /// takes three columns where the artboard does, and fewer below. A longer strip's picture sits in a
        /// Viewbox that only shrinks, so it fits its card rather than running off it: a 25-LED run at 9 px is
        /// 273 wide and a card's inside can be 180.</summary>
        public const double CardMinWidth = 210;

        /// <summary>The artboard's grid gap between cards.</summary>
        public const double CardGap = 12;

        /// <summary>At most three to a row, as the artboard lays them.</summary>
        public const int CardColumns = 3;

        /// <summary>
        /// A shape as the cards and the header write it: the three counts with a spaced dot between them, "3 · 9 ·
        /// 3", or the one count of a bare run, "15". An id this cannot read is written as the Updates page
        /// writes it.
        /// </summary>
        public static string ShapeDots(string shapeId)
        {
            var shape = LightShape.Parse(shapeId);
            if (shape == null) return PanelLightRows.ShapeLabel(shapeId);
            if (shape.Bare) return Digits(shape.Centre);
            return Digits(shape.Left) + Dot + Digits(shape.Centre) + Dot + Digits(shape.Right);
        }

        /// <summary>The hardware a strip's shape was added as: the Fanatec wheel, or any other strip.</summary>
        public static string Hardware(string shapeId)
        {
            var shape = LightShape.Parse(shapeId);
            return shape != null && shape.Wiring == PanelLightRows.FanatecSuffix ? PanelLights.BarFanatecTitle : SomethingElseStrip;
        }

        /// <summary>What the header's chip says before the shape's numerals: "Fanatec wheel · ".</summary>
        public static string HardwareLead(string shapeId)
        {
            return Hardware(shapeId) + Dot;
        }

        /// <summary>The ends on each side and the centre of a shape, for the pictures: 0 and the run's length
        /// for a bare run, and nothing at all for an id this cannot read.</summary>
        public static int Ends(string shapeId)
        {
            var shape = LightShape.Parse(shapeId);
            return shape == null ? 0 : shape.Left;
        }

        public static int Centre(string shapeId)
        {
            var shape = LightShape.Parse(shapeId);
            return shape == null ? 0 : shape.Centre;
        }

        /// <summary>The word a strip that is not a Fanatec wheel is, in the header's chip.</summary>
        public const string SomethingElseStrip = "Strip";

        // The card's state, in the words Home and the fix box use.
        public const string Showing = "Showing";
        public const string NotSelected = "Not selected in SimHub";
        public const string NotInstalled = PanelCopy.NotInstalled;
        public const string UpdateAvailable = "Update available";
        public const string Installed = PanelCopy.Installed;

        /// <summary>
        /// What a strip's card says about its profile in SimHub, from what the shell last read (StripFacts):
        /// null when that could not be read, since a guess would put a warning on a strip that is fine.
        /// </summary>
        /// <remarks>
        /// In the order a driver acts on them: a profile that is not there comes first, then one that is there
        /// and not selected (nothing shows until it is), then one with an update. "Installed" is the one state
        /// the artboard does not draw: the profile is up to date and the device's selection could not be read.
        /// </remarks>
        public static string StateText(FlagBoxInstallState? profile, bool? selected)
        {
            if (profile == null) return null;
            switch (profile.Value)
            {
                case FlagBoxInstallState.UpToDate:
                case FlagBoxInstallState.Outdated:
                    if (selected == false) return NotSelected;
                    if (profile.Value == FlagBoxInstallState.Outdated) return UpdateAvailable;
                    return selected == true ? Showing : Installed;
                case FlagBoxInstallState.Unavailable:
                    return null;
                default:
                    return NotInstalled;
            }
        }

        /// <summary>The ink of that state: green while it shows, amber where there is something to do, and
        /// the secondary ink where the driver has to press Install.</summary>
        public static string StateHex(FlagBoxInstallState? profile, bool? selected)
        {
            var text = StateText(profile, selected);
            if (text == Showing) return Theme.StatusUpToDate;
            if (text == NotSelected) return Theme.Caution;
            if (text == UpdateAvailable) return Theme.StatusUpdateAvailable;
            return Theme.TextSecondary;
        }

        public const string InstallProfile = "Install";
        public const string UpdateProfile = "Update";

        /// <summary>The header's press for the strip's profile: Install where SimHub has none (or the last
        /// install failed), Update where it has an older one, and nothing where it is current, where SimHub
        /// could not be read, or where this build carries no profile to install.</summary>
        public static string ProfileAction(FlagBoxInstallState? profile)
        {
            if (profile == FlagBoxInstallState.NotInstalled || profile == FlagBoxInstallState.Failed) return InstallProfile;
            if (profile == FlagBoxInstallState.Outdated) return UpdateProfile;
            return null;
        }

        /// <summary>
        /// The same, where the press could only fail: no press at all when this build carries no profile for
        /// the strip, or SimHub does not list the device it names, whatever the census says.
        /// </summary>
        /// <remarks>
        /// An install takes the strip's profile out of every device before it writes the new one, so pressing it
        /// for a device SimHub no longer lists would remove a profile that still lights and install nothing,
        /// which is why Updates' UpdateBars leaves such a strip alone. And a strip whose profile the build does
        /// not carry (a pre-grid brow, say) is compared against nothing, which the census reads as an update.
        /// </remarks>
        public static string ProfileAction(FlagBoxInstallState? profile, bool embedded, bool deviceListed)
        {
            return ProfileBlocked(embedded, deviceListed) == null ? ProfileAction(profile) : null;
        }

        /// <summary>
        /// Why nothing on this page can install the strip's profile, said under the header in place of a press
        /// and after a Reverse or a Rename that could not install it again; null where something can.
        /// </summary>
        /// <remarks>
        /// A device SimHub does not list is two states. Where SimHub offers another, the step is the device
        /// row's picker, in its value's words. Where it offers none, the device row has no picker to point at,
        /// so the line is the row's own caption (<see cref="PanelLights.DeviceRowCaption"/>): the first step is
        /// in SimHub, and a device passed over is named with the log that says why.
        /// </remarks>
        public static string ProfileBlocked(bool embedded, bool deviceListed, int offered = 1, IList<string> declined = null)
        {
            if (!embedded) return NoProfileForStrip;
            if (!deviceListed) return offered > 0 ? DeviceNotListed : PanelLights.DeviceRowCaption(0, null, declined);
            return null;
        }

        /// <summary>The build carries no profile of the strip's shape: nothing here can install one.</summary>
        public const string NoProfileForStrip = "This build ships no profile for this strip.";

        /// <summary>
        /// The same, as the line under the header says it: the one state it words apart is no SimHub device at
        /// all, where the SimHub device row's caption says what SimHub lacks and the step in SimHub, and the
        /// header says only what that means here and points at the row, rather than saying its fact a second time.
        /// </summary>
        public static string HeaderBlocked(bool embedded, bool deviceListed, int offered, IList<string> declined)
        {
            if (embedded && !deviceListed && offered <= 0) return AwaitsDevice;
            return ProfileBlocked(embedded, deviceListed, offered, declined);
        }

        /// <summary>The header's line with no SimHub device offered: why it offers no Install, and the row that
        /// says what SimHub lacks, by name. Nothing installs the profile on its own once a device appears, so the
        /// line does not say it waits for one.</summary>
        public const string AwaitsDevice = "Nothing here can install this strip's profile. See SimHub device.";

        /// <summary>
        /// Whether the SimHub device picker can be used: not where this build carries no profile of the strip's
        /// shape, since a move installs that profile on the device picked and there is none to install. The header
        /// already says why (<see cref="NoProfileForStrip"/>), so the picker is drawn disabled rather than refusing
        /// every pick with a line after it.
        /// </summary>
        public static bool DeviceMovable(bool embedded)
        {
            return embedded;
        }

        /// <summary>SimHub does not list the device the strip names and offers another: the one step is the device
        /// row's, whose picker names this state "Device not in SimHub".</summary>
        public const string DeviceNotListed = "This strip's device is not in SimHub. Choose one under SimHub device.";

        public const string InstallTooltip = "Installs this strip's profile in SimHub.";
        /// <summary>The Update press over an older profile says what it costs, in the words the Matrix page's
        /// flag box Update and the Updates table's row press say it (#524, ruling 12).</summary>
        public const string UpdateTooltip = FlagBoxInstallPlan.Replaces;

        public const string RenameButton = "Rename";
        public const string RemoveButton = "Remove";
        /// <summary>What Remove takes, in the words Screens' Remove uses for a screen: the strip is OpenDash's,
        /// and only its profile is SimHub's.</summary>
        public const string RemoveTooltip = "Removes this strip, its settings and its profile.";
        public const string RemoveConfirm = "Remove it";
        public const string RemoveBody = "Removes the strip, its settings and its profile in SimHub.";

        /// <summary>The Remove sheet's title: the press, and the strip it removes.</summary>
        public static string RemoveTitle(string name)
        {
            return RemoveButton + " " + name;
        }

        /// <summary>The fix box's title for a profile SimHub holds and has not selected, in the card's words.</summary>
        public const string NotSelectedTitle = "Installed, but not selected in SimHub";

        // --- The preview ----------------------------------------------------------------------------------

        /// <summary>The chip that shows the car's own rev lights as they are now.</summary>
        public const string LiveScenario = "live";

        /// <summary>What a screen reader calls the preview's chips: the Rig page's one noun for the scenario
        /// chips' section, as Matrix and Settings name theirs (#524, ruling 6).</summary>
        public const string PreviewChipsName = PanelRigMap.ScenariosName;

        /// <summary>
        /// The preview's chips, in the artboard's order: what the strip shows now, and five moments it can be
        /// asked to show, each drawn from PanelEmulation's rules and never sent to the hardware (#506).
        /// </summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Scenarios = new[]
        {
            new KeyValuePair<string, string>(LiveScenario, "Live"),
            new KeyValuePair<string, string>(PanelEmulation.Shift, "Shift point"),
            new KeyValuePair<string, string>(PanelEmulation.Yellow, "Yellow"),
            new KeyValuePair<string, string>(PanelEmulation.Blue, "Blue"),
            new KeyValuePair<string, string>(PanelEmulation.CarLeft, "Car left"),
            new KeyValuePair<string, string>(PanelEmulation.Limiter, "Pit limiter"),
        };

        /// <summary>The link beside the chips, to the Rig page's picture of every device.</summary>
        public const string AllDevicesAtOnce = "All devices at once";

        /// <summary>The artboard's gap between the preview's groups.</summary>
        public const double PreviewGroupGap = 22;

        /// <summary>
        /// The preview's columns at its natural size, left to right: each group's width at the preview's LEDs
        /// and the gap between groups. The labels are placed under these, outside the Viewbox that shrinks the
        /// LEDs, so they line up under their groups at any scale and stay at their own size.
        /// </summary>
        public static double[] PreviewColumns(int ends, int centre)
        {
            var style = StripStyle.Preview;
            Func<int, double> group = n => n <= 0 ? 0 : 2 * style.PadX + n * style.Led + (n - 1) * style.Gap;
            if (ends <= 0) return new[] { group(centre) };
            return new[] { group(ends), PreviewGroupGap, group(centre), PreviewGroupGap, group(ends) };
        }

        /// <summary>
        /// Where each label under the preview starts, given the room the preview has and each label's own
        /// width: centred under its group as the Viewbox draws it (shrunk to the room, never grown, and centred
        /// in it), and kept inside the room.
        /// </summary>
        /// <remarks>
        /// Each label is given its whole width rather than a cell as wide as its group: a 1-LED end is 30 px wide
        /// at the preview's size and "Right · 1" is about 50, and a Grid column clips what it cannot hold. An end
        /// label wider than its group reaches into the gap beside it, and one at the edge of the room is moved in
        /// rather than cut. PanelLedsTests holds every shape the build embeds to labels that stay in the room and
        /// never meet, at full size and at the narrow column.
        /// </remarks>
        public static double[] PreviewLabelLefts(double[] columns, double[] labelWidths, double room)
        {
            var natural = columns.Sum();
            var scale = natural <= 0 || room >= natural ? 1 : room / natural;
            var offset = Math.Max(0, (room - natural * scale) / 2);
            var lefts = new double[labelWidths.Length];
            var x = 0.0;
            for (int c = 0, g = 0; c < columns.Length; c++)
            {
                if (c % 2 == 0 && g < lefts.Length)
                {
                    var width = labelWidths[g];
                    var middle = offset + (x + columns[c] / 2) * scale;
                    lefts[g] = Math.Max(0, Math.Min(middle - width / 2, room - width));
                    g++;
                }
                x += columns[c];
            }
            return lefts;
        }

        /// <summary>Whether a tick redraws the preview: only while Live is pressed, since every other chip is a
        /// fixed moment. The redraw itself happens only when the frame changed.</summary>
        public static bool TickRedraws(string scenario)
        {
            return scenario == LiveScenario;
        }

        /// <summary>
        /// Whether Live draws the car's own run: the car's lights are loaded, the strip uses them (#369), and its
        /// centre shows the revs. With the centre on the brake, the pedals or the fuel, the profile draws the
        /// car's run nowhere, so the preview must not either.
        /// </summary>
        public static bool LiveRuns(bool ready, string rpmStyle, string centre)
        {
            return ready && UsesCarRevLights(rpmStyle) && Contract.NormaliseLedCentre(centre) == Contract.DefaultLedCentre;
        }

        /// <summary>What a frame looks like, as one string: the preview redraws only when this changes.</summary>
        public static string FrameKey(IList<string[]> frame)
        {
            if (frame == null) return string.Empty;
            return string.Join("|", frame.Select(group => group == null ? string.Empty : string.Join(",", group.Select(c => c ?? "-"))));
        }

        /// <summary>The words under each group of the preview: "Left · 3", "Centre · 9", "Right · 3", or "15
        /// LEDs" under a bare run.</summary>
        public static string[] PreviewLabels(int ends, int centre)
        {
            if (ends <= 0) return new[] { Digits(centre) + (centre == 1 ? " LED" : " LEDs") };
            return new[] { "Left" + Dot + Digits(ends), "Centre" + Dot + Digits(centre), "Right" + Dot + Digits(ends) };
        }

        /// <summary>What a strip is set to that changes its picture: the full-strip spotter and the effects
        /// it has switched off.</summary>
        public static StripOptions OptionsFor(bool spotterWhole, IEnumerable<string> effectsOff)
        {
            return new StripOptions
            {
                SpotterWhole = spotterWhole,
                EffectsOff = new HashSet<string>((effectsOff ?? Enumerable.Empty<string>()).Where(id => id != null), StringComparer.Ordinal),
            };
        }

        /// <summary>
        /// The frame the preview draws for a chip.
        /// </summary>
        /// <remarks>
        /// Live is the car's own rev lights as the plugin has them now (<paramref name="packed"/>, from
        /// CarLights.Run), read through PanelEmulation.LiveFrame, and only when that run is what the strip
        /// draws: the car's lights are loaded and the strip uses them. Otherwise it is the strip at rest, since a
        /// picture of lights that are not lit is worse than none. Every other chip is PanelEmulation's rules.
        /// </remarks>
        public static string[][] PreviewFrame(string scenario, int ends, int centre, StripOptions options, bool live, string packed, bool centreShowsRevs = true)
        {
            if (scenario == LiveScenario || PanelEmulation.Find(scenario) == null)
            {
                return live ? PanelEmulation.LiveFrame(packed, ends, centre) : PanelEmulation.StripFrame(ends, centre, PanelEmulation.Idle, options);
            }
            return SpotterOnBareRun(CentreAtRest(PanelEmulation.StripFrame(ends, centre, scenario, options), scenario, centreShowsRevs), ends, scenario, options);
        }

        /// <summary>
        /// A car alongside on a strip with no ends: the whole run in the spotter's amber, for a side whose switch is
        /// on, as the profile draws it (rpmStrip.ts takes the side role over the whole run where a shape has no
        /// lamps). PanelEmulation.StripFrame lights a car alongside only on an end, so on a bare run it would draw
        /// the moment as no car at all, and Spotter left and right would change nothing in the preview.
        /// </summary>
        private static string[][] SpotterOnBareRun(string[][] frame, int ends, string scenario, StripOptions options)
        {
            if (ends > 0 || frame == null || frame.Length != 1 || frame[0] == null) return frame;
            options = options ?? StripOptions.Default;
            var left = (scenario == PanelEmulation.CarLeft || scenario == PanelEmulation.CarBoth) && options.Draws(SpotterLeft);
            var right = (scenario == PanelEmulation.CarRight || scenario == PanelEmulation.CarBoth) && options.Draws(SpotterRight);
            if (!left && !right) return frame;
            for (var i = 0; i < frame[0].Length; i++) frame[0][i] = Theme.Caution;
            return frame;
        }

        /// <summary>The spotter's two effect ids, as Contract.LedEffects and the generator name them.</summary>
        private const string SpotterLeft = "spotter.left";

        private const string SpotterRight = "spotter.right";

        /// <summary>The frame a strip's card draws: the revs half way, as the Rig page opens on, in a centre that
        /// shows them, while the strip is showing in SimHub (<see cref="CardLit"/>); every LED dark otherwise.</summary>
        public static string[][] CardFrame(string shapeId, StripOptions options, bool centreShowsRevs = true, bool lit = true)
        {
            var ends = Ends(shapeId);
            var centre = Centre(shapeId);
            if (!lit) return ends > 0 ? new[] { new string[ends], new string[centre], new string[ends] } : new[] { new string[centre] };
            return CentreAtRest(PanelEmulation.StripFrame(ends, centre, PanelEmulation.Mid, options), PanelEmulation.Mid, centreShowsRevs);
        }

        /// <summary>
        /// Whether a card draws its strip lit: only while SimHub shows the profile, which is while SimHub holds it
        /// and has it selected. A card that says "Not selected in SimHub" or "Not installed" beside lit LEDs
        /// contradicts its own state line, which is the fake output ruling 20 keeps off Home's pictures; the
        /// artboard draws the unselected brow dark.
        /// </summary>
        /// <remarks>
        /// Not <see cref="StateText"/> == Showing: an older copy SimHub has selected reads "Update available",
        /// which outranks Showing, and its LEDs on the rig are running all the same. After any release that
        /// changes the strip profiles, every strip in use would otherwise go dark on its card.
        /// </remarks>
        public static bool CardLit(FlagBoxInstallState? profile, bool? selected)
        {
            return HeldInSimHub(profile) && selected == true;
        }

        /// <summary>Whether a strip's centre shows the revs (Centre display on RPM), which is the only centre the
        /// profile draws the rev ladder in.</summary>
        public static bool CentreShowsRevs(string centre)
        {
            return Contract.NormaliseLedCentre(centre) == Contract.DefaultLedCentre;
        }

        /// <summary>A moment that is the revs alone: the shift point, half way, and idle.</summary>
        public static bool IsRevMoment(string scenario)
        {
            return scenario == PanelEmulation.Shift || scenario == PanelEmulation.Mid || scenario == PanelEmulation.Idle;
        }

        /// <summary>
        /// A rev moment on a strip whose centre shows the brake, the pedals or the fuel: the centre at rest,
        /// since the profile never draws the revs there. Its brake or fuel is not the moment's to draw.
        /// </summary>
        /// <remarks>
        /// Only the rev moments: under a flag, a car alongside or the limiter, PanelEmulation.StripFrame draws
        /// the revs under the effect in every centre, which is the emulation's to change (it is frozen here).
        /// </remarks>
        private static string[][] CentreAtRest(string[][] frame, string scenario, bool centreShowsRevs)
        {
            if (centreShowsRevs || !IsRevMoment(scenario) || frame == null || frame.Length == 0) return frame;
            var middle = frame.Length == 3 ? 1 : 0;
            frame[middle] = new string[frame[middle] == null ? 0 : frame[middle].Length];
            return frame;
        }

        /// <summary>
        /// How bright the strip's pictures are drawn: full by day, as every light picture on the panel is, and at
        /// night the brightness in force, which is the lower of the strip's own and the night brightness.
        /// </summary>
        public static double PreviewDim(bool night, int nightBrightness, int? own)
        {
            if (!night) return 1;
            var value = own.HasValue ? Math.Min(own.Value, nightBrightness) : nightBrightness;
            return PanelEmulation.Dim(true, value);
        }

        // --- Rev lights -------------------------------------------------------------------------------------

        public const string RevLightsTitle = "Rev lights";

        /// <summary>The #369 switch: the car's own rev lights, or the plain ladder. A switch names the thing
        /// (voice.md), so the artboard's instruction "Use the car's own rev lights" is the departure
        /// docs/design/plugin.md records; search still finds the row by it.</summary>
        public const string CarRevLightsTitle = "Car's own rev lights";

        /// <summary>Whether a strip's stored style reads as the switch on. A retired style (meetInMiddle, f1)
        /// normalises to left to right, and reads as off.</summary>
        public static bool UsesCarRevLights(string rpmStyle)
        {
            return Contract.NormaliseLedRpmStyle(rpmStyle) == Contract.LedRpmStyleCar;
        }


        /// <summary>Whether the width row shows: it sizes the car's own lights, so only while the switch is on.</summary>
        public static bool ShowsMirrorFit(string rpmStyle)
        {
            return UsesCarRevLights(rpmStyle);
        }

        /// <summary>
        /// The line under the switch: whether the car in the sim is one Lovely Car Data has measured. Only while
        /// the switch is on and a car is loaded in a game the tables cover; null otherwise, since then there is
        /// nothing true to say.
        /// </summary>
        /// <remarks>
        /// With no tables on disk, which is every fresh install, no car can be found in them, and saying the
        /// car "is not in Lovely Car Data" would be false for a car it measures. The line says what is missing
        /// instead, and where the row that fetches it is. The plugin reads only iRacing's tables
        /// (CarLightLibrary), so in any other game every car would read as missing, which the greyed row for
        /// the other sims' car data already says is not read yet: there the line says nothing.
        ///
        /// <para>With no tables read, it says they are missing only where none were ever downloaded
        /// (<paramref name="tablesMissing"/>): while the start's read is still running, or where the copy on disk
        /// could not be read, the row below says "Loading…" or that it could not be read, and a line saying they were never
        /// downloaded would contradict it, and be false.</para>
        /// </remarks>
        public static string CarLine(bool on, string car, bool hasTable, bool tablesLoaded, bool tablesMissing, bool tablesCoverGame)
        {
            if (!on || string.IsNullOrWhiteSpace(car) || !tablesCoverGame) return null;
            if (!tablesLoaded) return tablesMissing ? CarTablesMissing : null;
            return car.Trim() + (hasTable ? " is in Lovely Car Data." : " is not in Lovely Car Data.");
        }

        /// <summary>The car line while a car is loaded and no tables were ever downloaded.</summary>
        public const string CarTablesMissing = "Lovely Car Data is not downloaded yet. Download it under Every strip.";

        /// <summary>Whether no tables were ever downloaded: none read, and the service says so, rather than that
        /// it is still reading them or could not read the copy on disk.</summary>
        public static bool TablesMissing(int carCount, string status)
        {
            return carCount <= 0 && (status ?? string.Empty).Trim().StartsWith(PanelLights.CarTablesNone, StringComparison.Ordinal);
        }

        /// <summary>The game whose tables the plugin reads, as SimHub names it or codes it ("iRacing", "IRacing").</summary>
        public const string TablesGame = "iRacing";

        /// <summary>Whether the tables cover the game SimHub is set to: iRacing's alone.</summary>
        public static bool TablesCoverGame(string game)
        {
            return game != null && string.Equals(game.Trim(), TablesGame, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether the tables are on disk: any car in them.</summary>
        public static bool TablesLoaded(int carCount)
        {
            return carCount > 0;
        }

        /// <summary>Whether the car line is the good news, drawn in green with the ringed check, rather than
        /// something to act on, drawn in amber with the warning.</summary>
        public static bool CarLineGood(bool hasTable, bool tablesLoaded)
        {
            return tablesLoaded && hasTable;
        }

        /// <summary>The car line's ink.</summary>
        public static string CarLineHex(bool good)
        {
            return good ? Theme.StatusUpToDate : Theme.Caution;
        }

        /// <summary>The width row, in the noun the switch uses: "Rev light width". The artboard's bare "Width"
        /// says nothing in a search result.</summary>
        public const string MirrorFitTitle = "Rev light width";

        /// <summary>The width is the rig's (LedMirrorFit), so the row says so.</summary>
        public const string MirrorFitCaption = "Every strip.";

        /// <summary>The widest a line under a row runs, as the kit caps a row's own caption: prose that runs the
        /// width of a desk is a line nobody finishes.</summary>
        public const double CaptionMaxWidth = 520;

        /// <summary>The widest the SimHub device picker is drawn, so a long device name trims inside it rather
        /// than crushing the row's title; the full name is its tooltip.</summary>
        public const double DevicePickerMaxWidth = 260;

        /// <summary>The narrowest the picker is bounded to, its own minimum width.</summary>
        public const double DevicePickerMinWidth = 160;

        /// <summary>The gap between the rev lights and This strip where the two sit side by side.</summary>
        public const double ColumnGap = 40;

        /// <summary>
        /// The widest the picker is drawn in a row <paramref name="rowWidth"/> wide: at most half of it, beside
        /// the row's gap, so the title and the caption keep the other half, and never wider than
        /// <see cref="DevicePickerMaxWidth"/>.
        /// </summary>
        /// <remarks>
        /// The row sits in one of two columns where they fit, and those start at 360 wide: a fixed 260 there left
        /// "SimHub device" 80 px and broke it onto two lines. The page sets it from the row's own width as the row
        /// is laid out, so a resize never has to build the page again for it: the page walks SimHub's devices.
        /// </remarks>
        public static double DevicePickerWidth(double rowWidth)
        {
            return Math.Min(DevicePickerMaxWidth, Math.Max(DevicePickerMinWidth, (rowWidth - PanelKit.RowGapLeds) / 2));
        }

        /// <summary>
        /// The SimHub device row: which of SimHub's LED devices a strip's profile goes to.
        /// </summary>
        /// <remarks>
        /// Three shapes rather than always a picker. A rig with no LED device is told why there is nowhere for the
        /// profile to go; a rig with one that the strip is on is told where it went; any other is asked. A strip
        /// pointed at a device SimHub does not list keeps its own entry, first and selected, labelled as gone, even
        /// beside a single device: dropping it would silently re-point the strip at whatever sorted first, which is
        /// the class of bug the picker exists to close. A device SimHub is not talking to says so beside its name.
        /// </remarks>
        public static LedDeviceRow DeviceRow(IList<LedDeviceEntry> devices, string current, IList<string> declined)
        {
            var listed = (devices ?? new LedDeviceEntry[0]).Where(device => device != null).ToList();
            if (listed.Count == 0) return new LedDeviceRow(PanelLights.DeviceRowCaption(0, null, declined), null, null, -1);
            var ids = listed.Select(device => device.Id).ToList();
            var labels = listed.Select(device => device.Connected ? device.Name : device.Name + PanelLights.DeviceOffline).ToList();
            var connected = listed.Select(device => device.Connected).ToList();
            var known = ids.Contains(current, StringComparer.Ordinal);
            if (!known)
            {
                ids.Insert(0, current);
                labels.Insert(0, PanelLights.DeviceGone);
            }
            if (listed.Count == 1 && known)
            {
                return new LedDeviceRow(PanelLights.OneDeviceCaption(listed[0].Name, connected[0], declined), null, null, -1);
            }
            var index = ids.FindIndex(id => string.Equals(id, current, StringComparison.Ordinal));
            return new LedDeviceRow(PanelLights.DeviceRowCaption(listed.Count, PanelLights.BarDeviceCaption, declined), ids, labels, index);
        }

        public const string CentreDisplayTitle = "Centre display";

        /// <summary>Where the stored centre sits in Contract.LedCentres, the first when it is none of them.</summary>
        public static int CentreIndex(string centre)
        {
            var i = Array.IndexOf(Contract.LedCentres, centre);
            return i < 0 ? 0 : i;
        }

        // --- This strip ------------------------------------------------------------------------------------

        public const string ThisStripTitle = "This strip";

        public const string BrightnessTitle = "Brightness";

        /// <summary>The brightnesses a strip can be set to of its own, in tens.</summary>
        public static readonly int[] BrightnessSteps = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        /// <summary>The rig's brightness in force, which a strip that follows the rig runs at: the night
        /// brightness while night mode is on, as PreviewDim dims the pictures, and the day's otherwise.</summary>
        public static int RigBrightnessInForce(bool night, int day, int nightBrightness)
        {
            return night ? nightBrightness : day;
        }

        /// <summary>The brightness chooser's entries: the rig's, named with its value in force
        /// (<see cref="RigBrightnessInForce"/>), then each step. The page draws the chooser again when that value
        /// moves, by a wheel's press or by night mode, so the value named is the one the strip runs at.</summary>
        public static string[] BrightnessLabels(int rig)
        {
            var labels = new List<string> { "Same as rig" + Dot + Percent(rig) };
            labels.AddRange(BrightnessSteps.Select(Percent));
            return labels.ToArray();
        }

        /// <summary>The entry a strip's own brightness is: 0 for the rig's, else its step, rounded to the nearest
        /// ten so a value written elsewhere still lands on an entry.</summary>
        public static int BrightnessIndex(int? own)
        {
            if (!own.HasValue) return 0;
            var nearest = 0;
            for (var i = 1; i < BrightnessSteps.Length; i++)
            {
                if (Math.Abs(BrightnessSteps[i] - own.Value) < Math.Abs(BrightnessSteps[nearest] - own.Value)) nearest = i;
            }
            return nearest + 1;
        }

        /// <summary>What an entry writes: null (the rig's) for the first, else its step.</summary>
        public static int? BrightnessValue(int index)
        {
            if (index <= 0 || index > BrightnessSteps.Length) return null;
            return BrightnessSteps[index - 1];
        }

        public const string ReverseTitle = "Reverse direction";

        /// <summary>The line after a Brightness pick (ruling 50: every press says what it did): what the strip
        /// runs at now, its own value or the rig's. A statement rather than "Set ...", which reads as the fix box's
        /// imperative, and never a possessive with no noun after it. At night the profile runs a strip at the lower
        /// of its own value and the night brightness (contract.ts ledBrightnessInForce), as
        /// <see cref="PreviewDim"/> draws it, so a value above the night brightness is said with the one in force.</summary>
        public static string BrightnessSaid(string name, int? value, bool night = false, int nightBrightness = 100)
        {
            if (!value.HasValue) return name + " runs at the rig's brightness.";
            var own = name + " runs at " + Percent(value.Value) + " brightness";
            return night && nightBrightness < value.Value
                ? own + ", " + Percent(nightBrightness) + " while night mode is on."
                : own + ".";
        }

        /// <summary>The greyed "Each LED in turn" row's press (#434).</summary>
        public const string EachLedStart = "Start";

        // --- Effects -----------------------------------------------------------------------------------------

        public const string EffectsTitle = "Effects";
        public const string EffectsCaption = "This strip only";

        /// <summary>The narrowest an effect's tile is laid at: the longest label, "Speeding in the pit lane" (about
        /// 142 at 14 px), the 12 gap, the 40 switch and the tile's 24 of padding, so three tiles hold every label on
        /// one line. PanelLedsTests measures every label from the bundled fonts.</summary>
        public const double EffectTileMinWidth = 220;

        /// <summary>The artboard's gap between effect tiles.</summary>
        public const double EffectTileGap = 6;

        /// <summary>Three to a row, as the artboard lays them.</summary>
        public const int EffectColumns = 3;

        // The roles of packages/dash/src/leds/effects.ts.
        public const string RoleSide = "side";
        public const string RoleRace = "race";
        public const string RoleCar = "car";
        public const string RoleAid = "aid";
        public const string RoleStrip = "strip";

        /// <summary>
        /// Every effect switch, in the artboard's order: the fifteen settings of Contract.LedEffectSettings,
        /// each once. There is no single Spotter or Turn signals switch, since each side is a setting of its own.
        /// </summary>
        public static readonly IReadOnlyList<LedEffectSwitch> Effects = new[]
        {
            new LedEffectSwitch(Contract.LedEffectFlags, "Flags", RoleRace),
            new LedEffectSwitch("LedEffectSpotterLeft", "Spotter left", RoleSide),
            new LedEffectSwitch("LedEffectSpotterRight", "Spotter right", RoleSide),
            new LedEffectSwitch("LedEffectPitLane", "Pit lane", RoleStrip),
            new LedEffectSwitch("LedEffectPitLimiter", "Pit limiter", RoleStrip),
            new LedEffectSwitch("LedEffectPitSpeeding", "Speeding in the pit lane", RoleStrip),
            new LedEffectSwitch("LedEffectTc", "TC", RoleAid, "traction control"),
            new LedEffectSwitch("LedEffectAbs", "ABS", RoleAid),
            new LedEffectSwitch("LedEffectDrs", "DRS", RoleAid),
            new LedEffectSwitch("LedEffectPushToPass", "Push to pass", RoleAid),
            new LedEffectSwitch(LowFuelSetting, "Low fuel", RoleCar),
            new LedEffectSwitch("LedEffectTemperature", "Temperature", RoleCar, "oil temperature", "water temperature"),
            new LedEffectSwitch("LedEffectOilPressure", "Oil pressure", RoleCar),
            new LedEffectSwitch(TurnLeftSetting, "Turn signal left", RoleSide),
            new LedEffectSwitch(TurnRightSetting, "Turn signal right", RoleSide),
        };

        /// <summary>
        /// The roles a side of <paramref name="count"/> LEDs carries, as lampsForSide in
        /// packages/dash/src/leds/lamps.ts assigns them: none on a side of none, the aids dropped below three.
        /// </summary>
        private static IEnumerable<string> RolesOfSide(int count)
        {
            if (count <= 0) return new string[0];
            if (count <= 2) return new[] { RoleSide, RoleRace, RoleCar };
            return new[] { RoleSide, RoleRace, RoleCar, RoleAid };
        }

        /// <summary>
        /// The effect switches a shape carries, in the grid's order: exactly the switches the shape's profile
        /// reads, so a switch for something the strip can never show is not offered and nothing the strip
        /// shows goes without its switch. PanelLedsTests holds this to every profile the build embeds.
        /// </summary>
        /// <remarks>
        /// rpmStrip.ts effects(): every lamp at the ends carries its roles, and the pit family is drawn over the
        /// whole run on every shape. A side of one or two has no aid lamp. On a side of one, lampConditions in
        /// effects.ts drops the turn signal, which is drawn as the green flag is, so that side's turn switch
        /// governs nothing and is not offered. A shape with no ends has no lamp to put anything on, so it draws
        /// the flags, a car alongside and the turn signals over the whole run instead, and drops the aids and
        /// the car's own warnings, which would be the rev LEDs flashing for ABS. Its centre can show the fuel,
        /// though, and the fuel centre flashes for low fuel, so a bare run keeps that one switch.
        /// </remarks>
        public static IList<LedEffectSwitch> EffectsFor(int left, int right)
        {
            var bare = left <= 0 && right <= 0;
            var roles = new HashSet<string>(RolesOfSide(left).Concat(RolesOfSide(right)), StringComparer.Ordinal) { RoleStrip };
            if (bare)
            {
                roles.Add(RoleRace);
                roles.Add(RoleSide);
            }
            return Effects
                .Where(effect => roles.Contains(effect.Role) || (bare && effect.Setting == LowFuelSetting))
                .Where(effect => !(effect.Setting == TurnLeftSetting && left == 1) && !(effect.Setting == TurnRightSetting && right == 1))
                .ToList();
        }

        private const string LowFuelSetting = "LedEffectLowFuel";
        private const string TurnLeftSetting = "LedEffectTurnLeft";
        private const string TurnRightSetting = "LedEffectTurnRight";

        /// <summary>The same, for a strip's shape id.</summary>
        public static IList<LedEffectSwitch> EffectsFor(string shapeId)
        {
            var shape = LightShape.Parse(shapeId);
            return shape == null ? new List<LedEffectSwitch>(Effects) : EffectsFor(shape.Left, shape.Right);
        }

        /// <summary>Whether the full-strip spotter does anything on a shape: only where an end carries the
        /// spotter's lamp, since a bare run already lights all of itself for a car alongside.</summary>
        public static bool HasFullStripSpotter(int left, int right)
        {
            return left > 0 || right > 0;
        }

        /// <summary>The same, for a strip's shape id: an id this cannot read is offered the row, as it is
        /// offered every effect, rather than nothing.</summary>
        public static bool HasFullStripSpotter(string shapeId)
        {
            var shape = LightShape.Parse(shapeId);
            return shape == null || HasFullStripSpotter(shape.Left, shape.Right);
        }

        /// <summary>The flag animation row, on every strip: a bare run carries the flags over its whole length.</summary>
        public const string FlagAnimationTitle = "Flag animation";

        public const string SpotterTitle = "Full-strip spotter";
        public const string SpotterCaption = "Lights the whole strip for a car alongside, instead of just the end nearest it.";

        /// <summary>The greyed Pit limiter lights row's two values (#509).</summary>
        public static readonly string[] PitLimiterLightsValues = { "ends", "whole" };
        public static readonly string[] PitLimiterLightsLabels = { "Ends", "Whole strip" };

        // --- Every strip ---------------------------------------------------------------------------------------

        /// <summary>The section of what is the same for every strip.</summary>
        public const string EveryStripTitle = "Every strip";

        // --- The Add LEDs sheet --------------------------------------------------------------------------------

        public const string HardwareStep = "Hardware";

        /// <summary>The hardware tiles, two to a row 8 apart, as the sheet lays them, and never narrower than the
        /// Fanatec tile's 3 · 9 · 3 at the card's 9 px LEDs (171) inside the tile's 12 of padding and its border:
        /// a grid that laid two tiles narrower cut the picture at its right edge.</summary>
        public const double HardwareTileMinWidth = 200;
        public const double HardwareTileGap = 8;

        public const string ShapeStep = "Shape";

        /// <summary>The tile for every strip that is not a Fanatec wheel, and the line under it.</summary>
        public const string SomethingElse = "Something else";
        public const string SomethingElseNote = "Any RGB strip";

        /// <summary>The eyebrow on the Fanatec tile when SimHub has a device of that name.</summary>
        public const string FoundInSimHub = "Found in SimHub";

        /// <summary>The artboard's size for that eyebrow, a point below the kit's.</summary>
        public const double FoundInSimHubSize = 10;

        /// <summary>The note under the tiles. The artboard promises more wheels; the panel promises nothing.</summary>
        public const string OtherWheelNote = "For any other wheel, use Something else.";

        public const string SetByTheWheel = "Set by the wheel";
        public const string Fixed = "Fixed";

        /// <summary>The shape the Fanatec tile and the fixed shape row show.</summary>
        public static readonly string FanatecShape = ShapeDots(PanelLights.FanatecShapeId);

        /// <summary>The label of a count of ends in the segmented choice: None for a bare run.</summary>
        public static string EndsLabel(int ends)
        {
            return ends <= 0 ? "None" : Digits(ends);
        }

        /// <summary>
        /// The picture of a shape in the sheet: the ends in the flag's yellow, and the centre the whole rev
        /// ladder, green to amber to red. A bare run is the ladder alone, never a yellow fill, which would read
        /// as a flag rather than as the shape.
        /// </summary>
        public static string[][] ShapeFrame(int ends, int centre)
        {
            var middle = new string[Math.Max(0, centre)];
            for (var i = 0; i < middle.Length; i++)
            {
                var f = (i + 1) / (double)middle.Length;
                middle[i] = f <= 0.45 ? Theme.ShiftStage1 : f <= 0.8 ? Theme.ShiftStage2 : Theme.ShiftStage3;
            }
            if (ends <= 0) return new[] { middle };
            var left = Enumerable.Repeat(Theme.FlagYellow, ends).ToArray();
            var right = Enumerable.Repeat(Theme.FlagYellow, ends).ToArray();
            return new[] { left, middle, right };
        }

        /// <summary>The LEDs the Something else tile draws: twelve, in the border's grey, as AddLeds.dc.html draws
        /// them. Not a strip at rest, whose unlit LEDs are the raised surface one step above the tile's ground and
        /// all but vanish there beside the lit Fanatec tile.</summary>
        public static string[][] AnyStripFrame()
        {
            return new[] { Enumerable.Repeat(Theme.Border, AnyStripLeds).ToArray() };
        }

        /// <summary>How many LEDs the Something else tile draws.</summary>
        private const int AnyStripLeds = 12;

        /// <summary>What a device row in the sheet says at its right: the rig's strips already on it, so a
        /// driver sees a device is taken before installing a second profile into its list, and whether SimHub
        /// is talking to it.</summary>
        /// <remarks>
        /// Bounded, since the names are the driver's own and the kit's radio row docks its meta at full width
        /// beside the device's name: one strip is named, cut to <see cref="DeviceMetaNameChars"/>, and the rest are
        /// counted, with "Not connected" last. PanelLedsTests measures the widest it can be against the sheet.
        /// </remarks>
        public static string DeviceMeta(bool connected, IEnumerable<string> stripsOnIt)
        {
            var names = (stripsOnIt ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList();
            var parts = new List<string>();
            if (names.Count > 0)
            {
                var first = names[0].Length > DeviceMetaNameChars ? names[0].Substring(0, DeviceMetaNameChars - 1).TrimEnd() + "…" : names[0];
                parts.Add(names.Count == 1 ? first : first + " and " + Digits(names.Count - 1) + " more");
            }
            if (!connected) parts.Add(NotConnected);
            return parts.Count == 0 ? null : string.Join(Dot, parts);
        }

        /// <summary>The most characters of a strip's name a device row in the sheet spells out.</summary>
        public const int DeviceMetaNameChars = 14;

        /// <summary>Whether SimHub has a device whose name says it is a Fanatec wheel, which is what the tile's
        /// eyebrow claims and all it can: nothing detects the wheel's LEDs themselves.</summary>
        public static bool FoundFanatec(IEnumerable<string> deviceNames)
        {
            return deviceNames != null && deviceNames.Any(name => name != null && name.IndexOf("Fanatec", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Which tile the sheet opens on: the Fanatec wheel where this build carries its profile and
        /// SimHub has one, else Something else.</summary>
        public static bool StartsOnFanatec(bool offered, bool found)
        {
            return offered && found;
        }

        /// <summary>Whether the sheet has a shape to offer at all: a plain shape or the Fanatec wheel. Where it
        /// has neither it says <see cref="NoProfiles"/> and nothing else.</summary>
        public static bool SheetHasShapes(int plainSides, bool offersFanatec)
        {
            return plainSides > 0 || offersFanatec;
        }

        /// <summary>The tile the sheet opens on, the build's plain shapes counted: the Fanatec wheel wherever the
        /// build has no plain shape, since then it is the only tile.</summary>
        public static bool SheetStartsOnFanatec(bool hasPlainShapes, bool offersFanatec, bool found)
        {
            return !hasPlainShapes || StartsOnFanatec(offersFanatec, found);
        }

        /// <summary>The ends the sheet opens on: three where the build has them, the artboard's 3 · 9 · 3, else
        /// the fewest it has, and the Fanatec wheel's where it has no plain shape.</summary>
        public static int StartSide(int[] sides)
        {
            if (sides == null || sides.Length == 0) return PanelLights.FanatecSide;
            return sides.Contains(3) ? 3 : sides[0];
        }

        /// <summary>The centre the sheet opens on, and the one it keeps when the ends change: the one chosen where
        /// the new ends have it, else nine where they do, else the fewest, and the Fanatec wheel's with none.</summary>
        public static int KeptCentre(int[] centres, int chosen)
        {
            if (centres == null || centres.Length == 0) return PanelLights.FanatecCentre;
            if (centres.Contains(chosen)) return chosen;
            return centres.Contains(9) ? 9 : centres[0];
        }

        /// <summary>The note under the tiles goes with the Fanatec tile: without it there is only Something else,
        /// and no other wheel to point away from.</summary>
        public static bool ShowsOtherWheelNote(bool offersFanatec)
        {
            return offersFanatec;
        }

        /// <summary>Whether the device step says <see cref="PanelLights.NoDevices"/>: only with nothing offered
        /// and nothing passed over, since a device passed over is a disabled row saying so.</summary>
        public static bool ShowsNoDevices(int offered, int passedOver)
        {
            return offered == 0 && passedOver == 0;
        }

        /// <summary>What a device OpenDash passed over says in the device list (PanelLights.NotOffered's fact).</summary>
        public const string NotReachable = "No LEDs OpenDash can reach";

        /// <summary>The one line under the rows passed over: the rows say which, and the log says why, in the
        /// one form every line on the page points at it.</summary>
        public const string PassedOverNote = "See SimHub's log.";

        /// <summary>What a device SimHub is not talking to says beside its name. A profile installs into it all
        /// the same.</summary>
        public const string NotConnected = "Not connected";

        public const string AddAndInstall = "Add and install";

        /// <summary>The footer's line: what the press will install and where, the profile listed under the
        /// strip's own name. Null with no SimHub device, where the press can install nothing and the device
        /// step already says why.</summary>
        public static string InstallsOn(string name, string device)
        {
            if (string.IsNullOrWhiteSpace(device)) return null;
            return "Installs \"" + (name ?? string.Empty).Trim() + "\" on " + device + ".";
        }

        /// <summary>The name the press adds a strip under, as OpenDashSettings.AddLedBar settles it: what was
        /// typed, or the name the sheet opened on where the box was cleared, numbered where the rig already
        /// has it. The footer names this, so it names what will be added.</summary>
        public static string NameToAdd(string typed, string defaultName, IEnumerable<string> taken)
        {
            var wanted = string.IsNullOrWhiteSpace(typed) ? defaultName : typed.Trim();
            return PackageCatalogue.UniqueName(wanted, taken ?? Enumerable.Empty<string>());
        }

        public const string WheelRim = "Wheel rim";

        /// <summary>
        /// The name the sheet opens on for a shape: "Wheel rim" for the Fanatec wheel and "Strip" for anything
        /// else, numbered as the rig numbers a second one, so the footer names what will be added.
        /// </summary>
        public static string DefaultName(string shapeId, IEnumerable<string> taken)
        {
            var name = Hardware(shapeId) == PanelLights.BarFanatecTitle ? WheelRim : SomethingElseStrip;
            return PackageCatalogue.UniqueName(name, taken ?? Enumerable.Empty<string>());
        }

        public const string NoProfiles = "This build ships no strip profiles.";

        // --- What is said after a press ---------------------------------------------------------------------------

        /// <summary>A profile installed from the header: the step SimHub does not take, selecting it, on the
        /// SimHub device it went to, or in SimHub where that device cannot be named. A note the install
        /// returned (the device listing only its maker's profiles) comes first, since the select waits on it.</summary>
        public static string ProfileInstalled(string name, string device, string note = null)
        {
            return Steps("Installed " + name + "'s profile.", note, SelectIt(name, device));
        }

        /// <summary>The step SimHub does not take after an install, in the one form every press says it: after
        /// an install, a move, a Reverse and, behind the restart, an Add (PanelLights.BarAdded).</summary>
        public static string SelectIt(string name, string device)
        {
            var on = string.IsNullOrWhiteSpace(device) ? string.Empty : " on " + device;
            return "Select \"" + name + "\"" + on + " in SimHub to use it.";
        }

        public static string ProfileUpdated(string name, string note = null)
        {
            return Steps("Updated " + name + "'s profile.", note);
        }

        public static string ProfileFailed(string name)
        {
            return "Could not install " + name + "'s profile. See SimHub's log.";
        }

        /// <summary>The header's Update press, failed: in the press's own verb.</summary>
        public static string UpdateFailed(string name)
        {
            return "Could not update " + name + "'s profile. See SimHub's log.";
        }

        /// <summary>A strip moved to another SimHub device, which installs its profile there fresh and
        /// unselected: the step SimHub does not take, as after any install.</summary>
        public static string Moved(string name, string device, string note = null)
        {
            var moved = string.IsNullOrWhiteSpace(device) ? "Moved " + name + "'s profile." : "Moved " + name + "'s profile to " + device + ".";
            return Steps(moved, note, SelectIt(name, device));
        }

        /// <summary>
        /// A device pick whose install failed. The device was saved before the install, and the install takes the
        /// old copy out of every device first, so the strip is on the new device with no profile there: the line
        /// says that, and the header then offers Install.
        /// </summary>
        public static string MovedNotInstalled(string name, string device)
        {
            return string.IsNullOrWhiteSpace(device)
                ? name + "'s device is changed, but its profile could not be installed. See SimHub's log."
                : name + " is set to " + device + ", but its profile could not be installed there. See SimHub's log.";
        }

        /// <summary>A device pick the page could not follow, before anything was written: the reason
        /// (<see cref="WithReason"/>) follows it.</summary>
        public static string NotMoved(string name)
        {
            return "Could not move " + name + "'s profile.";
        }

        /// <summary>A rename, which installs the profile again so SimHub's list carries the new name.</summary>
        public static string Renamed(string name, bool inSimHub)
        {
            return inSimHub ? "Renamed to " + name + " in OpenDash and SimHub." : "Renamed to " + name + ".";
        }

        /// <summary>Whether a rename installs the profile again: only where SimHub holds it, up to date or not,
        /// so SimHub's list carries the new name. A profile SimHub lacks has no name there to change.</summary>
        public static bool RenameReinstalls(FlagBoxInstallState? profile)
        {
            return profile == FlagBoxInstallState.UpToDate || profile == FlagBoxInstallState.Outdated;
        }

        /// <summary>Whether a Rename press can go ahead: a name of nothing but spaces renames nothing, so the
        /// press is disabled while the box holds one.</summary>
        public static bool CanRename(string typed)
        {
            return !string.IsNullOrWhiteSpace(typed);
        }

        /// <summary>The line after the header's Install or Update: an update where SimHub held an older copy,
        /// else an install with the step SimHub does not take; the log where it failed.</summary>
        public static string InstallSaid(bool ok, FlagBoxInstallState? before, string name, string device, string note = null)
        {
            if (!ok) return before == FlagBoxInstallState.Outdated ? UpdateFailed(name) : ProfileFailed(name);
            return before == FlagBoxInstallState.Outdated ? ProfileUpdated(name, note) : ProfileInstalled(name, device, note);
        }

        /// <summary>A rename whose install failed. The install takes the old copy out of every device first, so
        /// what is left is no copy, and the log says why.</summary>
        public static string RenameNotInSimHub(string name)
        {
            return "Renamed to " + name + ", but its profile could not be installed again. See SimHub's log.";
        }

        /// <summary>A line after a press that could not install the profile, with the reason
        /// <see cref="ProfileBlocked"/> gives, which is the step left to the driver.</summary>
        public static string WithReason(string said, string reason)
        {
            return Steps(said, reason);
        }

        /// <summary>
        /// The line after Remove: the strip and its profile where the profile was taken out of SimHub, the strip
        /// alone where SimHub never held one, and the log where SimHub held one and it is still there or the
        /// uninstall threw (<paramref name="takenOut"/> null).
        /// </summary>
        public static string Removed(string name, bool heldInSimHub, bool? takenOut)
        {
            if (takenOut == true) return "Removed " + name + " and its profile.";
            if (!RemovedCleanly(heldInSimHub, takenOut)) return "Removed " + name + ", but its profile could not be removed from SimHub. See SimHub's log.";
            return "Removed " + name + ".";
        }

        /// <summary>Whether a Remove left nothing behind in SimHub: false where the uninstall threw, or SimHub held
        /// the profile and nothing was taken out.</summary>
        public static bool RemovedCleanly(bool heldInSimHub, bool? takenOut)
        {
            return takenOut == true || (takenOut == false && !heldInSimHub);
        }

        public static string ReverseSaid(string name, bool reversed)
        {
            return reversed ? "Reversed " + name + "." : name + " is no longer reversed.";
        }

        /// <summary>
        /// The line after a Reverse that installed the other twin. Where SimHub held the strip's profile, the
        /// twin replaces it; where it did not, the twin is installed fresh and unselected, so the line says the
        /// step SimHub does not take.
        /// </summary>
        public static string ReverseSaid(string name, bool reversed, bool wasInSimHub, string device, string note = null)
        {
            return Steps(ReverseSaid(name, reversed), note, wasInSimHub ? null : SelectIt(name, device));
        }

        /// <summary>
        /// A Reverse whose install failed. The direction was saved before the install, and the install takes the
        /// old copy out of every device first, so the line says what the strip is now, as a failed rename or move
        /// does.
        /// </summary>
        public static string ReversedNotInstalled(string name, bool reversed)
        {
            var now = reversed ? "Reversed " + name : name + " is no longer reversed";
            return now + ", but its profile could not be installed again. See SimHub's log.";
        }

        /// <summary>Whether SimHub held the profile before a press, up to date or not: the one state in which an
        /// install replaces a copy rather than adding one the driver has still to select.</summary>
        public static bool HeldInSimHub(FlagBoxInstallState? profile)
        {
            return profile == FlagBoxInstallState.UpToDate || profile == FlagBoxInstallState.Outdated;
        }

        /// <summary>The Add sheet's press where SimHub offers no LED device: the strip is added, and its profile
        /// waits for a device, so the press promises no install.</summary>
        public const string AddOnly = "Add";

        /// <summary>The press's label: an install only where there is a device to install on.</summary>
        public static string AddPress(bool hasDevice)
        {
            return hasDevice ? AddAndInstall : AddOnly;
        }

        /// <summary>
        /// A strip added while SimHub offered no LED device: the steps left, in order, which start in SimHub. The
        /// profile is installed here once there is a device, by the header's Install or the device row, and the
        /// restart comes last, since the plugin publishes a strip's own settings only for the strips it held when
        /// SimHub started (PanelLights.BarAdded says the same).
        /// </summary>
        /// <remarks>Where SimHub lists LED devices OpenDash passed over (#437), the first step is not to add hardware
        /// SimHub already has: the line says which devices OpenDash cannot reach and points at the log, as the
        /// SimHub device row under it does, then names the two steps every add without an install leaves, in
        /// PanelLights.BarAddFailed's form.</remarks>
        public static string AddedWithoutDevice(string name, IList<string> declined = null)
        {
            var passed = PanelLights.Unreached(declined);
            if (passed != null) return "Added " + name + ", but " + passed + ". See SimHub's log, then install " + name + "'s profile here and restart SimHub.";
            return "Added " + name + ". Add your wheel or Arduino in SimHub, install " + name + "'s profile here, then restart SimHub.";
        }


        // --- Search ---------------------------------------------------------------------------------------------

        /// <summary>Every row label and heading the page draws. An effect is drawn from its switch's Label in a
        /// loop, which SearchDrawnOtherwise says.</summary>
        public static readonly PanelSearch.Entry[] Search = BuildSearch();

        private static PanelSearch.Entry[] BuildSearch()
        {
            var entries = new List<PanelSearch.Entry>
            {
                new PanelSearch.Entry(PanelLights.AddBar, PanelPage.Leds, AnchorStrips, "strip", "wheel", "rim", "brow", "your LED strips", "add leds", "new"),
                new PanelSearch.Entry(RevLightsTitle, PanelPage.Leds, AnchorRevLights, "shift lights", "rpm"),
                new PanelSearch.Entry(CarRevLightsTitle, PanelPage.Leds, AnchorRevStyle, "rev light style", "car-specific", "use the car's own rev lights", "shift lights", "rpm"),
                new PanelSearch.Entry(MirrorFitTitle, PanelPage.Leds, AnchorMirrorFit, "stretch", "actual size", "width", "true size", "fill the strip"),
                new PanelSearch.Entry(CentreDisplayTitle, PanelPage.Leds, AnchorCentre, "middle", "brake", "throttle", "fuel"),
                new PanelSearch.Entry(ThisStripTitle, PanelPage.Leds, AnchorThisStrip, "strip"),
                new PanelSearch.Entry(PanelLights.BarDeviceTitle, PanelPage.Leds, AnchorDevice, "LED device", "arduino", "wheel"),
                new PanelSearch.Entry(BrightnessTitle, PanelPage.Leds, AnchorBrightness, "strip brightness", "dim"),
                new PanelSearch.Entry(ReverseTitle, PanelPage.Leds, AnchorReverse, "reversed", "far end", "wiring"),
                new PanelSearch.Entry(EffectsTitle, PanelPage.Leds, AnchorEffects, "effect", "this strip only"),
                new PanelSearch.Entry(FlagAnimationTitle, PanelPage.Leds, AnchorFlagAnimation, "flags"),
                new PanelSearch.Entry(SpotterTitle, PanelPage.Leds, AnchorSpotter, "car alongside"),
                new PanelSearch.Entry(EveryStripTitle, PanelPage.Leds, AnchorEveryStrip, "strip"),
                new PanelSearch.Entry(PanelLights.CarTablesTitle, PanelPage.Leds, AnchorCarTables, "car light tables", "car data", "download"),
            };
            foreach (var effect in Effects)
            {
                entries.Add(new PanelSearch.Entry(effect.Label, PanelPage.Leds, AnchorEffects, new[] { "effect" }.Concat(effect.Keywords).ToArray()));
            }
            return entries.ToArray();
        }

        /// <summary>The greyed rows this page draws, which search lists: PanelSoonTests holds the list to this
        /// page's own sources.</summary>
        public static readonly SoonItem[] SoonDrawn =
        {
            PanelSoon.EachLedInTurn,
            PanelSoon.PitLimiterLights,
            PanelSoon.IdleSweep,
            PanelSoon.EngineStartAnimation,
            PanelSoon.CarDataForAcAccLmu,
        };

        /// <summary>The effect labels, drawn from each switch's Label in the grid's loop rather than from a
        /// constant each.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise =
            Effects.ToDictionary(effect => effect.Label, effect => "effect.Label", StringComparer.Ordinal);

        // --- Helpers ------------------------------------------------------------------------------------------------

        /// <summary>The spaced dot the artboards join a shape's counts and a chip's parts with.</summary>
        public const string Dot = " · ";

        /// <summary>The sentences of a line, each one there, joined with a space.</summary>
        private static string Steps(params string[] sentences)
        {
            return string.Join(" ", sentences.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        }

        private static string Digits(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Percent(int value)
        {
            return Digits(value) + "%";
        }
    }
}
