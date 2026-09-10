/**
 * design/tokens.json in code. Resolves `{a.b.c}` aliases recursively and exposes `ds`, the typed
 * subset the dash face needs. Every tokens.json path `ds` reads is recorded in DS_TOKEN_PATHS so
 * a test can prove each one exists and every colour is `#RRGGBB`. Nothing here reads
 * `color.brand.*`: the brand colour never appears on the face.
 */
import tokensJson from '../../../design/tokens.json';
import type { Hex } from './generator.ts';

type Tree = Record<string, unknown>;
const tree = tokensJson as unknown as Tree;
const ALIAS = /^\{([A-Za-z0-9_.$-]+)\}$/;
const HEX6 = /^#[0-9A-F]{6}$/;

const isObject = (v: unknown): v is Tree => v !== null && typeof v === 'object';

/** SimHub's transparent colour. Not a token: it is the absence of one. */
export const TRANSPARENT: Hex = '#00FFFFFF';

/** The raw node at a dotted path, or undefined when the path does not exist. */
export function tokenNode(path: string): unknown {
  let node: unknown = tree;
  for (const segment of path.split('.')) {
    if (!isObject(node) || !(segment in node)) return undefined;
    node = node[segment];
  }
  return node;
}

/** The leaf value at a path, following `{alias}` strings until a literal is reached. */
export function resolveToken(path: string, trail: string[] = []): string | number | boolean {
  if (trail.includes(path)) throw new Error(`tokens: alias cycle ${[...trail, path].join(' -> ')}`);
  const node = tokenNode(path);
  if (node === undefined) throw new Error(`tokens: nothing at ${path}`);
  const value = isObject(node) && 'value' in node ? node.value : node;
  if (typeof value === 'string') {
    const alias = ALIAS.exec(value);
    return alias ? resolveToken(alias[1] ?? '', [...trail, path]) : value;
  }
  if (typeof value === 'number' || typeof value === 'boolean') return value;
  throw new Error(`tokens: ${path} is a group, not a token`);
}

export function tokenExists(path: string): boolean {
  try {
    resolveToken(path);
    return true;
  } catch {
    return false;
  }
}

const used: string[] = [];
/** Every tokens.json path `ds` reads, in the order it reads them. */
export const DS_TOKEN_PATHS: readonly string[] = used;

function hex(path: string): Hex {
  used.push(path);
  const v = resolveToken(path);
  if (typeof v !== 'string' || !HEX6.test(v)) throw new Error(`tokens: ${path} is not a #RRGGBB colour (${String(v)})`);
  return v as Hex;
}

function num(path: string): number {
  used.push(path);
  const v = resolveToken(path);
  if (typeof v !== 'number') throw new Error(`tokens: ${path} is not a number (${String(v)})`);
  return v;
}

function text(path: string): string {
  used.push(path);
  const v = resolveToken(path);
  if (typeof v !== 'string') throw new Error(`tokens: ${path} is not a string (${String(v)})`);
  return v;
}

/** "12px 16px" as [top and bottom, left and right]. */
function padding(path: string): readonly [number, number] {
  const parts = text(path)
    .split(/\s+/)
    .map((p) => Number.parseInt(p, 10));
  const y = parts[0];
  const x = parts[1] ?? y;
  if (y === undefined || Number.isNaN(y) || x === undefined || Number.isNaN(x)) throw new Error(`tokens: ${path} is not a padding`);
  return [y, x];
}

const rung = (name: 'L' | 'M' | 'S') => ({
  minSlotWidth: num(`card.rung.${name}.minSlotWidth`),
  value: num(`card.rung.${name}.value`),
  denominator: num(`card.rung.${name}.denominator`),
  grid: num(`card.rung.${name}.grid`),
});

/** The design system as the dash reads it. Colours are `#RRGGBB`; the serialiser adds the alpha. */
export const ds = {
  color: {
    surface: {
      base: hex('color.surface.base'),
      zone: hex('color.surface.zone'),
      raised: hex('color.surface.raised'),
      inset: hex('color.surface.inset'),
    },
    text: {
      primary: hex('color.text.primary'),
      secondary: hex('color.text.secondary'),
      label: hex('color.text.label'),
      dim: hex('color.text.dim'),
    },
  },
  purpose: {
    delta: { faster: hex('purpose.delta.faster'), slower: hex('purpose.delta.slower'), zero: hex('purpose.delta.zero') },
    lap: { sessionBest: hex('purpose.lap.sessionBest'), nominal: hex('purpose.lap.nominal'), noData: hex('purpose.lap.noData') },
    fuel: { low: hex('purpose.fuel.low'), nominal: hex('purpose.fuel.nominal') },
    assist: { off: hex('purpose.assist.off'), none: hex('purpose.assist.none') },
    tyre: {
      cold: hex('purpose.tyre.cold'),
      nominal: hex('purpose.tyre.nominal'),
      hot: hex('purpose.tyre.hot'),
      pressure: hex('purpose.tyre.pressure'),
    },
    shift: {
      stage1: hex('purpose.shift.stage1'),
      stage2: hex('purpose.shift.stage2'),
      stage3: hex('purpose.shift.stage3'),
      unlit: hex('purpose.shift.unlit'),
    },
    flag: {
      yellow: hex('purpose.flag.yellow'),
      blue: hex('purpose.flag.blue'),
      white: hex('purpose.flag.white'),
      green: hex('purpose.flag.green'),
      black: hex('purpose.flag.black'),
      chequer: hex('purpose.flag.chequer'),
      onFlag: hex('purpose.flag.onFlag'),
    },
    pitLimiter: hex('purpose.pitLimiter'),
  },
  font: {
    /** Barlow Condensed: every numeral. */
    data: text('font.family.data'),
    /** Barlow: labels and units. */
    label: text('font.family.label'),
  },
  size: {
    gear: num('font.size.gear'),
    gearSm: num('font.size.gearSm'),
    hero: num('font.size.hero'),
    lapTime: num('font.size.lapTime'),
    value: num('font.size.value'),
    valueSm: num('font.size.valueSm'),
    label: num('font.size.label'),
    labelSm: num('font.size.labelSm'),
  },
  space: {
    1: num('space.1'),
    2: num('space.2'),
    3: num('space.3'),
    4: num('space.4'),
    5: num('space.5'),
    6: num('space.6'),
    7: num('space.7'),
    8: num('space.8'),
  },
  radius: { none: num('radius.none'), seg: num('radius.seg') },
  shiftLights: {
    segments: num('shiftLights.segments'),
    height: num('shiftLights.height'),
    flashHz: num('shiftLights.flashHz'),
  },
  indicator: {
    flagBand: { height: num('indicator.flagBand.height'), flashHz: num('indicator.flagBand.flashHz') },
    pitLimiter: { height: num('indicator.pitLimiter.height') },
  },
  card: {
    rung: { L: rung('L'), M: rung('M'), S: rung('S') },
    /** [top and bottom, left and right]. S is "8px 12px" per the token's description; the canvas draws 6/10. */
    padding: { L: padding('card.padding'), M: padding('card.padding'), S: [8, 12] as const },
  },
  screen: {
    mvp: { w: num('screen.mvp.w'), h: num('screen.mvp.h'), slots: num('screen.mvp.slots'), hero: num('screen.mvp.hero') },
  },
} as const;

export type DesignSystem = typeof ds;
