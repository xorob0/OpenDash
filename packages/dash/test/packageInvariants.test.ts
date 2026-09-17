/**
 * Two rules the token layer states over every package the build composes: every colour a package
 * draws comes from design/tokens.json, and nothing carries a corner radius the design does not
 * give it.
 *
 * They are checked on the serialised `.djson` rather than on the model, because that is where a
 * colour the model never named can still appear: an item that leaves a sub-object's key unset gets
 * SimHub's own initialiser in its place, and those initialisers are literals. The radar's dot
 * border was exactly that until the module named it.
 *
 * The colour rule is written as "every colour drawn resolves to a token" and not as "the colours
 * drawn are the sheet's list". The stronger form would fail on every colour the design names and
 * the face has not reached yet, which is a question for the author rather than an invariant.
 *
 * A colour that is genuinely not a token gets a named exception below with the reason it is not
 * one, never a loosened rule; and a colour with no home in the token file is reported as a failure
 * rather than added, since design/tokens.json is the author's and is never written from code.
 */
import { describe, expect, test } from 'bun:test';
import tokensJson from '../../../design/tokens.json';
import { composePackages } from '../src/build.ts';
import { buildDashboardObject, TRANSPARENT, type JsonValue } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { itemsOf } from '../src/walk.ts';

const PACKAGES = composePackages({ version: '0.0.0-test', log: () => {} }, true);

/** `#RRGGBB` or `#AARRGGBB`, wherever it appears: a colour key, or a colour inside a formula. */
const HEX = /#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?/g;
/** The four keys SimHub writes a corner radius under. */
const RADIUS_KEY = /^Radius(?:TopLeft|TopRight|BottomLeft|BottomRight)$/;

const isObject = (v: JsonValue): v is { [k: string]: JsonValue } => v !== null && typeof v === 'object' && !Array.isArray(v);

