/**
 * The pop-up: its anatomy, the one part of it that may wear a colour, the rule that only one shows,
 * and what it is allowed to cover.
 *
 * The anatomy figures are literal here for the reason the zone rectangles are literal in
 * `zoneFace.test.ts`: they are read off the pagesandalerts artboard, and deriving them would be a
 * second opinion about the design rather than a check on the code. Where tokens.json carries the
 * same figure the literal is held against the token instead, so the two cannot drift.
 *
 * What a pop-up covers is the hero, which on a zone face is zone A, and 560 px is wider than any
 * zone A the artboards draw, so a landscape face has a pop-up reaching into the inner edge of zones
 * B and C. That is what "centred over the hero" means on this face. What it may not touch is
 * anything carrying a state of its own: the rev bar in its well, the bar of settled values, the
 * limiter banner and band D, where a flag has the better claim on the same sixty pixels.
 */
import { describe, expect, test } from 'bun:test';
import { ncalc } from '../src/generator.ts';
import type { Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';
import {
  DRS_POP_UP,
  FUEL_POP_UP,
  LAP_POP_UP,
  POP_UPS,
  POP_UP_GAP,
  POP_UP_HEIGHT,
  POP_UP_PAD_X,
  POP_UP_RULE,
  POP_UP_SECONDARY_SIZE,
  POP_UP_SECONDS,
  POP_UP_VALUE_SIZE,
  POP_UP_WIDTH,
  popUp,
  popUpFit,
  popUpFrame,
  popUpVisible,
  popUps,
  type PopUpSpec,
} from '../src/components/popUp.ts';
import { FLAG_BLINK_MS } from '../src/components/flagStrip.ts';
import { SIDE_EFFECTS } from '../src/leds/effects.ts';
import { measureText } from '../src/design/advances.ts';
import { centre, contains, overlaps, rect, type Rect } from '../src/design/geometry.ts';
import { CANVAS_BASELINE, LINE_SPACING, WPF_BASELINE } from '../src/design/metrics.ts';
import { resolveToken } from '../src/tokens.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, type ZoneLayout } from '../src/zones/index.ts';
import { faceOf } from './monoGlyphs.ts';

const { not } = ncalc;

/** A token read back as what it is, the way `tokens.test.ts` reads one. */
const hexToken = (path: string): `#${string}` => resolveToken(path) as `#${string}`;
const numberToken = (path: string): number => resolveToken(path) as number;

/** A pop-up drawn somewhere with room around it, for the tests that are about the box and not the face. */
const FRAME = rect(100, 100, POP_UP_WIDTH, POP_UP_HEIGHT);
const drawn = (spec: PopUpSpec): Item[] => [...walkItems([popUp(FRAME, spec)])];
const named = (spec: PopUpSpec, part: string): Item | undefined => drawn(spec).find((i) => i.name === `popUp.${spec.id}.${part}`);
const textNamed = (spec: PopUpSpec, part: string): TextItem => {
  const item = named(spec, part);
  if (!item || item.kind !== 'text') throw new Error(`popUp.${spec.id}.${part} is not a text item`);
  return item;
};
const rectNamed = (spec: PopUpSpec, part: string): RectangleItem => {
  const item = named(spec, part);
  if (!item || item.kind !== 'rect') throw new Error(`popUp.${spec.id}.${part} is not a rect`);
  return item;
};

/** Width of what an item draws: its cells when monospaced, its measured advances otherwise. */
function drawnWidth(item: TextItem): number {
  const text = item.widest ?? item.text;
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), text, item.fontSize);
  const specials = [...text].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (text.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}

