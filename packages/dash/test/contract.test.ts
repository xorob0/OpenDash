/** contract.ts: the card catalogue, the property list and the isnull-wrapped setting reads. */
import { describe, expect, test } from 'bun:test';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import {
  BAR_SLOTS,
  FACE_SIZES,
  FACE_ZONE_LETTERS,
  facePrefix,
  facePropertyNames,
  faceForPrefix,
  flagFormatSettingName,
  FLAG_FORMATS,
  DEFAULT_FLAG_FORMAT,
  zone,
  CARD_CATALOGUE,
  cardMeta,
  declaredProperties,
  ledProperties,
  LED_CENTRES,
  LED_CENTRE_SETTING,
  LED_FLAG_ANIMATION_SETTING,
  RETIRED_LED_CENTRE,
  LED_RPM_STYLES,
  LED_RPM_STYLE_SETTING,
  flagBox,
  flagBoxProperties,
  FLAG_BOX_LOW_FUEL_LAPS_SETTING,
  FLAG_BOX_MATRICES,
  LIGHTS_LOW_FUEL_LAPS_SETTING,
  PIT_WALL_DEFAULT_WIDE_ZONE_PAGE,
  PIT_WALL_DEFAULT_ZONE_PAGES,
  MODULE_CATALOGUE,
  MODULE_COUNT,
  moduleAt,
  moduleMeta,
  moduleSettingName,
  secondScreen,
  PIT_WALL_WIDE_ZONE_PAGES,
  PIT_WALL_ZONE_LETTERS,
  PIT_WALL_ZONE_PAGES,
  pitWallZoneSettingName,
  COMPANION_PREFIX,
  PIT_WALL_PREFIX,
  foreignProperties,
  screenPrefixes,
  screenProperties,
  defaultCardForSlot,
  DEFAULT_SLOT_CARDS,
  DEFAULTS,
  DELTA_REFERENCES,
  POSITION_MODES,
  PROPERTY_PREFIX,
  REV_BAR_MODES,
  REV_BAR_SETTING,
  BLUE_FLAG_DETAILS,
  BLUE_FLAG_DETAIL_SETTING,
  SESSION_PROGRESS_MODES,
  setting,
  SLOT_MAX,
  slotSettingName,
} from '../src/contract.ts';
import { CARDS } from '../src/cards/index.ts';

describe('card catalogue', () => {
  test('has 13 entries numbered 0..12 in order with unique ids', () => {
    expect(CARD_CATALOGUE).toHaveLength(13);
    CARD_CATALOGUE.forEach((c, i) => expect(c.number).toBe(i));
    expect(new Set(CARD_CATALOGUE.map((c) => c.id)).size).toBe(13);
    expect(CARD_CATALOGUE.map((c) => c.id)).toEqual([
      'currentLap', 'lastLap', 'bestLap', 'delta', 'position', 'session', 'fuel', 'fuelLaps', 'tc', 'abs', 'tyreTemps', 'tyrePressures', 'speed',
    ]);
  });

  test('the card modules follow the catalogue', () => {
    expect(CARDS.map((c) => c.id)).toEqual(CARD_CATALOGUE.map((c) => c.id));
    CARDS.forEach((c, i) => expect(c).toMatchObject(CARD_CATALOGUE[i]!));
    expect(() => cardMeta('nope')).toThrow();
  });
});