/** Every `#RRGGBB` the token file defines. An alias is collected at the token it points at. */
const tokenColours = (): Set<string> => {
  const out = new Set<string>();
  const walk = (node: unknown): void => {
    if (typeof node === 'string') {
      if (/^#[0-9A-Fa-f]{6}$/.test(node)) out.add(node.toUpperCase());
    } else if (node !== null && typeof node === 'object') Object.values(node).forEach(walk);
  };
  walk(tokensJson);
  return out;
};

/**
 * Colours a built package draws that are not design tokens, each with the reason it is not one.
 *
 * The audit expected a country flag's own colours and the class colour a sim publishes here too.
 * Neither is drawn: the nationality column holds its place unfilled (packages/dash/src/second/table.ts)
 * and every map sets `overrideColorsWithCarClassColors` false, so neither has an entry until one is.
 */
const COLOUR_EXCEPTIONS: readonly { value: string; why: string }[] = [
  { value: TRANSPARENT, why: "SimHub's transparent: the absence of a colour rather than a colour, and the ground of every text item, widget and layer" },
];

/**
 * Items allowed a corner radius, each with the reason the design gives it one. `radius` pins the
 * value where the design fixes it; a drawing's corners are cut from its own box at every size it
 * is drawn, so those two are named by shape alone.
 */
const RADIUS_EXCEPTIONS: readonly { match: RegExp; radius?: number; why: string }[] = [
  { match: /(?:^|\.)(?:shift|rpm|simhub)\.\d+$/, radius: ds.radius.seg, why: 'radius.seg on a shift-light or rev-bar segment: the one rounded thing on the face' },
  { match: /(?:^|\.)tyres\.(?:Front|Rear)(?:Left|Right)\.(?:body|change)$/, why: "the tyre drawing's body and its change badge, cut from their box: packages/dash/src/second/wheel.ts" },
  { match: /(?:^|\.)pitView\.car\.(?:body|nose|tail|(?:Front|Rear)(?:Left|Right))$/, why: 'the car seen from above, whose tapers are rectangles because the format has no path item: packages/dash/src/second/carTopView.ts' },
];

/** Where something was found: the package, the dashboard, and the item drawing it. */
interface Site {
  package: string;
  dashboard: string;
  item: string;
  key: string;
}

const describeSite = (s: Site): string => `${s.package}/${s.dashboard}/${s.item}`;

const colours: (Site & { colour: string })[] = [];
const radii: (Site & { radius: number })[] = [];

const walk = (node: JsonValue, site: Site): void => {
  if (Array.isArray(node)) {
    for (const n of node) walk(n, site);
    return;
  }
  if (isObject(node)) {
    const here = typeof node.Name === 'string' && node.Name !== '' ? { ...site, item: node.Name } : site;
    for (const [key, value] of Object.entries(node)) {
      if (RADIUS_KEY.test(key) && typeof value === 'number' && value !== 0) radii.push({ ...here, key, radius: value });
      walk(value, { ...here, key });
    }
    return;
  }
  if (typeof node === 'string') for (const c of node.match(HEX) ?? []) colours.push({ ...site, colour: c.toUpperCase() });
};

for (const { pkg } of PACKAGES) {
  for (const dashboard of pkg.dashboards) {
    const doc = buildDashboardObject(dashboard, { packageName: pkg.folderName }) as unknown as JsonValue;
    walk(doc, { package: pkg.folderName, dashboard: dashboard.name, item: '(dashboard)', key: '' });
  }
}

const matchingRule = (item: string): (typeof RADIUS_EXCEPTIONS)[number] | undefined => RADIUS_EXCEPTIONS.find((e) => e.match.test(item));

describe('every package the build composes', () => {
  test('is composed and scanned, so none of these rules can pass on an empty list', () => {
    expect(PACKAGES.length).toBeGreaterThan(10);
    expect(colours.length).toBeGreaterThan(1000);
    expect(radii.length).toBeGreaterThan(100);
  });

  test('draws no colour that design/tokens.json does not define', () => {
    const known = tokenColours();
    expect(known.size).toBeGreaterThan(20);
    const excepted = new Set(COLOUR_EXCEPTIONS.map((e) => e.value.toUpperCase()));
    // The serialiser puts the alpha on; what has to resolve to a token is the colour under it.
    const rgb = (c: string): string => (c.length === 9 ? `#${c.slice(3)}` : c);
    const strays = colours.filter((c) => !excepted.has(c.colour) && !known.has(rgb(c.colour)));
    expect([...new Set(strays.map((c) => `${c.colour} on ${c.key} of ${describeSite(c)}`))].sort()).toEqual([]);
  });

  test('reaches no dot style default, so every map and radar names all three of its colours', () => {
    const unset: string[] = [];
    for (const { pkg } of PACKAGES) {
      for (const dashboard of pkg.dashboards) {
        for (const item of itemsOf(dashboard)) {
          if (item.kind !== 'radar' && item.kind !== 'staticMap') continue;
          for (const which of ['playerStyle', 'opponentStyle'] as const) {
            const style = item[which];
            // Unset means SimHub's own initialiser: #FFFF0000, #FF000000 or #FFFFFFFF. The rule
            // above catches the first two, since no token carries them; the third is white, which
            // is a token, so nothing but this would tell a borrowed white from a chosen one.
            for (const k of ['labelColor', 'dotColor', 'dotBorderColor'] as const) {
              if (style?.[k] === undefined) unset.push(`${which}.${k} of ${pkg.folderName}/${dashboard.name}/${item.name}`);
            }
          }
        }
      }
    }
    expect(unset.sort()).toEqual([]);
  });

  test('carries a corner radius only where the design gives one, at the value it gives', () => {
    const strays = radii.filter((r) => {
      const rule = matchingRule(r.item);
      return rule === undefined || (rule.radius !== undefined && r.radius !== rule.radius);
    });
    expect([...new Set(strays.map((r) => `${r.key} ${r.radius} of ${describeSite(r)}`))].sort()).toEqual([]);
  });

  test('rounds its shift and rev segments, and rounds them to radius.seg', () => {
    // Without this the rule above would go on passing if every segment lost its radius tomorrow,
    // which is the other half of what the design states about radius on a face.
    const segments = radii.filter((r) => RADIUS_EXCEPTIONS[0]!.match.test(r.item));
    expect(segments.length).toBeGreaterThan(100);
    expect([...new Set(segments.map((r) => r.radius))]).toEqual([ds.radius.seg]);
  });
});