describe('the box is the one the artboard draws', () => {
  test('560 by 120, and the height is the token', () => {
    expect([POP_UP_WIDTH, POP_UP_HEIGHT]).toEqual([560, 120]);
    expect(POP_UP_HEIGHT).toBe(numberToken('indicator.popUp.height'));
    const box = rectNamed(LAP_POP_UP, 'box');
    expect(box.rect).toEqual(FRAME);
  });

  test('the fill and the top rule are the pop-up tokens, reached through the door ds exposes', () => {
    expect(rectNamed(LAP_POP_UP, 'box').backgroundColor).toBe(hexToken('purpose.popUp.surface'));
    const rule = rectNamed(LAP_POP_UP, 'rule');
    expect(rule.backgroundColor).toBe(hexToken('purpose.popUp.rule'));
    expect(rule.rect).toEqual(rect(FRAME.left, FRAME.top, FRAME.width, POP_UP_RULE));
    expect(POP_UP_RULE).toBe(2);
  });

  test('32 px of side padding, a 15 px label 4 px above a 64 px value, a 46 px secondary at the right end', () => {
    expect([POP_UP_PAD_X, POP_UP_GAP, POP_UP_VALUE_SIZE, POP_UP_SECONDARY_SIZE]).toEqual([32, 4, 64, 46]);
    const label = textNamed(LAP_POP_UP, 'label');
    const value = textNamed(LAP_POP_UP, 'value');
    const secondary = textNamed(LAP_POP_UP, 'secondary');
    expect(label.rect.left).toBe(FRAME.left + POP_UP_PAD_X);
    expect(value.rect.left).toBe(FRAME.left + POP_UP_PAD_X);
    expect([label.fontSize, value.fontSize, secondary.fontSize]).toEqual([15, 64, 46]);
    expect([label.font, label.fontWeight]).toEqual([ds.font.label, 'Medium']);
    expect([value.font, value.fontWeight]).toEqual([ds.font.data, 'SemiBold']);
    // The label's line box and the value's, 4 px apart, which is what the canvas stacks rather than
    // the readout's 5. Both items are drawn from their WPF box, so the gap is read back off the
    // canvas line boxes the two boxes reproduce.
    const canvasY = (item: TextItem): number => item.rect.top + (LINE_SPACING - WPF_BASELINE - (1 - CANVAS_BASELINE)) * item.fontSize;
    expect(Math.round(canvasY(value) - canvasY(label) - label.fontSize)).toBe(POP_UP_GAP);
    expect(secondary.rect.left + secondary.rect.width).toBe(FRAME.left + FRAME.width - POP_UP_PAD_X);
    expect(secondary.hAlign).toBe('right');
  });

  test('the whole anatomy stays inside the box', () => {
    for (const spec of POP_UPS) {
      for (const item of drawn(spec)) {
        if (item.kind === 'layer') continue;
        expect({ item: item.name, rect: item.rect, inside: contains(FRAME, item.rect) }).toMatchObject({ inside: true });
      }
    }
  });
});

describe('only the value wears the purpose colour', () => {
  /** The neutral tokens the rest of a pop-up is allowed to be drawn in. */
  const NEUTRAL: string[] = [ds.color.surface.zone, ds.color.text.primary, ds.color.text.label, ds.color.text.secondary];

  for (const spec of POP_UPS) {
    test(`${spec.id} colours its value and nothing else`, () => {
      expect(textNamed(spec, 'value').textColor).toBe(spec.colour);
      for (const item of drawn(spec)) {
        if (item.name.endsWith('.value')) continue;
        const colour = item.kind === 'text' ? item.textColor : item.kind === 'rect' ? item.backgroundColor : undefined;
        if (colour === undefined) continue;
        expect({ item: item.name, colour, neutral: NEUTRAL.includes(colour) }).toMatchObject({ neutral: true });
      }
    });

    test(`${spec.id} binds no colour anywhere, so nothing can change its dress at runtime`, () => {
      for (const item of drawn(spec)) expect({ item: item.name, bound: item.bindings?.TextColor ?? null }).toMatchObject({ bound: null });
    });
  }
});

describe('one pop-up at a time', () => {
  test('each is visible only when no higher one is', () => {
    for (const [index, spec] of POP_UPS.entries()) {
      const visible = popUpVisible(spec.id);
      expect(visible).toContain(spec.when);
      for (const higher of POP_UPS.slice(0, index)) expect(visible).toContain(not(higher.when));
      for (const lower of POP_UPS.slice(index + 1)) expect(visible).not.toContain(not(lower.when));
    }
  });

  /**
   * The chain evaluated rather than read: every condition stood up as a boolean, the `and` and the
   * `!` NCalc writes turned into JavaScript's, and the result asked which pop-ups would be drawn.
   * The conditions are substituted longest first, so one that is a substring of another cannot take
   * its place.
   */
  const shownWhen = (live: Record<string, boolean>): string[] => {
    const order = [...POP_UPS].sort((a, b) => b.when.length - a.when.length);
    return POP_UPS.filter((spec) => {
      let expression = popUpVisible(spec.id);
      for (const other of order) expression = expression.split(other.when).join(String(live[other.id]));
      return Boolean(new Function(`return ${expression.replace(/ and /g, ' && ')};`)());
    }).map((spec) => spec.id);
  };

  test('no two are ever out together, and the one that is out is the highest whose condition holds', () => {
    for (let mask = 0; mask < 1 << POP_UPS.length; mask++) {
      const live = Object.fromEntries(POP_UPS.map((spec, i) => [spec.id, (mask & (1 << i)) !== 0]));
      const shown = shownWhen(live);
      const expected = POP_UPS.filter((spec) => live[spec.id]).map((spec) => spec.id).slice(0, 1);
      expect({ live, shown }).toMatchObject({ live, shown: expected });
    }
  });

  test('the lap time leads, because it is the only one whose condition lasts seconds', () => {
    expect(POP_UPS.map((spec) => spec.id)).toEqual(['lap', 'fuel', 'drs']);
  });

  test('a pop-up that is not out costs nothing, its bindings being behind one Visible', () => {
    for (const spec of POP_UPS) {
      const layer = popUp(FRAME, spec);
      expect(layer.kind).toBe('layer');
      expect(layer.bindings?.Visible?.formula).toBe(popUpVisible(spec.id));
      for (const child of layer.children) expect({ child: child.name, visible: child.bindings?.Visible ?? null }).toMatchObject({ visible: null });
    }
  });
});

