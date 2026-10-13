/**
 * The pop-up: its anatomy, the one part of it that may wear a colour, the rule that only one shows,
 * and what it is allowed to cover.
 *
 * The anatomy figures are literal here for the reason the zone rectangles are literal in
 * `zoneFace.test.ts`: they are read off the pagesandalerts artboard, and deriving them would be a
 * second opinion about the design rather than a check on the code. Where tokens.json carries the
 * same figure the literal is held against the token instead, so the two cannot drift.
 *
 * What a pop-up covers depends on how long it is out, which is the author's ruling on #1047. The
 * lap time at the line is out for three seconds and is drawn as a real pop-up, the sheet's 560 by
 * 120 centred on the hero and reaching over zones B and C for those seconds. Low fuel and DRS hold
 * for as long as their condition does, and are drawn on one line in the pit banner's row, where they
 * never cover the gear. What neither may touch is anything outside the body that carries a state of
 * its own, the rev bar in its well, the bar of settled values and band D, where a flag has the better
 * claim on the same sixty pixels; and the transient box keeps off the limiter banner, which the
 * lasting ones give way to by condition.
 */
import { describe, expect, test } from 'bun:test';
import { ncalc } from '../src/generator.ts';
import type { Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';
import {
  DRS_POP_UP,
  FUEL_POP_UP,
  LAP_POP_UP,
  LASTING_GAP,
  LASTING_LEAST_HEIGHT,
  LASTING_PAD_X,
  LASTING_VALUE_SIZE,
  POP_UPS,
  POP_UP_GAP,
  POP_UP_HEIGHT,
  POP_UP_PAD_X,
  POP_UP_RULE,
  POP_UP_SECONDARY_SIZE,
  POP_UP_SECONDS,
  POP_UP_VALUE_SIZE,
  POP_UP_WIDTH,
  lastingFit,
  lastingPopUpFrame,
  lastingPopUps,
  popUp,
  popUpFit,
  popUpFrame,
  popUpRoom,
  popUpVisible,
  popUpsOfKind,
  transientPopUps,
  type PopUpSpec,
} from '../src/components/popUp.ts';
import { pitAlertOut } from '../src/components/pitAlerts.ts';
import { zoneAGearInk } from '../src/zones/pages.ts';
import { FLAG_BLINK_MS } from '../src/components/flagStrip.ts';
import { SIDE_EFFECTS } from '../src/leds/effects.ts';
import { measureText } from '../src/design/advances.ts';
import { centre, contains, overlaps, rect, type Rect } from '../src/design/geometry.ts';
import { CANVAS_BASELINE, LINE_SPACING, WPF_BASELINE } from '../src/design/metrics.ts';
import { resolveToken } from '../src/tokens.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { ZONE_FACES, faceItems, layoutWithoutRevBar, zoneRegions, type ZoneLayout } from '../src/zones/index.ts';
import { regionRect } from '../src/themes/anatomy.ts';
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

describe('one pop-up of each kind at a time', () => {
  test('the lap time is the one transient pop-up, and low fuel and DRS hold while their condition does', () => {
    expect(POP_UPS.map((spec) => spec.id)).toEqual(['lap', 'fuel', 'drs']);
    expect(popUpsOfKind('transient').map((spec) => spec.id)).toEqual(['lap']);
    expect(popUpsOfKind('lasting').map((spec) => spec.id)).toEqual(['fuel', 'drs']);
  });

  test('each is visible only when no higher one of its kind is', () => {
    for (const kind of ['transient', 'lasting'] as const) {
      const specs = popUpsOfKind(kind);
      for (const [index, spec] of specs.entries()) {
        const visible = popUpVisible(spec.id);
        expect(visible).toContain(spec.when);
        for (const higher of specs.slice(0, index)) expect(visible).toContain(not(higher.when));
        for (const lower of specs.slice(index + 1)) expect(visible).not.toContain(not(lower.when));
        // The two kinds draw in different places and are not ranked against each other.
        for (const other of POP_UPS.filter((p) => p.kind !== kind)) expect(visible).not.toContain(not(other.when));
      }
    }
  });

  test('a lasting pop-up waits for the pit family, whose row it shares, and the lap time does not', () => {
    for (const spec of popUpsOfKind('lasting')) expect(popUpVisible(spec.id)).toContain(not(pitAlertOut()));
    expect(popUpVisible(LAP_POP_UP.id)).not.toContain(pitAlertOut());
  });

  /**
   * The chain evaluated rather than read: every condition stood up as a boolean, the `and` and the
   * `!` NCalc writes turned into JavaScript's, and the result asked which pop-ups would be drawn.
   * The conditions are substituted longest first, so one that is a substring of another cannot take
   * its place, and the pit family is stood up as out or not.
   */
  const shownWhen = (live: Record<string, boolean>, pit = false): string[] => {
    const order = [...POP_UPS].sort((a, b) => b.when.length - a.when.length);
    return POP_UPS.filter((spec) => {
      let expression = popUpVisible(spec.id).split(pitAlertOut()).join(String(pit));
      for (const other of order) expression = expression.split(other.when).join(String(live[other.id]));
      return Boolean(new Function(`return ${expression.replace(/ and /g, ' && ')};`)());
    }).map((spec) => spec.id);
  };

  test('no two of a kind are ever out together, the one out is the highest whose condition holds, and the pit family silences the lasting kind', () => {
    for (const pit of [false, true]) {
      for (let mask = 0; mask < 1 << POP_UPS.length; mask++) {
        const live = Object.fromEntries(POP_UPS.map((spec, i) => [spec.id, (mask & (1 << i)) !== 0]));
        const shown = shownWhen(live, pit);
        const first = (kind: 'transient' | 'lasting'): string[] => popUpsOfKind(kind).filter((spec) => live[spec.id]).map((spec) => spec.id).slice(0, 1);
        const expected = [...first('transient'), ...(pit ? [] : first('lasting'))].sort((a, b) => POP_UPS.findIndex((p) => p.id === a) - POP_UPS.findIndex((p) => p.id === b));
        expect({ live, pit, shown }).toMatchObject({ live, pit, shown: expected });
      }
    }
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
  });
});

describe('a box narrower than the artboard gives ground inside itself', () => {
  /**
   * Widths a transient box can be given, down to a box narrower than any body that ships, which is
   * the guard: every shipped body has the sheet's 560 for it today, but a theme's need not.
   */
  const WIDTHS = [600, 380, 340, 300, 277, 260, 240, 202, 162, 151, 120];
  /** The shortest room a transient box is given, the Porsche's 102 px gear at 800 x 286, and one shorter still. */
  const HEIGHTS = [POP_UP_HEIGHT, 102, 90];

  test('a 380 px box sheds the lap pop-up\'s delta and keeps the lap time at 64', () => {
    expect(popUpFit(LAP_POP_UP, rect(0, 0, 380, POP_UP_HEIGHT))).toEqual({ valueFs: POP_UP_VALUE_SIZE, secondary: false });
  });

  for (const spec of popUpsOfKind('transient')) {
    test(`${spec.id} draws nothing outside a box of any width or height a hero gives it`, () => {
      for (const width of WIDTHS) {
        for (const height of HEIGHTS) {
          const frame = rect(10, 10, width, height);
          const fit = popUpFit(spec, frame);
          for (const item of walkItems([popUp(frame, spec)])) {
            if (item.kind === 'layer') continue;
            expect({ id: spec.id, width, height, item: item.name, rect: item.rect, inside: contains(frame, item.rect) }).toMatchObject({ inside: true });
            if (item.kind !== 'text') continue;
            const drawn = drawnWidth(item);
            expect({ id: spec.id, width, item: item.name, drawn, box: item.rect.width, fits: drawn <= item.rect.width }).toMatchObject({ fits: true });
          }
          const value = [...walkItems([popUp(frame, spec)])].find((i) => i.name === `popUp.${spec.id}.value`);
          expect(value?.kind === 'text' ? value.fontSize : undefined).toBe(fit.valueFs);
        }
      }
    });
  }

  test('the value only shrinks where the box is narrower than the run, and never below 12', () => {
    for (const spec of popUpsOfKind('transient')) {
      let last = POP_UP_VALUE_SIZE;
      for (const width of WIDTHS) {
        const { valueFs } = popUpFit(spec, rect(0, 0, width, POP_UP_HEIGHT));
        expect({ id: spec.id, width, monotonic: valueFs <= last, floor: valueFs >= 12 }).toMatchObject({ monotonic: true, floor: true });
        last = valueFs;
      }
    }
  });
});

describe('the box covers the hero and nothing that carries a state of its own', () => {
  /** Both arrangements of every face: the one with the rev bar and the one that gives its room back. */
  const arrangements: { name: string; layout: ZoneLayout; items: Item[] }[] = ZONE_FACES.flatMap((face) => [
    { name: face.folder, layout: face, items: faceItems(face) },
    { name: `${face.folder}, rev bar off`, layout: layoutWithoutRevBar(face), items: faceItems(layoutWithoutRevBar(face), { revBar: false }) },
  ]);

  for (const { name, layout, items } of arrangements) {
    const body = regionRect(zoneRegions(layout), 'flagBody');
    const drawn = items.flatMap((i) => [...walkItems([i])]);

    test(`${name} draws the lap time as the sheet's box, centred on zone A and inside the body`, () => {
      const frame = popUpFrame(layout.zones.zoneA, body);
      expect([frame.width, frame.height]).toEqual([POP_UP_WIDTH, POP_UP_HEIGHT]);
      expect({ name, frame, inside: contains(body, frame) }).toMatchObject({ inside: true });
      // Within the half pixel a whole-pixel rect costs on a zone of odd height.
      const zone = centre(layout.zones.zoneA);
      const off = centre(frame);
      expect({ name, dx: Math.abs(off.x - zone.x) <= 0.5, dy: Math.abs(off.y - zone.y) <= 0.5 }).toMatchObject({ name, dx: true, dy: true });
      const box = drawn.find((i) => i.name === 'popUp.lap.box');
      expect(box?.kind === 'rect' ? box.rect : undefined).toEqual(frame);
      // On every house face zone A is the middle of the face, so the box is centred on the face too.
      expect({ name, dx: Math.abs(off.x - layout.width / 2) <= 0.5 }).toEqual({ name, dx: true });
    });

    test(`${name} keeps the lap time clear of the settled parts and the limiter banner`, () => {
      const z = layout.zones;
      const untouchable: [string, Rect | undefined][] = [
        ['the rev bar well', name.endsWith('rev bar off') ? undefined : z.revBarWell],
        ['the bar', z.bar],
        ['the limiter banner', z.pitLimiter],
        ['band D', z.band],
      ];
      for (const item of drawn) {
        if (item.kind === 'layer' || !item.name.startsWith('popUp.lap.')) continue;
        for (const [what, box] of untouchable) {
          if (!box) continue;
          expect({ name, item: item.name, what, covered: overlaps(item.rect, box) }).toMatchObject({ covered: false });
        }
      }
    });

    test(`${name} draws low fuel and DRS in the limiter banner's row, above the gear on every page of zone A`, () => {
      const row = lastingPopUpFrame(layout.zones.pitLimiter, zoneAGearInk(layout.zones.zoneA));
      expect({ name, inside: contains(layout.zones.pitLimiter, row) }).toEqual({ name, inside: true });
      for (const spec of popUpsOfKind('lasting')) {
        const box = drawn.find((i) => i.name === `popUp.${spec.id}.box`);
        expect(box?.kind === 'rect' ? box.rect : undefined).toEqual(row);
        for (const item of drawn) {
          if (item.kind === 'layer' || !item.name.startsWith(`popUp.${spec.id}.`)) continue;
          expect({ name, item: item.name, inside: contains(row, item.rect) }).toEqual({ name, item: item.name, inside: true });
          for (const ink of zoneAGearInk(layout.zones.zoneA)) expect({ name, item: item.name, ink, covered: overlaps(item.rect, ink) }).toMatchObject({ covered: false });
        }
      }
    });
  }

  /**
   * Where the gear's ink reaches into the banner's row the row ends there: the house's nano by a
   * pixel and a half, which costs its low-fuel value three pixels of size, 19 against 22.
   */
  test('the nano\'s row is cut short above its gear, and every other house face keeps the whole banner row', () => {
    for (const face of ZONE_FACES) {
      for (const layout of [face, layoutWithoutRevBar(face)]) {
        const row = lastingPopUpFrame(layout.zones.pitLimiter, zoneAGearInk(layout.zones.zoneA));
        const cut = face.folder === 'OpenDash 800x286' && layout === face;
        expect({ face: face.folder, revBar: layout === face, whole: row.height === layout.zones.pitLimiter.height }).toEqual({ face: face.folder, revBar: layout === face, whole: !cut });
      }
    }
  });

  test('the row ends where the gear\'s ink begins, and a row the gear leaves too short is refused rather than drawn over it', () => {
    const banner = rect(0, 10, 200, 30);
    expect(lastingPopUpFrame(banner, [rect(80, 60, 40, 100)])).toEqual(banner);
    expect(lastingPopUpFrame(banner, [rect(80, 36.4, 40, 100)])).toEqual(rect(0, 10, 200, 26));
    // Ink beside the row, however high, is not in it.
    expect(lastingPopUpFrame(banner, [rect(210, 0, 40, 100)])).toEqual(banner);
    expect(() => lastingPopUpFrame(banner, [rect(80, 10 + LASTING_LEAST_HEIGHT - 1, 40, 100)])).toThrow(/needs/);
    expect(() => lastingPopUpFrame(banner, [rect(80, 0, 40, 100)])).toThrow(/above/);
  });

  test('every house face centres the family on zone A itself, the banner being clear of it there', () => {
    for (const face of ZONE_FACES) {
      for (const layout of [face, layoutWithoutRevBar(face)]) {
        expect({ face: face.folder, room: popUpRoom(layout.zones.zoneA, layout.zones.pitLimiter) }).toEqual({ face: face.folder, room: layout.zones.zoneA });
      }
    }
  });

  test('a hero too short to clear the banner centres the box on what the banner leaves of it (#1052)', () => {
    // The Porsche's 600 x 686 gear tile and its banner: centred on the tile the box began at 117,
    // over the banner's last 12 px.
    const hero = rect(12, 95, 576, 164);
    const banner = rect(15, 99, 570, 30);
    expect(overlaps(popUpFrame(hero), banner)).toBe(true);
    const room = popUpRoom(hero, banner);
    expect(room).toEqual(rect(12, 129, 576, 130));
    const frame = popUpFrame(room);
    expect(frame).toEqual(rect(20, 134, 560, 120));
    expect({ inside: contains(hero, frame), covered: overlaps(frame, banner) }).toEqual({ inside: true, covered: false });
  });

  test('where the banner leaves less than the box, the box takes what it leaves, and the taller side', () => {
    const hero = rect(0, 0, 300, 140);
    // Below the banner 90 px, above it 20: the box goes below and is 90 tall.
    expect(popUpFrame(popUpRoom(hero, rect(0, 20, 300, 30)))).toEqual(rect(0, 50, 300, 90));
    // A banner at the foot of the hero puts the room above it.
    expect(popUpFrame(popUpRoom(hero, rect(0, 110, 300, 30)))).toEqual(rect(0, 0, 300, 110));
  });

  test('every face draws the whole catalogue, each behind its own Visible, the lasting kind under the transient', () => {
    const order = [...popUpsOfKind('lasting'), ...popUpsOfKind('transient')].map((s) => `popUp.${s.id}`);
    for (const face of ZONE_FACES) {
      const layers = faceItems(face).filter((i): i is LayerItem => i.kind === 'layer' && i.name.startsWith('popUp.'));
      expect({ face: face.folder, ids: layers.map((l) => l.name) }).toMatchObject({ face: face.folder, ids: order });
    }
    expect(transientPopUps(rect(0, 0, 800, 400))).toHaveLength(popUpsOfKind('transient').length);
    expect(lastingPopUps(rect(0, 0, 272, 30))).toHaveLength(popUpsOfKind('lasting').length);
  });
});

describe('a lasting pop-up is one line in the banner\'s row', () => {
  /** Every row a face that ships gives the lasting pop-ups, cut short where the gear reaches into it. */
  const ROWS = [rect(824, 111, 272, 30), rect(299, 97, 252, 30), rect(294, 39, 212, 26), rect(200, 90, 200, 28), rect(489, 92, 271, 25), rect(248, 103, 192, 30), rect(15, 99, 570, 24), rect(1120, 118, 760, 30)];

  for (const spec of popUpsOfKind('lasting')) {
    test(`${spec.id} sets its label and its value on one baseline, centred, inside every row`, () => {
      for (const row of ROWS) {
        const items = [...walkItems([popUp(row, spec)])].filter((i): i is TextItem | RectangleItem => i.kind === 'text' || i.kind === 'rect');
        for (const item of items) expect({ id: spec.id, row, item: item.name, inside: contains(row, item.rect) }).toMatchObject({ inside: true });
        const label = items.find((i): i is TextItem => i.name === `popUp.${spec.id}.label`)!;
        const value = items.find((i): i is TextItem => i.name === `popUp.${spec.id}.value`)!;
        // One baseline: WPF sets it 1 em under the top of each box.
        expect(Math.abs(label.rect.top + label.fontSize - (value.rect.top + value.fontSize))).toBeLessThanOrEqual(1);
        expect(value.rect.left - (label.rect.left + label.rect.width)).toBe(LASTING_GAP);
        expect(label.rect.left).toBeGreaterThanOrEqual(row.left + LASTING_PAD_X);
        expect(value.rect.left + value.rect.width).toBeLessThanOrEqual(row.left + row.width - LASTING_PAD_X);
        expect(value.fontSize).toBe(lastingFit(spec, row).valueFs);
        expect(value.fontSize).toBeLessThanOrEqual(LASTING_VALUE_SIZE);
        expect(drawnWidth(value)).toBeLessThanOrEqual(value.rect.width);
        expect(items.some((i) => i.name === `popUp.${spec.id}.secondary`)).toBe(false);
      }
    });
  }

  test('the value is as large as the row lets it be: 22 in the 30 px row every face but three gives it', () => {
    for (const spec of popUpsOfKind('lasting')) expect(lastingFit(spec, rect(0, 0, 272, 30)).valueFs).toBe(22);
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
    expect(textNamed(LAP_POP_UP, 'label').text).toBe('Lap');
    expect(textNamed(LAP_POP_UP, 'value').textColor).toBe(ds.color.text.primary);
    expect(textNamed(LAP_POP_UP, 'value').bindings?.Text?.formula).toContain('DataCorePlugin.GameData.LastLapTime');
    expect(textNamed(LAP_POP_UP, 'secondary').textColor).toBe(ds.color.text.secondary);
    expect(textNamed(LAP_POP_UP, 'secondary').bindings?.Text?.formula).toContain('LiveDeltaSeconds');
  });

  test('low fuel reads the threshold the driver set rather than a second number', () => {
    expect(FUEL_POP_UP.when).toContain('DataCorePlugin.Computed.Fuel_RemainingLaps');
    expect(FUEL_POP_UP.when).toContain('OpenDash.LightsLowFuelLaps');
    expect(FUEL_POP_UP.when).toContain('OpenDash.FlagBoxLowFuelLaps');
    expect(textNamed(FUEL_POP_UP, 'label').text).toBe('Fuel');
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
