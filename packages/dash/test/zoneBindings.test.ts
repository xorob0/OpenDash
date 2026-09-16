/**
 * What a zone shows, rather than where it draws.
 *
 * Every other test in this directory measures geometry: advances against a box, items inside a
 * frame, a tree of rectangles in a snapshot. None of them would have noticed XOR-83, where
 * `left()` was not a SimHub function and every class and tyre chip drew an empty block with the
 * formula well formed, the item present, the geometry correct and the suite green.
 *
 * Under the slot model that cost one card its text. Under the zone model a zone is a widget whose
 * screen index is an expression, so the same mistake costs a zone everything it shows. These are
 * the structural assertions that can be made without evaluating a binding, gathered in one file
 * so that the guarantee is auditable, together with the deliberate breakages proving each guard
 * actually bites. Asserting a computed value waits on XOR-20.
 *
 * Two things the ticket asks for cannot be asserted here and are asserted on the plugin side
 * instead, because neither ever reaches a package: the page mask, which the plugin normalises and
 * which no expression reads (ContractTests and SettingsTests), and the quick glance, which is a
 * plugin action rather than something a dashboard reads.
 */
import { describe, expect, test } from 'bun:test';
import {
  COMPANION_PREFIX,
  FACE_ZONE_LETTERS,
  PIT_WALL_PREFIX,
  PROPERTY_PREFIX,
  declaredProperties,
  facePrefix,
  foreignProperties,
  pagesForZone,
} from '../src/contract.ts';
import { packImages } from '../src/build.ts';
import { buildPackage, fontsForPackage } from '../src/dashboard.ts';
import { LAYOUTS } from '../src/layouts/index.ts';
import { buildScreenPackage, SCREEN_PACKAGES } from '../src/screens/index.ts';
import { validatePackage, type DashPackage, type WidgetItem } from '../src/generator.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, buildZoneFace, sizeOf } from '../src/zones/index.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const VALIDATE = { declaredProperties: declaredProperties(), propertyPrefix: PROPERTY_PREFIX };

/** A package and the screen whose settings it owns. A card face owns none: it reads the modes and the slots. */
interface OwnedPackage {
  pkg: DashPackage;
  screen?: string;
}

const zoneFacePackage = (face: (typeof ZONE_FACES)[number]): DashPackage => {
  const built = buildZoneFace(face, OPTS);
  return { folderName: face.folder, dashboards: [built.main, ...built.zones], fonts: fontsForPackage() };
};

/**
 * A package as the build hands it to the validator, which is after `packImages` has declared on
 * each dashboard the artwork its own items draw. A builder returns items and never sees the
 * dashboard they land on, so a package validated before that step reports every picture as missing.
 */
const composed = (pkg: DashPackage): DashPackage => {
  packImages(pkg);
  return pkg;
};

/** Every package the build produces, rebuilt for each test that breaks one. */
const everyPackage = (): OwnedPackage[] => [
  ...LAYOUTS.map((layout) => ({ pkg: composed(buildPackage(layout, { version: OPTS.version, simHubVersion: OPTS.simHubVersion, strategy: 'widget' as const })) })),
  ...ZONE_FACES.map((face) => ({ pkg: composed(zoneFacePackage(face)), screen: facePrefix(sizeOf(face)) })),
  ...SCREEN_PACKAGES.map((screen) => ({ pkg: composed(buildScreenPackage(screen, OPTS)), screen: screen.kind === 'pitwall' ? PIT_WALL_PREFIX : COMPANION_PREFIX })),
];

/** The zone path alone, which is what the assertions about zones measure. */
const packages = (): DashPackage[] => everyPackage().filter((owned) => owned.screen !== undefined).map((owned) => owned.pkg);

const widgetsOf = (pkg: DashPackage): WidgetItem[] =>
  pkg.dashboards.flatMap((d) => d.screens.flatMap((s) => [...walkItems(s.items)])).filter((i): i is WidgetItem => i.kind === 'widget');

const errorCodes = (pkg: DashPackage): string[] => validatePackage(pkg, VALIDATE).errors.map((e) => e.code);

describe('every zone selects a page it has', () => {
  test('a zone widget binds its screen index to a declared property', () => {
    for (const pkg of packages()) {
      for (const widget of widgetsOf(pkg)) {
        const formula = widget.bindings?.InitialScreenIndex?.formula;
        expect({ pkg: pkg.folderName, widget: widget.name, bound: formula !== undefined }).toMatchObject({ bound: true });
        const named = String(typeof formula === 'string' ? formula : formula?.expression).match(/\[([A-Za-z0-9_.]+)\]/g) ?? [];
        const ours = named.map((n) => n.slice(1, -1)).filter((n) => n.startsWith(`${PROPERTY_PREFIX}.`));
        expect({ pkg: pkg.folderName, widget: widget.name, ours }).not.toMatchObject({ ours: [] });
        for (const property of ours) expect(declaredProperties()).toContain(property);
      }
    }
  });

  test('a widget can only open on a screen its target dashboard has', () => {
    // The generator proves this over the whole package; here it is stated as the promise it is,
    // because the index is the value a wheel button advances and an index past the end opens
    // nothing at all.
    for (const pkg of packages()) {
      const byFile = new Map(pkg.dashboards.map((d) => [`${d.name}.djson`.toLowerCase(), d]));
      for (const widget of widgetsOf(pkg)) {
        const target = byFile.get(widget.fileName.toLowerCase());
        expect({ pkg: pkg.folderName, widget: widget.name, found: target !== undefined }).toMatchObject({ found: true });
        expect(widget.initialScreenIndex).toBeGreaterThanOrEqual(0);
        expect(widget.initialScreenIndex).toBeLessThan(target!.screens.length);
      }
    }
  });

  test('every page of every face catalogue exists as a screen to reach', () => {
    // A mask the plugin can set has to have somewhere to land. The plugin refuses a mask bit
    // beyond the catalogue (ContractTests); this is the other half, that the catalogue the plugin
    // counts and the screens the package carries are the same number.
    const faces = ZONE_FACES.map((face) => buildZoneFace(face, OPTS));
    for (const built of faces) {
      for (const zone of FACE_ZONE_LETTERS) {
        const widget = [...walkItems(built.main.screens[0]!.items)].find((i): i is WidgetItem => i.kind === 'widget' && i.name === `zone${zone}`);
        if (!widget) continue;
        const target = built.zones.find((d) => `${d.name}.djson` === widget.fileName)!;
        expect({ zone, screens: target.screens.length }).toMatchObject({ screens: pagesForZone(zone).length });
      }
    }
  });
});

