/**
 * The alert band: the three shapes it is allowed to take, and the promise that only one of them is
 * ever drawn.
 *
 * Band D used to draw the six flags SimHub normalises and the 8x8 box drew all fifteen conditions
 * of `FLAG_CATALOGUE`, so a red flag, a disqualification, a furled black, a meatball, a full-course
 * caution, a waved yellow, the debris flag and the start gantry were on the box and invisible on
 * the dash. The band reads the catalogue now. What is asserted here is what that costs: that the
 * fifteen still exclude one another exactly as the box's do, that each takes one of the three
 * shapes and no fourth, and that every one of them is opaque over the whole band, since a flag
 * takes band D over precisely so that the page underneath cannot be read.
 *
 * Nothing in the repository evaluates a binding, so `visible()` below carries the same small
 * boolean evaluator `flagBox.test.ts` does, for the subset of NCalc these conditions are built
 * from. It is what makes the several-conditions-at-once table possible at all.
 */
import { describe, expect, test } from 'bun:test';
import { ALERT_BAND_BORDER, ALERT_BAND_STYLES, ALERT_FLASH_MS, type AlertBandStyle } from '../src/components/alertBand.ts';
import { flagStrip } from '../src/components/flagStrip.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { flagBit, FLAG_CATALOGUE, type SessionFlagBit } from '../src/flags.ts';
import type { Item, LayerItem, Rect, RectangleItem, TextItem } from '../src/generator.ts';
import { ds } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';

/** Band D at its widest and at the narrowest the packages draw, which is the portrait face's 600. */
const WIDE = rect(0, 420, 1920, 60);
const NARROW = rect(0, 630, 600, 56);

const layers = (frame: Rect = WIDE, style: AlertBandStyle = ALERT_BAND_STYLES.standard): LayerItem[] =>
  flagStrip(frame, style).map((item) => {
    if (item.kind !== 'layer') throw new Error(`${item.name} is not a layer`);
    return item;
  });

const layerOf = (id: string, frame: Rect = WIDE, style: AlertBandStyle = ALERT_BAND_STYLES.standard): LayerItem => {
  const found = layers(frame, style).find((l) => l.name === `flag.${id}`);
  if (!found) throw new Error(`no ${id} band`);
  return found;
};

const rects = (item: Item): RectangleItem[] => [...walkItems([item])].filter((i): i is RectangleItem => i.kind === 'rect');
const texts = (item: Item): TextItem[] => [...walkItems([item])].filter((i): i is TextItem => i.kind === 'text');

/**
 * Evaluates one band's `Visible` with the named bits set and the named normalised properties at 1.
 * Handles exactly what flags.ts emits for the band: parenthesised `and`, `or`, `!`, and
 * `isnull([prop], 0) = 1`. Anything else throws rather than guessing.
 */
function visible(layer: LayerItem, set: readonly SessionFlagBit[], normalised: readonly string[] = []): boolean {
  let s = String(layer.bindings?.Visible?.formula ?? '');
  for (const condition of FLAG_CATALOGUE) {
    for (const bit of condition.bits) s = s.split(`isnull(${flagBit(bit)}, 0)`).join(set.includes(bit) ? '1' : '0');
    if (condition.limiter) s = s.split(`isnull([DataCorePlugin.GameData.${condition.limiter}], 0)`).join(normalised.includes(condition.limiter) ? '1' : '0');
  }
  s = s.replace(/\band\b/g, '&&').replace(/\bor\b/g, '||').replace(/([^!<>=])=([^=])/g, '$1===$2');
  if (!/^[\s()!&|=01]+$/.test(s)) throw new Error(`the condition holds something this cannot evaluate: ${s}`);
  return Boolean(new Function(`return (${s});`)());
}

/** Which band draws with these bits raised, or undefined when none does. */
function shown(set: readonly SessionFlagBit[], normalised: readonly string[] = []): string | undefined {
  const drawn = layers().filter((l) => visible(l, set, normalised));
  // The assertion that matters: never two. One flag at a time is a property of the ranking rather
  // than of who draws last, because a band covers the page under it whichever order it is in.
  expect({ set: set.join(' ') || 'nothing raised', drawn: drawn.map((l) => l.name).slice(1) }).toEqual({
    set: set.join(' ') || 'nothing raised',
    drawn: [],
  });
  return drawn[0]?.name.slice('flag.'.length);
}