describe('settings', () => {
  test('declares the dash, the zones, the companion and the pit wall', () => {
    const props = declaredProperties();
    // Per face, not per rig: every face that ships carries its own group, so a 1920 face and an
    // 850 face beside it are configured apart instead of sharing one set of zones. Four per zone --
    // page, mask, start and the class filter -- plus the bar's ends, the glance and the flag format.
    const perFace = FACE_ZONE_LETTERS.length * 4 + BAR_SLOTS.length + 2;
    // The last two terms are the lights, which are not screens but whose settings are properties for
    // the same reason: ADR 0003, and ADR 0013 for why they are here at all. The flag box is six
    // global and ten per matrix, the way every face carries its own group; the strips are the three
    // that decide what a strip shows. It was nine and six until the four settings a box owns --
    // critical flags only, the gear and the two temperatures -- moved under the matrix that owns them.
    expect(flagBoxProperties()).toHaveLength(6 + FLAG_BOX_MATRICES.length * 10);
    expect(ledProperties()).toEqual(['OpenDash.LedCentre', 'OpenDash.LedRpmStyle', 'OpenDash.LedFlagAnimation']);
    // The lone 2 is RevBar and the blue flag detail, which every screen shares with the four modes
    // and the twelve slots.
    expect(props).toHaveLength(
      4 + SLOT_MAX + 2 + FACE_SIZES.length * perFace + MODULE_COUNT + PIT_WALL_ZONE_LETTERS.length + 3 + flagBoxProperties().length + ledProperties().length,
    );
    // And what that sum comes to, said out loud: ContractTests.cs asserts the same number of the
    // plugin's own list, and the two were 246 and 244 for as long as the strips went unattached.
    // 256 before the four settings a box owns became four per matrix, which is twelve names more,
    // 269 before the pit wall gained the class filter its board and its list zones read, and 270
    // before band D was allowed to name the car a blue flag is being waved for.
    expect(props).toHaveLength(271);
    expect(new Set(props).size).toBe(props.length);
    expect(props.slice(0, 4)).toEqual(['OpenDash.ShiftLights', 'OpenDash.PositionMode', 'OpenDash.DeltaReference', 'OpenDash.SessionProgress']);
    expect(props[4]).toBe('OpenDash.Slot01');
    expect(props[15]).toBe('OpenDash.Slot12');
    // The zones are declared here and read by the face from XOR-85. Slot01 to Slot12 stay beside
    // them until the card path is retired, because ten faces still read them.
    // Appended to the shared group rather than beside ShiftLights, which has shipped at index 0.
    expect(props[4 + SLOT_MAX]).toBe('OpenDash.RevBar');
    // Appended after it for the same reason, and shared rather than a face's: the flag *format* is
    // per screen because it decides how much of one screen a flag takes, whereas what a band is
    // allowed to say is the same answer wherever it is written.
    expect(props[5 + SLOT_MAX]).toBe('OpenDash.BlueFlagDetail');
    expect(props).toContain('OpenDash.Face1920x480ZoneA');
    expect(props).toContain('OpenDash.Face1920x480ZoneDPages');
    expect(props).toContain('OpenDash.Face850x480ZoneCStart');
    expect(props).toContain('OpenDash.Face1280x400ZoneBClassOnly');
    expect(props).toContain('OpenDash.Face600x686BarLeft1');
    expect(props).toContain('OpenDash.Face800x286QuickGlance');
    expect(props).toContain('OpenDash.Face1920x480FlagFormat');
    expect(props).toContain('OpenDash.Face600x686FlagFormat');
    // And nothing without a prefix, which is the promise: a bare ZoneA would be one face's
    // settings silently shared with every other.
    expect(props.filter((p) => /^OpenDash\.(Zone|Bar|QuickGlance|FlagFormat)/.test(p))).toEqual([]);
    const lights = flagBoxProperties().length + ledProperties().length;
    expect(props.slice(-(lights + 7), -lights)).toEqual(['OpenDash.PitWallZoneA', 'OpenDash.PitWallZoneB', 'OpenDash.PitWallZoneC', 'OpenDash.PitWallZoneD', 'OpenDash.PitWallWide', 'OpenDash.WebViewUrl', 'OpenDash.PitWallClassOnly']);
    // One filter for the screen and not one per zone: a pit wall zone is a widget pointed at one
    // dashboard file per rectangle, so zones A and B of the race page are the same file.
    expect(secondScreen.classOnly()).toBe('isnull([OpenDash.PitWallClassOnly], false)');
    expect(props.filter((p) => p.endsWith('PitWallClassOnly'))).toHaveLength(1);
    // The lights come last, after the screens, because they are the artefacts the plugin does not
    // install; see ADR 0013. The flag box first, then the strips.
    expect(props.slice(-lights)).toEqual([...flagBoxProperties(), ...ledProperties()]);
  });

  test('every face that ships has a group, and every group is complete', () => {
    const props = new Set(declaredProperties());
    for (const face of FACE_SIZES) {
      for (const name of facePropertyNames(face)) expect({ face: facePrefix(face), name, declared: props.has(`OpenDash.${name}`) }).toMatchObject({ declared: true });
    }
    // The prefix is concatenated and carries no dot of its own, because SimHub puts one in front
    // of every name and whether its parser accepts a second inside the name is unverified.
    for (const face of FACE_SIZES) expect(facePrefix(face)).toMatch(/^Face\d+x\d+$/);
    expect(faceForPrefix('Face1920x480')).toMatchObject({ width: 1920, height: 480 });
    expect(faceForPrefix('Face1x1')).toBeUndefined();
    // The flag format is one of a face's own, on every size, so a rig with two faces can take a flag
    // over the whole of one and leave the other's band alone.
    expect(FLAG_FORMATS).toEqual(['band', 'full']);
    expect(DEFAULT_FLAG_FORMAT).toBe('band');
    for (const face of FACE_SIZES) expect(facePropertyNames(face)).toContain(flagFormatSettingName(face));
    expect(flagFormatSettingName(FACE_SIZES[0]!)).toBe('Face1920x480FlagFormat');
    expect(zone.flagFormat(FACE_SIZES[0]!)).toBe("isnull([OpenDash.Face1920x480FlagFormat], 'band')");
    expect(zone.flagFormatIs(FACE_SIZES[6]!, 'full')).toBe("(isnull([OpenDash.Face800x286FlagFormat], 'band')) = ('full')");
  });

  test('every property belongs to one screen or to every screen', () => {
    // The rule the build enforces is a partition of the contract: a name is one screen's or it is
    // shared by all of them, and never both. Without that a package could be told it may read
    // something no screen owns, or be denied one of its own.
    const declared = declaredProperties();
    const owned = screenPrefixes().flatMap(screenProperties);
    expect(new Set(owned).size).toBe(owned.length);
    for (const name of owned) expect({ name, declared: declared.includes(name) }).toMatchObject({ declared: true });

    // What is left over is what a rig shares and what its lights read. The four modes and the
    // twelve slots mean the same thing on the wheel, on the rim and on the pit wall, so they carry
    // no screen's name; the lights' settings belong to no screen either, because neither a matrix
    // nor a strip is one. Three parts of one partition rather than two parts and an exception.
    const lights = [...flagBoxProperties(), ...ledProperties()];
    for (const name of lights) expect({ name, owned: owned.includes(name) }).toMatchObject({ owned: false });
    const shared = declared.filter((name) => !owned.includes(name) && !lights.includes(name));
    const fixed = ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress'];
    // RevBar is shared too, and has to be: only a rectangular face has a second arrangement, but the
    // round faces' rev arc and the companion's speedo draw the same segments and read the same
    // setting, and a screen may not read a property another screen owns.
    expect(shared).toEqual(
      [...fixed, ...Array.from({ length: SLOT_MAX }, (_, i) => slotSettingName(i + 1)), REV_BAR_SETTING, BLUE_FLAG_DETAIL_SETTING].map((n) => `${PROPERTY_PREFIX}.${n}`),
    );

    // The web view address is the pit wall's although its name carries no prefix: it was named
    // before the idiom, and no other screen has a browser page to point anywhere.
    expect(screenProperties(PIT_WALL_PREFIX)).toContain('OpenDash.WebViewUrl');
    expect(screenProperties(COMPANION_PREFIX)).toHaveLength(MODULE_COUNT);

    const own = facePrefix(FACE_SIZES[0]!);
    expect(foreignProperties(own)).not.toContain('OpenDash.Face1920x480ZoneA');
    expect(foreignProperties(own)).toContain('OpenDash.Face850x480ZoneA');
    expect(foreignProperties(own)).toContain('OpenDash.PitWallZoneA');
    // A card face owns nothing and every group is foreign to it.
    expect(foreignProperties()).toHaveLength(owned.length);
    expect(() => screenProperties('Face1x1')).toThrow(RangeError);
    expect(() => foreignProperties('Face1x1')).toThrow(RangeError);
  });

  test('slot names and defaults', () => {
    expect(slotSettingName(1)).toBe('Slot01');
    expect(slotSettingName(12)).toBe('Slot12');
    // Speed leads so that even a two-slot round face shows it beside the gear.
    expect(defaultCardForSlot(1)).toBe(12);
    expect(defaultCardForSlot(12)).toBe(10);
    expect(DEFAULT_SLOT_CARDS).toEqual([12, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    // Tyre pressures is the one card no slot shows by default.
    expect(DEFAULT_SLOT_CARDS).not.toContain(11);
    expect(() => slotSettingName(0)).toThrow(RangeError);
    expect(() => slotSettingName(13)).toThrow(RangeError);
  });

  test('every read falls back to the default without the plugin', () => {
    expect(setting.shiftLights()).toBe('isnull([OpenDash.ShiftLights], true)');
    // Two fallbacks: the deprecated alias, and through it the default a package without the plugin
    // shows. An rc.2 plugin attaches ShiftLights and not RevBar, and its user's switch still works.
    expect(setting.revBar()).toBe("isnull([OpenDash.RevBar], if(isnull([OpenDash.ShiftLights], true), 'shift', 'rpm'))");
    expect(setting.revBarIs('off')).toBe("(isnull([OpenDash.RevBar], if(isnull([OpenDash.ShiftLights], true), 'shift', 'rpm'))) = ('off')");
    expect(REV_BAR_MODES).toEqual(['shift', 'rpm', 'off']);
    expect(DEFAULTS.RevBar).toBe('shift');
    expect(setting.positionMode()).toBe("isnull([OpenDash.PositionMode], 'overall')");
    expect(setting.deltaReference()).toBe("isnull([OpenDash.DeltaReference], 'session')");
    expect(setting.sessionProgress()).toBe("isnull([OpenDash.SessionProgress], 'auto')");
    expect(setting.slot(1)).toBe('isnull([OpenDash.Slot01], 12)');
    expect(setting.slot(7)).toBe('isnull([OpenDash.Slot07], 5)');
    // On, because movement is what a flag is read by at the edge of vision; a profile installed
    // beside no plugin at all therefore still moves.
    expect(setting.ledFlagAnimation()).toBe('isnull([OpenDash.LedFlagAnimation], true)');
    // Two fallbacks, as the rev bar has: the deprecated alias first, so that a profile beside an
    // rc.2 plugin reads the threshold that user set, and the default behind it.
    expect(flagBox.lowFuelLaps()).toBe('isnull([OpenDash.LightsLowFuelLaps], isnull([OpenDash.FlagBoxLowFuelLaps], 2))');
  });
});

/** The plugin sources mirror contract.ts; a missing file fails here rather than skipping. */
const pluginSource = (file: string): string => readFileSync(path.resolve(import.meta.dir, '../../../plugin/OpenDash', file), 'utf8');

/**
 * The settings panel as one string.
 *
 * It is four tabs across six partial classes since XOR-125, so a test that named SettingsControl.cs
 * was reading a sixth of it and went green on the strips having moved to the Lights tab. The whole
 * panel is what these assertions mean: a setting is offered somewhere a user can reach it.
 */
const panelSource = (): string =>
  readdirSync(path.resolve(import.meta.dir, '../../../plugin/OpenDash'))
    .filter((name) => name.startsWith('SettingsControl') && name.endsWith('.cs'))
    .map((name) => pluginSource(name))
    .join('\n');
const csArray = (values: readonly string[]): string => `{ ${values.map((v) => `"${v}"`).join(', ')} }`;

/** The pinned list, without its header. `declared-properties.txt` says what it is for. */
const pinnedProperties = (): string[] =>
  readFileSync(path.resolve(import.meta.dir, 'declared-properties.txt'), 'utf8')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith('#'));

describe('plugin mirror', () => {
  test('the declared list is the pinned one, which the plugin is checked against too', () => {
    // The gap this closes: each side built its own list and nothing compared them, so LedCentre and
    // LedRpmStyle could be declared here, read by every generated .ledsprofile, attached by nothing,
    // and leave both suites green. The pin is the third party the two are measured against;
    // ContractTests.cs reads the same file. A name added on one side alone fails here or there.
    //
    // In order on this side, because the file is written in the order the properties are declared and
    // a reader should be able to follow it. The C# check is by set: the two sides emit a face's
    // twenty-one names in different orders -- contract.ts groups them by zone, Contract.cs by
    // property across the zones -- which predates this test and is not settled by it.
    expect(declaredProperties()).toEqual(pinnedProperties());
    expect(new Set(pinnedProperties()).size).toBe(pinnedProperties().length);
    // Named, so that the two the plugin never attached cannot go missing again in silence.
    expect(pinnedProperties()).toContain('OpenDash.LedCentre');
    expect(pinnedProperties()).toContain('OpenDash.LedRpmStyle');
    // And the superseded name beside the one that supersedes it, both attached: a published property
    // name is a public interface, so an rc.2 rig's threshold does not vanish with the rename.
    expect(pinnedProperties()).toContain('OpenDash.LightsLowFuelLaps');
    expect(pinnedProperties()).toContain('OpenDash.FlagBoxLowFuelLaps');
    const attach = pluginSource('OpenDash.cs');
    for (const name of [LIGHTS_LOW_FUEL_LAPS_SETTING, FLAG_BOX_LOW_FUEL_LAPS_SETTING]) expect(attach).toContain(`this.AttachDelegate(Contract.${name},`);
  });

  test('Contract.cs declares the strips, with their value sets and their defaults', () => {
    // The C# list itself is checked against the pin by ContractTests.cs, which can enumerate it; what
    // is checked here is the half a regex can see, the same way the modes above are.
    const source = pluginSource('Contract.cs');
    for (const name of [LED_CENTRE_SETTING, LED_RPM_STYLE_SETTING, LED_FLAG_ANIMATION_SETTING]) expect(source).toContain(`public const string ${name} = "${name}";`);
    expect(source).toContain(`public const bool DefaultLedFlagAnimation = ${String(DEFAULTS.LedFlagAnimation)};`);
    expect(source).toContain(`LedCentres = ${csArray(LED_CENTRES)};`);
    expect(source).toContain(`LedRpmStyles = ${csArray(LED_RPM_STYLES)};`);
    expect(source).toContain(`public const string DefaultLedCentre = "${DEFAULTS.LedCentre}";`);
    expect(source).toContain(`public const string DefaultLedRpmStyle = "${DEFAULTS.LedRpmStyle}";`);
    // Four centres and the fifth named as retired, so that the plugin can migrate a stored rpmOnly
    // rather than leave a strip matching no conditional group and going dark.
    expect(LED_CENTRES).toEqual(['rpm', 'brake', 'throttleBrake', 'fuel']);
    expect(LED_CENTRES).not.toContain(RETIRED_LED_CENTRE);
    expect(source).toContain(`public const string RetiredLedCentre = "${RETIRED_LED_CENTRE}";`);
    // Attached, and offered on the panel. A property the plugin declares and never attaches is a
    // profile stuck on its isnull() default, which is exactly how these two shipped.
    const attach = pluginSource('OpenDash.cs');
    for (const name of [LED_CENTRE_SETTING, LED_RPM_STYLE_SETTING, LED_FLAG_ANIMATION_SETTING]) expect(attach).toContain(`this.AttachDelegate(Contract.${name},`);
    const panel = panelSource();
    expect(panel).toContain('Contract.LedCentres');
    expect(panel).toContain('Contract.LedRpmStyles');
  });

  test('Cards.cs lists the catalogue: number, id, label and display name, in order', () => {
    const source = pluginSource('Cards.cs');
    const cards = [...source.matchAll(/new Card\((\d+), "([^"]*)", "([^"]*)", "([^"]*)", "[^"]*"\)/g)].map((m) => ({
      number: Number(m[1]),
      id: m[2],
      label: m[3],
      displayName: m[4],
    }));
    expect(cards).toEqual(CARD_CATALOGUE.map(({ number, id, label, displayName }) => ({ number, id, label, displayName })));
    expect(source).toContain(`public const int Count = ${CARD_CATALOGUE.length};`);
  });

  test('Contract.cs carries the prefix, the slot count, the property names, the value sets and the defaults', () => {
    const source = pluginSource('Contract.cs');
    expect(source).toContain(`public const string Prefix = "${PROPERTY_PREFIX}";`);
    expect(source).toContain(`public const int SlotCount = ${SLOT_MAX};`);
    for (const name of ['ShiftLights', 'PositionMode', 'DeltaReference', 'SessionProgress', 'RevBar']) expect(source).toContain(`public const string ${name} = "${name}";`);
    expect(source).toContain(`RevBarModes = ${csArray(REV_BAR_MODES)};`);
    // The plugin names each mode, because it compares against them in four places; the mirror is
    // that the names spell the modes this file declares and that the default points at one of them.
    const revBarConst = (mode: string): string => `RevBar${mode[0]!.toUpperCase()}${mode.slice(1)}`;
    for (const mode of REV_BAR_MODES) expect(source).toContain(`public const string ${revBarConst(mode)} = "${mode}";`);
    expect(source).toContain(`public const string DefaultRevBar = ${revBarConst(DEFAULTS.RevBar)};`);
    expect(source).toContain(`FlagFormats = ${csArray(FLAG_FORMATS)};`);
    expect(source).toContain(`public const string DefaultFlagFormat = "${DEFAULT_FLAG_FORMAT}";`);
    // And offered on the panel, which is the half of a setting that makes it one. The format was
    // declared, mirrored and attached with nothing in the panel writing it, so the only way to draw
    // a flag over the body was to hand-edit the settings file.
    expect(panelSource()).toContain('Contract.FlagFormats');
    // The blue flag detail is shared, so it sits beside the four modes rather than beside the flag
    // format. There is no panel assertion under it: the row that writes it belongs on the Data tab,
    // which is the tab for settings that mean the same thing on every screen, and until it is there
    // the setting sits at its default and the band draws what it has always drawn.
    expect(source).toContain(`public const string ${BLUE_FLAG_DETAIL_SETTING} = "${BLUE_FLAG_DETAIL_SETTING}";`);
    expect(source).toContain(`BlueFlagDetails = ${csArray(BLUE_FLAG_DETAILS)};`);
    expect(source).toContain(`public const string DefaultBlueFlagDetail = "${DEFAULTS.BlueFlagDetail}";`);
    expect(pluginSource('OpenDash.cs')).toContain(`this.AttachDelegate(Contract.${BLUE_FLAG_DETAIL_SETTING},`);
    expect(source).toContain(`PositionModes = ${csArray(POSITION_MODES)};`);
    expect(source).toContain(`DeltaReferences = ${csArray(DELTA_REFERENCES)};`);
    expect(source).toContain(`SessionProgressModes = ${csArray(SESSION_PROGRESS_MODES)};`);
    expect(source).toContain(`DefaultShiftLights = ${String(DEFAULTS.ShiftLights)};`);
    expect(source).toContain(`DefaultPositionMode = "${DEFAULTS.PositionMode}";`);
    expect(source).toContain(`DefaultDeltaReference = "${DEFAULTS.DeltaReference}";`);
    expect(source).toContain(`DefaultSessionProgress = "${DEFAULTS.SessionProgress}";`);
  });
});