describe('the guards bite', () => {
  test('a property nobody declared fails the build, wherever it is read', () => {
    const pkg = packages()[0]!;
    const widget = widgetsOf(pkg)[0]!;
    widget.bindings = { ...widget.bindings, InitialScreenIndex: { mode: 'formula', formula: 'isnull([OpenDash.ZoneZ], 0)' } };
    expect(errorCodes(pkg)).toContain('property/undeclared');
  });

  test('a screen index past the end of its catalogue fails the build', () => {
    const pkg = packages()[0]!;
    const widget = widgetsOf(pkg)[0]!;
    widget.initialScreenIndex = 99;
    expect(errorCodes(pkg)).toContain('widget/screen-index');
  });

  test('a function SimHub does not dispatch fails the build, which is the XOR-83 failure', () => {
    const pkg = packages()[0]!;
    const widget = widgetsOf(pkg)[0]!;
    widget.bindings = { ...widget.bindings, InitialScreenIndex: { mode: 'formula', formula: 'nosuchfunction([OpenDash.ZoneA])' } };
    expect(errorCodes(pkg)).toContain('expression/unknown-function');
  });

  test('a real function called with the wrong number of arguments fails the build', () => {
    // The half that is easy to miss: SimHub matches on name and parameter count together and
    // attaches no delegate when either is wrong, so `left(x, 4)` is as dead as a typo.
    const pkg = packages()[0]!;
    const screen = pkg.dashboards[0]!.screens[0]!;
    screen.enabledExpression = "left([DataCorePlugin.GameData.CarModel], 4) = 'x'";
    expect(errorCodes(pkg)).toContain('expression/arity');
  });

  test('a face reading the screen beside it fails the build, though the name is declared', () => {
    // The one a declared-property check cannot catch: every face's group is declared, so reading a
    // neighbour's validates cleanly and then moves when somebody configures the other screen.
    const face = ZONE_FACES[0]!;
    const pkg = zoneFacePackage(face);
    const widget = widgetsOf(pkg)[0]!;
    const other = sizeOf(ZONE_FACES[3]!);
    widget.bindings = { ...widget.bindings, InitialScreenIndex: { mode: 'formula', formula: `isnull([${PROPERTY_PREFIX}.${facePrefix(other)}ZoneA], 0)` } };

    const codes = validatePackage(pkg, { ...VALIDATE, foreignProperties: foreignProperties(facePrefix(sizeOf(face))) }).errors.map((e) => e.code);
    expect(codes).toContain('property/another-screens');
    // And it is not merely undeclared: the name exists, which is what makes the rule necessary.
    expect(codes).not.toContain('property/undeclared');
  });

  test('and a second screen reading another screen fails it the same way', () => {
    // The rule is about screens and not about faces. A companion is one screen of a rig like any
    // other, and the pit wall beside it is configured by somebody who is not driving.
    const companion = SCREEN_PACKAGES.find((s) => s.kind === 'companion')!;
    const pkg = buildScreenPackage(companion, OPTS);
    // A companion page is a screen behind an enabled expression rather than a widget, so that is
    // where the reading happens and where the rule has to bite.
    const screen = pkg.dashboards[0]!.screens[0]!;
    screen.enabledExpression = `isnull([${PROPERTY_PREFIX}.PitWallZoneA], 0) > 0`;

    const codes = validatePackage(pkg, { ...VALIDATE, foreignProperties: foreignProperties(COMPANION_PREFIX) }).errors.map((e) => e.code);
    expect(codes).toContain('property/another-screens');
    expect(codes).not.toContain('property/undeclared');
  });

  test('every built package reads only its own screen', () => {
    // Every package, and not the faces alone: a card face owns no screen at all, so the whole of
    // every group is foreign to it, and the two second screens own one each.
    for (const { pkg, screen } of everyPackage()) {
      const result = validatePackage(pkg, { ...VALIDATE, foreignProperties: foreignProperties(screen) });
      expect({ folder: pkg.folderName, errors: result.errors }).toMatchObject({ errors: [] });
    }
  });

  test('and none of those codes appears in a package nobody broke', () => {
    for (const pkg of packages()) {
      expect({ pkg: pkg.folderName, codes: errorCodes(pkg) }).toMatchObject({ codes: [] });
    }
  });
});