describe('nothing a pop-up draws is wider than the box it is drawn in', () => {
  const room = POP_UP_WIDTH - 2 * POP_UP_PAD_X;

  for (const spec of POP_UPS) {
    test(`${spec.id} measures every run from its widest rendering`, () => {
      for (const item of drawn(spec)) {
        if (item.kind !== 'text') continue;
        const width = drawnWidth(item);
        expect({ item: item.name, drawn: item.widest ?? item.text, width, box: item.rect.width, fits: width <= item.rect.width }).toMatchObject({ fits: true });
      }
      const value = textNamed(spec, 'value');
      expect({ id: spec.id, value: drawnWidth(value), room, fits: drawnWidth(value) <= room }).toMatchObject({ fits: true });
    });

    test(`${spec.id} draws its binding rather than its sample`, () => {
      const value = spec.value;
      if (value.bind) expect(value.chars ?? value.widest).toBeDefined();
      if (spec.secondary?.bind) expect(spec.secondary.chars ?? spec.secondary.widest).toBeDefined();
    });
  }

  test('the secondary is dropped before the value is shrunk, which is the order fitFields takes', () => {
    const wide = { sample: '0', widest: 'W'.repeat(20) };
    const shed: PopUpSpec = { ...LAP_POP_UP, value: { sample: '1:42.905', chars: LAP_POP_UP.value.chars }, secondary: wide };
    expect(popUpFit(shed)).toEqual({ valueFs: POP_UP_VALUE_SIZE, secondary: false });
    const shrunk: PopUpSpec = { ...LAP_POP_UP, value: wide, secondary: undefined };
    const fit = popUpFit(shrunk);
    expect(fit.valueFs).toBeLessThan(POP_UP_VALUE_SIZE);
    expect(Math.ceil(measureText('BarlowCondensedSemiBold', wide.widest, fit.valueFs))).toBeLessThanOrEqual(room);
  });

  test('a pop-up that fits keeps both runs at full size', () => {
    expect(popUpFit(LAP_POP_UP)).toEqual({ valueFs: POP_UP_VALUE_SIZE, secondary: true });
    expect(popUpFit(FUEL_POP_UP)).toEqual({ valueFs: POP_UP_VALUE_SIZE, secondary: false });
  });
});