describe('the band is the catalogue', () => {
  test('one layer per condition, in the catalogue’s order', () => {
    expect(layers().map((l) => l.name)).toEqual(FLAG_CATALOGUE.map((c) => `flag.${c.id}`));
  });

  test('three shapes and no fourth', () => {
    expect(new Set(FLAG_CATALOGUE.map((c) => c.band.shape))).toEqual(new Set(['filled', 'outlined', 'chequer']));
  });

  test('every band names itself, except the chequer, which has no name to write', () => {
    for (const condition of FLAG_CATALOGUE) {
      const names = texts(layerOf(condition.id)).map((t) => t.text);
      expect({ id: condition.id, names }).toEqual({ id: condition.id, names: condition.band.shape === 'chequer' ? [] : [condition.band.label] });
    }
  });

  test('the nano writes no name at all, at any of the fifteen', () => {
    for (const condition of FLAG_CATALOGUE) {
      const nano = layerOf(condition.id, rect(0, 274, 800, 12), ALERT_BAND_STYLES.nano);
      expect({ id: condition.id, names: texts(nano).length }).toEqual({ id: condition.id, names: 0 });
    }
  });
});

describe('every shape is opaque over the whole band', () => {
  // The black flag was once an outline with nothing behind it, which left band D's fuel page fully
  // readable underneath the most serious thing the band can say. Whatever a band does, and whatever
  // phase a flashing one is in, it has to leave nothing of the page showing.
  for (const frame of [WIDE, NARROW]) {
    test(`at ${frame.width} x ${frame.height} each band grounds itself and stays inside band D`, () => {
      for (const condition of FLAG_CATALOGUE) {
        const layer = layerOf(condition.id, frame);
        const ground = rects(layer)[0];
        if (!ground) throw new Error(`${condition.id} draws no ground`);
        expect({ id: condition.id, name: ground.name, rect: ground.rect }).toEqual({ id: condition.id, name: `flag.${condition.id}.band`, rect: frame });
        // Opaque: `band` writes a literal colour and nothing here is allowed to be transparent.
        expect({ id: condition.id, opaque: /^#[0-9A-F]{6}$/.test(ground.backgroundColor ?? '') }).toEqual({ id: condition.id, opaque: true });
        for (const part of [...walkItems([layer])]) {
          if (part.kind === 'layer') continue;
          expect({ id: condition.id, part: part.name, inside: contains(frame, part.rect) }).toEqual({ id: condition.id, part: part.name, inside: true });
        }
      }
    });
  }

  test('an outlined band grounds itself in surface.base and wears its colour on the border and the name', () => {
    // It is the generalisation of what the black flag alone used to be: purpose.flag.black is
    // #F5F7FA, the ink rather than the ground, so a band filled with it would be the white flag.
    for (const condition of FLAG_CATALOGUE) {
      const spec = condition.band;
      if (spec.shape !== 'outlined') continue;
      const layer = layerOf(condition.id);
      const ground = rects(layer)[0]!;
      const colour = spec.colour;
      expect({ id: condition.id, ground: ground.backgroundColor }).toEqual({ id: condition.id, ground: ds.color.surface.base });
      expect({ id: condition.id, border: ground.border }).toEqual({
        id: condition.id,
        border: { color: colour, top: ALERT_BAND_BORDER, bottom: ALERT_BAND_BORDER, left: ALERT_BAND_BORDER, right: ALERT_BAND_BORDER },
      });
      expect({ id: condition.id, ink: texts(layer)[0]?.textColor }).toEqual({ id: condition.id, ink: colour });
    }
    expect(FLAG_CATALOGUE.filter((c) => c.band.shape === 'outlined').map((c) => c.id)).toEqual(['disqualify', 'furled', 'black', 'startSet', 'startReady']);
  });

  test('a filled band wears its colour on the fill and the border and writes its name in onFlag', () => {
    for (const condition of FLAG_CATALOGUE) {
      const spec = condition.band;
      if (spec.shape !== 'filled') continue;
      const layer = layerOf(condition.id);
      const ground = rects(layer)[0]!;
      const colour = spec.colour;
      expect({ id: condition.id, fill: ground.backgroundColor, border: ground.border?.color }).toEqual({ id: condition.id, fill: colour, border: colour });
      expect({ id: condition.id, ink: texts(layer)[0]?.textColor }).toEqual({ id: condition.id, ink: ds.purpose.flag.onFlag });
    }
  });

  test('the one flashing band alternates two opaque things rather than blinking itself away', () => {
    // A blinking layer draws nothing for half of every cycle and the page reads straight through
    // the flag that had taken the band over. It is the waved yellow that flashes, which is the rule
    // the box keeps under "waving is blinking"; the standing yellow is steady.
    const waved = layerOf('yellowWaving');
    expect(waved.blink).toBeUndefined();
    const flashing = [...walkItems([waved])].filter((i) => i.blink?.enabled);
    expect(flashing.map((i) => i.name)).toEqual(['flag.yellowWaving.flash']);
    expect(flashing[0]?.blink).toEqual({ enabled: true, delayMs: ALERT_FLASH_MS });
    expect((flashing[0] as RectangleItem).backgroundColor).toBe(ds.color.surface.base);
    for (const condition of FLAG_CATALOGUE.filter((c) => c.id !== 'yellowWaving')) {
      const blinking = [...walkItems([layerOf(condition.id)])].filter((i) => i.blink?.enabled);
      expect({ id: condition.id, blinking: blinking.map((i) => i.name) }).toEqual({ id: condition.id, blinking: [] });
    }
  });
});

describe('several conditions raised at once', () => {
  // iRacing raises more than one bit constantly: a caution is yellow plus caution plus
  // cautionWaving, and the last lap of a race under a black flag is two at once. The band draws
  // one, so the only question that matters is which -- and it has to be the same answer the box
  // gives, or a driver with both in front of them is told two different things.
  const cases: { name: string; bits: SessionFlagBit[]; expect: string | undefined }[] = [
    { name: 'nothing out', bits: [], expect: undefined },
    { name: 'a local yellow', bits: ['yellow'], expect: 'yellow' },
    { name: 'a waved yellow also sets yellow', bits: ['yellow', 'yellowWaving'], expect: 'yellowWaving' },
    { name: 'a full-course caution sets all three', bits: ['yellow', 'yellowWaving', 'caution', 'cautionWaving'], expect: 'caution' },
    { name: 'red outranks everything', bits: ['red', 'yellow', 'caution', 'black'], expect: 'red' },
    { name: 'a black flag on the last lap', bits: ['black', 'white'], expect: 'black' },
    { name: 'disqualified outranks the black flag it comes with', bits: ['black', 'disqualify'], expect: 'disqualify' },
    { name: 'a furled black is not a black', bits: ['furled'], expect: 'furled' },
    { name: 'a meatball while being lapped', bits: ['repair', 'blue'], expect: 'meatball' },
    { name: 'the chequer while being lapped', bits: ['checkered', 'blue'], expect: 'blue' },
    { name: 'a yellow thrown at a chequered finish', bits: ['checkered', 'yellow'], expect: 'yellow' },
    { name: 'the start gantry', bits: ['startReady'], expect: 'startReady' },
    { name: 'set outranks ready, because it is later', bits: ['startReady', 'startSet'], expect: 'startSet' },
  ];
  for (const c of cases) test(c.name, () => expect(shown(c.bits)).toBe(c.expect));

  test('exactly one band is ever drawn, whichever bits are set', () => {
    // Every pair in the catalogue, which is the case a hand-written list of examples misses.
    const bits = FLAG_CATALOGUE.flatMap((c) => c.bits);
    for (const a of bits) for (const b of bits) expect(() => shown([a, b])).not.toThrow();
  });

  test('the green is the flag SimHub limits, so band D is not green for a whole race', () => {
    // iRacing holds the `green` bit for the entire green-flag stint, where the green flag is an
    // event the canvas gives three seconds. SimHub's GreenLimiter is the only clock openDash has.
    expect(shown(['green'])).toBeUndefined();
    expect(shown(['green'], ['Flag_Green'])).toBe('green');
    // And it yields to anything above it, limiter or no limiter.
    expect(shown(['green', 'yellow'], ['Flag_Green'])).toBe('yellow');
  });
});