describe('the second screens', () => {
  test('21 modules, numbered in page order, three of them off', () => {
    expect(MODULE_CATALOGUE).toHaveLength(21);
    expect(MODULE_COUNT).toBe(21);
    MODULE_CATALOGUE.forEach((m, i) => expect(m.number).toBe(i + 1));
    expect(new Set(MODULE_CATALOGUE.map((m) => m.id)).size).toBe(21);
    // Virtual energy, damage and segment rivals: the three iRacing cannot fill.
    expect(MODULE_CATALOGUE.filter((m) => !m.enabled).map((m) => m.id)).toEqual(['energy', 'damage', 'trackRivals']);
    // Module 17 is the gear alone, as the hero is: a module shows one thing.
    expect(moduleAt(17)).toMatchObject({ id: 'gear', name: 'Gear' });
    expect(moduleMeta('gear').number).toBe(17);
    expect(() => moduleAt(0)).toThrow(RangeError);
    expect(() => moduleAt(22)).toThrow(RangeError);
    expect(() => moduleMeta('nope')).toThrow();
  });

  test('module settings are two-digit and zero-padded', () => {
    expect(moduleSettingName(1)).toBe('CompanionModule01');
    expect(moduleSettingName(21)).toBe('CompanionModule21');
    expect(pitWallZoneSettingName('A')).toBe('PitWallZoneA');
  });

  test('every zone page names a module, save the web view', () => {
    expect(PIT_WALL_ZONE_PAGES).toHaveLength(11);
    expect(PIT_WALL_WIDE_ZONE_PAGES).toHaveLength(6);
    PIT_WALL_ZONE_PAGES.forEach((p, i) => expect(p.number).toBe(i));
    PIT_WALL_WIDE_ZONE_PAGES.forEach((p, i) => expect(p.number).toBe(i));
    const ids = new Set(MODULE_CATALOGUE.map((m) => m.id));
    for (const page of PIT_WALL_ZONE_PAGES) expect(page.id === 'web' || ids.has(page.id)).toBe(true);
    // The wide car-telemetry page is the one that is not a module: a trace beside the settings grid.
    for (const page of PIT_WALL_WIDE_ZONE_PAGES) expect(page.id === 'web' || page.id === 'carTelemetry' || ids.has(page.id)).toBe(true);
  });

  test('the defaults name real pages', () => {
    for (const letter of PIT_WALL_ZONE_LETTERS) expect(PIT_WALL_ZONE_PAGES[PIT_WALL_DEFAULT_ZONE_PAGES[letter]]).toBeDefined();
    expect(PIT_WALL_ZONE_PAGES[PIT_WALL_DEFAULT_ZONE_PAGES.A]?.id).toBe('fuel');
    expect(PIT_WALL_ZONE_PAGES[PIT_WALL_DEFAULT_ZONE_PAGES.C]?.id).toBe('relative');
    expect(PIT_WALL_WIDE_ZONE_PAGES[PIT_WALL_DEFAULT_WIDE_ZONE_PAGE]?.id).toBe('carTelemetry');
  });

  test('a module reads as a number, so SimHub can treat it as a screen switch', () => {
    // SimHub enables a screen when its expression evaluates above zero; a boolean converts to 1.
    expect(secondScreen.moduleEnabled(1)).toBe('isnull([OpenDash.CompanionModule01], 1)');
    expect(secondScreen.moduleEnabled(6)).toBe('isnull([OpenDash.CompanionModule06], 0)');
    expect(secondScreen.zonePage('C')).toBe('isnull([OpenDash.PitWallZoneC], 4)');
    expect(secondScreen.wideZonePage()).toBe('isnull([OpenDash.PitWallWide], 5)');
    expect(secondScreen.webViewUrl()).toBe("isnull([OpenDash.WebViewUrl], '')");
  });
});