describe('the box covers the hero and nothing that carries a state of its own', () => {
  /** Both arrangements of every face: the one with the rev bar and the one that gives its room back. */
  const arrangements: { name: string; layout: ZoneLayout; items: Item[] }[] = ZONE_FACES.flatMap((face) => [
    { name: face.folder, layout: face, items: faceItems(face) },
    { name: `${face.folder}, rev bar off`, layout: layoutWithoutRevBar(face), items: faceItems(layoutWithoutRevBar(face), { revBar: false }) },
  ]);

  for (const { name, layout, items } of arrangements) {
    test(`${name} centres the box on zone A`, () => {
      const frame = popUpFrame(layout.zones.zoneA);
      expect(frame.width).toBe(POP_UP_WIDTH);
      expect(frame.height).toBe(POP_UP_HEIGHT);
      // Within the half pixel a whole-pixel rect costs on a zone of odd height.
      const zone = centre(layout.zones.zoneA);
      const off = centre(frame);
      expect({ name, dx: Math.abs(off.x - zone.x) <= 0.5, dy: Math.abs(off.y - zone.y) <= 0.5 }).toMatchObject({ name, dx: true, dy: true });
      const box = items.flatMap((i) => [...walkItems([i])]).find((i) => i.name === 'popUp.lap.box');
      expect(box?.kind === 'rect' ? box.rect : undefined).toEqual(frame);
    });

    test(`${name} draws every pop-up inside the face and clear of the settled parts`, () => {
      const z = layout.zones;
      const frame = popUpFrame(z.zoneA);
      const face: Rect = rect(0, 0, layout.width, layout.height);
      expect({ name, frame, inside: contains(face, frame) }).toMatchObject({ inside: true });
      const untouchable: [string, Rect | undefined][] = [
        ['the rev bar well', name.endsWith('rev bar off') ? undefined : z.revBarWell],
        ['the bar', z.bar],
        ['the limiter banner', z.pitLimiter],
        ['band D', z.band],
      ];
      for (const [what, box] of untouchable) {
        if (!box) continue;
        expect({ name, what, covered: overlaps(frame, box) }).toMatchObject({ covered: false });
      }
      for (const item of items.flatMap((i) => [...walkItems([i])])) {
        if (item.kind === 'layer' || !item.name.startsWith('popUp.')) continue;
        expect({ item: item.name, rect: item.rect, inside: contains(frame, item.rect) }).toMatchObject({ inside: true });
      }
    });
  }

  test('every face draws the whole catalogue, each behind its own Visible', () => {
    for (const face of ZONE_FACES) {
      const layers = faceItems(face).filter((i): i is LayerItem => i.kind === 'layer' && i.name.startsWith('popUp.'));
      expect({ face: face.folder, ids: layers.map((l) => l.name) }).toMatchObject({ face: face.folder, ids: POP_UPS.map((s) => `popUp.${s.id}`) });
    }
    expect(popUps(rect(0, 0, 800, 400))).toHaveLength(POP_UPS.length);
  });
});

describe('the three conditions, each a state or arithmetic over a published property', () => {
  test('the lap time is out for the three seconds after the line, read off the lap and not off a clock', () => {
    expect(POP_UP_SECONDS * 1000).toBe(numberToken('indicator.popUp.durationMs'));
    expect(LAP_POP_UP.when).toContain('DataCorePlugin.GameData.CurrentLapTime');
    expect(LAP_POP_UP.when).toContain(`< (${POP_UP_SECONDS})`);
    // And it waits for a lap to have been set, or the out lap opens on the no-data glyph.
    expect(LAP_POP_UP.when).toContain('DataCorePlugin.GameData.LastLapTime');
    expect(LAP_POP_UP.when).not.toContain('blink(');
  });

  test('the lap time is the label Lap over the last lap, with the delta beside it', () => {
    expect(textNamed(LAP_POP_UP, 'label').text).toBe('LAP');
    expect(textNamed(LAP_POP_UP, 'value').textColor).toBe(ds.color.text.primary);
    expect(textNamed(LAP_POP_UP, 'value').bindings?.Text?.formula).toContain('DataCorePlugin.GameData.LastLapTime');
    expect(textNamed(LAP_POP_UP, 'secondary').textColor).toBe(ds.color.text.secondary);
    expect(textNamed(LAP_POP_UP, 'secondary').bindings?.Text?.formula).toContain('LiveDeltaSeconds');
  });

  test('low fuel reads the threshold the driver set rather than a second number', () => {
    expect(FUEL_POP_UP.when).toContain('DataCorePlugin.Computed.Fuel_RemainingLaps');
    expect(FUEL_POP_UP.when).toContain('OpenDash.LightsLowFuelLaps');
    expect(FUEL_POP_UP.when).toContain('OpenDash.FlagBoxLowFuelLaps');
    expect(textNamed(FUEL_POP_UP, 'label').text).toBe('FUEL');
    expect(textNamed(FUEL_POP_UP, 'value').textColor).toBe('#FF2D46');
  });

  test('the low-fuel value flashes at the rate the flag band defines, and the box under it does not', () => {
    expect(textNamed(FUEL_POP_UP, 'value').blink).toEqual({ enabled: true, delayMs: FLAG_BLINK_MS });
    expect(rectNamed(FUEL_POP_UP, 'box').blink).toBeUndefined();
    expect(popUp(FRAME, FUEL_POP_UP).blink).toBeUndefined();
  });

  test('DRS says the word the artboard writes, in green, on part of what lights the strip', () => {
    expect(textNamed(DRS_POP_UP, 'label').text).toBe('DRS');
    expect(textNamed(DRS_POP_UP, 'value').text).toBe('Available');
    expect(textNamed(DRS_POP_UP, 'value').textColor).toBe('#00D96A');
    const effect = SIDE_EFFECTS.find((e) => e.id === 'drs');
    expect(effect?.when).toContain(DRS_POP_UP.when);
  });
});
