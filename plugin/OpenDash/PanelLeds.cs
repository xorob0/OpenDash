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
        public LedEffectSwitch(string setting, string label, string role)
        {
            Setting = setting;
            Label = label;
            Role = role;
        }

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
        /// Where Home's issue and the sidebar's dot send the driver for a strip profile's update: Updates' lights
        /// section. The LEDs header carries an Update press of its own now, but PanelAttentionTests pins this
        /// route to Updates, so moving it to new PanelRoute(PanelPage.Leds, AnchorStrips) waits on that test.
        /// </summary>
        public static readonly PanelRoute StripUpdateRoute = new PanelRoute(PanelPage.Updates, PanelUpdates.AnchorLights);

        // --- The cards ------------------------------------------------------------------------------------

        /// <summary>The narrowest a strip's card is laid at, which fits a 3/9/3 at the card's 9 px LEDs; the grid
        /// takes three columns where the artboard does, and fewer below.</summary>
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

        public const string InstallTooltip = "Installs this strip's profile in SimHub.";
        public const string UpdateTooltip = "Installs this build's version of this strip's profile.";

        public const string RenameButton = "Rename";
        public const string RemoveButton = "Remove";
        public const string RemoveTooltip = "Removes this strip and its profile from SimHub.";
        public const string RemoveConfirm = "Remove it";
        public const string RemoveBody = "Removes the strip, its settings and its profile in SimHub.";

        /// <summary>The fix box's title for a profile SimHub holds and has not selected, in the card's words.</summary>
        public const string NotSelectedTitle = "Installed, but not selected in SimHub";

        // --- The preview ----------------------------------------------------------------------------------

        /// <summary>The chip that shows the car's own rev lights as they are now.</summary>
        public const string LiveScenario = "live";

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
        public static string[][] PreviewFrame(string scenario, int ends, int centre, StripOptions options, bool live, string packed)
        {
            if (scenario == LiveScenario || PanelEmulation.Find(scenario) == null)
            {
                return live ? PanelEmulation.LiveFrame(packed, ends, centre) : PanelEmulation.StripFrame(ends, centre, PanelEmulation.Idle, options);
            }
            return PanelEmulation.StripFrame(ends, centre, scenario, options);
        }

        /// <summary>The frame a strip's card draws: the revs half way, as the Rig page opens on.</summary>
        public static string[][] CardFrame(string shapeId, StripOptions options)
        {
            return PanelEmulation.StripFrame(Ends(shapeId), Centre(shapeId), PanelEmulation.Mid, options);
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

        /// <summary>The #369 switch: the car's own rev lights, or the plain ladder. The artboard's words, which
        /// #369 decided; the noun form would be "Car's own rev lights" (docs/design/plugin.md).</summary>
        public const string CarRevLightsTitle = "Use the car's own rev lights";
        public const string CarRevLightsCaption = "Off fills the strip left to right.";

        /// <summary>Whether a strip's stored style reads as the switch on. A retired style (meetInMiddle, f1)
        /// normalises to left to right, and reads as off.</summary>
        public static bool UsesCarRevLights(string rpmStyle)
        {
            return Contract.NormaliseLedRpmStyle(rpmStyle) == Contract.LedRpmStyleCar;
        }

        /// <summary>
        /// The line under the switch: whether the car in the sim is one Lovely Car Data has measured. Null
        /// while the switch is off or no car is loaded, since then there is nothing to say.
        /// </summary>
        public static string CarLine(bool on, string car, bool hasTable)
        {
            if (!on || string.IsNullOrWhiteSpace(car)) return null;
            return car.Trim() + (hasTable ? " is in Lovely Car Data." : " is not in Lovely Car Data.");
        }

        /// <summary>The width row, in the noun the switch uses: "Rev light width". The artboard's bare "Width"
        /// says nothing in a search result.</summary>
        public const string MirrorFitTitle = "Rev light width";

        /// <summary>The width is the rig's (LedMirrorFit), so the row says so.</summary>
        public const string MirrorFitCaption = "Every strip.";

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

        /// <summary>The brightness chooser's entries: the rig's, named with its value, then each step.</summary>
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

        /// <summary>The greyed "Each LED in turn" row's press (#434).</summary>
        public const string EachLedStart = "Start";

        // --- Effects -----------------------------------------------------------------------------------------

        public const string EffectsTitle = "Effects";
        public const string EffectsCaption = "This strip only";

        /// <summary>The narrowest an effect's tile is laid at: the longest label and its switch.</summary>
        public const double EffectTileMinWidth = 200;

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
            new LedEffectSwitch("LedEffectTc", "TC", RoleAid),
            new LedEffectSwitch("LedEffectAbs", "ABS", RoleAid),
            new LedEffectSwitch("LedEffectDrs", "DRS", RoleAid),
            new LedEffectSwitch("LedEffectPushToPass", "Push to pass", RoleAid),
            new LedEffectSwitch(LowFuelSetting, "Low fuel", RoleCar),
            new LedEffectSwitch("LedEffectTemperature", "Temperature", RoleCar),
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

        /// <summary>The hardware tiles, two to a row 8 apart, as the sheet lays them.</summary>
        public const double HardwareTileMinWidth = 180;
        public const double HardwareTileGap = 8;

        public const string ShapeStep = "Shape";

        /// <summary>The tile for every strip that is not a Fanatec wheel, and the line under it.</summary>
        public const string SomethingElse = "Something else";
        public const string SomethingElseNote = "Any RGB strip";

        /// <summary>The eyebrow on the Fanatec tile when SimHub has a device of that name.</summary>
        public const string FoundInSimHub = "Found in SimHub";

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

        /// <summary>What a device OpenDash passed over says in the device list (PanelLights.NotOffered's fact).</summary>
        public const string NotReachable = "No LEDs OpenDash can reach";

        /// <summary>What a device SimHub is not talking to says beside its name. A profile installs into it all
        /// the same.</summary>
        public const string NotConnected = "Not connected";

        public const string AddAndInstall = "Add and install";

        /// <summary>The footer's line: what the press will install and where. The profile is listed under the
        /// strip's own name.</summary>
        public static string InstallsOn(string name, string device)
        {
            var quoted = "\"" + (name ?? string.Empty).Trim() + "\"";
            return string.IsNullOrWhiteSpace(device) ? "Installs " + quoted + "." : "Installs " + quoted + " on " + device + ".";
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

        /// <summary>A profile installed from the header: the step SimHub does not take, selecting it.</summary>
        public static string ProfileInstalled(string name, string device)
        {
            var where = string.IsNullOrWhiteSpace(device) ? "your LED device" : device;
            return "Installed " + name + ". Select \"" + name + "\" on " + where + " in SimHub to use it.";
        }

        public static string ProfileUpdated(string name)
        {
            return "Updated " + name + "'s profile.";
        }

        public static string ProfileFailed(string name)
        {
            return "Could not install " + name + "'s profile. See SimHub's log.";
        }

        public static string Moved(string name, string device)
        {
            return "Moved " + name + "'s profile to " + (string.IsNullOrWhiteSpace(device) ? "that device" : device) + ".";
        }

        /// <summary>A rename, which installs the profile again so SimHub's list carries the new name.</summary>
        public static string Renamed(string name, bool inSimHub)
        {
            return inSimHub ? "Renamed to " + name + ", in SimHub too." : "Renamed to " + name + ".";
        }

        public static string RenameNotInSimHub(string name)
        {
            return "Renamed to " + name + ", but SimHub still lists the old name. See SimHub's log.";
        }

        public static string Removed(string name)
        {
            return "Removed " + name + " and its profile.";
        }

        public static string ReverseSaid(string name, bool reversed)
        {
            return reversed ? "Reversed " + name + "." : name + " runs in its usual direction again.";
        }

        public static string BrightnessSaid(string name, int? own)
        {
            return own.HasValue ? name + " is at " + Percent(own.Value) + "." : name + " follows the rig's brightness.";
        }

        // --- Search ---------------------------------------------------------------------------------------------

        /// <summary>Every row label and heading the page draws. An effect is drawn from its switch's Label in a
        /// loop, which SearchDrawnOtherwise says.</summary>
        public static readonly PanelSearch.Entry[] Search = BuildSearch();

        private static PanelSearch.Entry[] BuildSearch()
        {
            var entries = new List<PanelSearch.Entry>
            {
                new PanelSearch.Entry(PanelLights.AddBar, PanelPage.Leds, AnchorStrips, "strip", "wheel", "rim", "brow", "your LED strips", "new"),
                new PanelSearch.Entry(RevLightsTitle, PanelPage.Leds, AnchorRevLights, "shift lights", "rpm"),
                new PanelSearch.Entry(CarRevLightsTitle, PanelPage.Leds, AnchorRevStyle, "rev light style", "car-specific", "car's own rev lights", "shift lights", "rpm"),
                new PanelSearch.Entry(MirrorFitTitle, PanelPage.Leds, AnchorMirrorFit, "stretch", "actual size", "width"),
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
                entries.Add(new PanelSearch.Entry(effect.Label, PanelPage.Leds, AnchorEffects, "effect"));
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
