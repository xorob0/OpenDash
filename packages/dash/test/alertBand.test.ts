/**
 * The alert band: the four shapes it is allowed to take, and the promise that only one of them is
 * ever drawn.
 *
 * Band D used to draw the six flags SimHub normalises and the 8x8 box drew all fifteen conditions
 * of `FLAG_CATALOGUE`, so a red flag, a disqualification, a furled black, a meatball, a full-course
 * caution, a waved yellow, the debris flag and the start gantry were on the box and invisible on
 * the dash. The band reads the catalogue now, and since #109 the catalogue is twenty: the fifteen
 * flags and five car alerts, ranked in one list. What is asserted here is what that costs: that the
 * twenty still exclude one another, the flags exactly as the box's do, that each takes one of the
 * four shapes and no fifth, and that every one of them is opaque over the whole band, since a flag
 * takes band D over precisely so that the page underneath cannot be read.
 *
 * Nothing in the repository evaluates a binding, so `visible()` below carries the same small
 * boolean evaluator `flagBox.test.ts` does, for the subset of NCalc these conditions are built
 * from, and stands a car alert's whole condition in for one truth value, as it does a bit. What each
 * of those conditions says is held in `alertCatalogue.test.ts`; here it is only raised or not. It is
 * what makes the several-conditions-at-once table possible at all.
 */
import { describe, expect, test } from 'bun:test';
import { ALERT_BAND_BORDER, ALERT_BAND_STYLES, ALERT_FLASH_MS, type AlertBandStyle } from '../src/components/alertBand.ts';
import { BLUE_FLAG_ID, flagStrip } from '../src/components/flagStrip.ts';
import { BLUE_FLAG_DETAILS, setting } from '../src/contract.ts';
import { carBehindClass, carBehindPositionClass } from '../src/second/values.ts';
import { measureText } from '../src/design/advances.ts';
import { contains, rect } from '../src/design/geometry.ts';
import { ALERT_CATALOGUE, flagBit, isFlag, type SessionFlagBit } from '../src/flags.ts';
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

/** A bit iRacing sets, or the id of a car alert whose whole condition holds. */
type Raised = SessionFlagBit | 'ignition' | 'engine' | 'incident' | 'pushToPass' | 'headlightFlash';

/**
 * Evaluates one band's `Visible` with the named bits set, the named car alerts raised and the named
 * normalised properties at 1. Handles exactly what flags.ts emits for the band: parenthesised `and`,
 * `or`, `!`, `isnull([prop], 0) = 1` and a car alert's `when`, which it replaces whole. Anything else
 * throws rather than guessing.
 */
function visible(layer: LayerItem, set: readonly Raised[], normalised: readonly string[] = []): boolean {
  let s = String(layer.bindings?.Visible?.formula ?? '');
  // The alerts first, whole: their conditions are the only text here that is not a bit read.
  for (const condition of ALERT_CATALOGUE) {
    if (!isFlag(condition)) s = s.split(condition.when).join(set.includes(condition.id as Raised) ? '1 = 1' : '0 = 1');
  }
  for (const condition of ALERT_CATALOGUE.filter(isFlag)) {
    for (const bit of condition.bits) s = s.split(`isnull(${flagBit(bit)}, 0)`).join(set.includes(bit) ? '1' : '0');
    if (condition.limiter) s = s.split(`isnull([DataCorePlugin.GameData.${condition.limiter}], 0)`).join(normalised.includes(condition.limiter) ? '1' : '0');
  }
  s = s.replace(/\band\b/g, '&&').replace(/\bor\b/g, '||').replace(/([^!<>=])=([^=])/g, '$1===$2');
  if (!/^[\s()!&|=01]+$/.test(s)) throw new Error(`the condition holds something this cannot evaluate: ${s}`);
  return Boolean(new Function(`return (${s});`)());
}

/** Which band draws with these raised, or undefined when none does. */
function shown(set: readonly Raised[], normalised: readonly string[] = []): string | undefined {
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
    expect(layers().map((l) => l.name)).toEqual(ALERT_CATALOGUE.map((c) => `flag.${c.id}`));
  });

  test('four shapes and no fifth, the canvas’s bands, outlined bands and two patterns', () => {
    expect(new Set(ALERT_CATALOGUE.map((c) => c.band.shape))).toEqual(new Set(['filled', 'outlined', 'chequer', 'striped']));
    // Each pattern is one flag's: the board is the chequer's and the stripes are the debris flag's.
    expect(ALERT_CATALOGUE.filter((c) => c.band.shape === 'chequer').map((c) => c.id)).toEqual(['chequered']);
    expect(ALERT_CATALOGUE.filter((c) => c.band.shape === 'striped').map((c) => c.id)).toEqual(['debris']);
  });

  test('every band names itself, except the chequer, which has no name to write', () => {
    for (const condition of ALERT_CATALOGUE) {
      const names = texts(layerOf(condition.id)).map((t) => t.text);
      if (condition.band.shape === 'chequer') {
        expect({ id: condition.id, names }).toEqual({ id: condition.id, names: [] });
        continue;
      }
      // The blue is the one band that can say more than its own name, so it carries a run per
      // value of BlueFlagDetail; every run opens with the label, which is what the setting is a
      // detail *of*.
      const expected = condition.id === BLUE_FLAG_ID ? BLUE_FLAG_DETAILS.length : 1;
      expect({ id: condition.id, drawn: names.length }).toEqual({ id: condition.id, drawn: expected });
      for (const name of names) expect({ id: condition.id, name, opens: name.startsWith(condition.band.label) }).toEqual({ id: condition.id, name, opens: true });
    }
  });

  /**
   * What a blue flag says beyond its colour, which is the one thing `OpenDash.BlueFlagDetail`
   * decides. Three runs over one line box, exactly one of them visible, so the band cannot end up
   * writing its name twice over somebody's chosen detail.
   */
  test('the blue band carries one run per detail, and exactly one of them shows', () => {
    const runs = texts(layerOf('blue'));
    expect(runs.map((r) => r.name)).toEqual(['flag.blue.label', 'flag.blue.label.class', 'flag.blue.label.positionClass']);
    // The plain run is the label as it has always been drawn and is bound to nothing; the two
    // details are bound and each declares the widest string it can draw, since WPF clips whatever
    // does not fit and a bound run measured on its sample is a run measured on the wrong string.
    expect(runs[0]!.bindings?.Text).toBeUndefined();
    for (const run of runs.slice(1)) {
      expect({ name: run.name, bound: typeof run.bindings?.Text?.formula === 'string' }).toEqual({ name: run.name, bound: true });
      expect({ name: run.name, widest: run.widest }).toMatchObject({ name: run.name, widest: expect.any(String) });
      expect(run.widest!.length).toBeGreaterThan(runs[0]!.text.length);
    }
    // One per value, and each visible on its own value alone.
    BLUE_FLAG_DETAILS.forEach((detail, i) => {
      expect(runs[i]!.bindings?.Visible?.formula).toBe(setting.blueFlagDetailIs(detail));
    });
    // The class of the car behind and its position come from the relative helpers rather than from
    // a second reading of the leaderboard, so the band and the tables cannot name two cars.
    expect(runs[1]!.bindings?.Text?.formula).toContain(carBehindClass());
    expect(runs[2]!.bindings?.Text?.formula).toContain(carBehindPositionClass());
  });

  test('the nano writes no name at all, on any condition it draws', () => {
    const strip = rect(0, 274, 800, 12);
    for (const condition of ALERT_CATALOGUE) {
      const nano = layers(strip, ALERT_BAND_STYLES.nano).find((l) => l.name === `flag.${condition.id}`);
      if (nano) expect({ id: condition.id, names: texts(nano).length }).toEqual({ id: condition.id, names: 0 });
    }
  });

  test('a neutral alert is its name, so where no name is written it draws nothing at all', () => {
    // Push to pass and the headlight flash are white, and white without a word is the white flag
    // filled and the black family outlined. The nano writes no word, so they have no layer there;
    // every other condition keeps its colour, which is what the nano's strip is.
    const nano = layers(rect(0, 274, 800, 12), ALERT_BAND_STYLES.nano).map((l) => l.name.slice('flag.'.length));
    expect(nano).toEqual(ALERT_CATALOGUE.map((c) => c.id).filter((id) => id !== 'pushToPass' && id !== 'headlightFlash'));
    // And the colour they would have drawn is the one that makes it necessary.
    expect(ds.purpose.alert.p2p).toBe(ds.purpose.flag.white);
  });

  test('the incident writes its count against its limit while it has the whole band', () => {
    // The one band with a number to say. The run is bound, and declares the widest string it can
    // draw, since WPF clips whatever does not fit and a bound run measured on its sample is a run
    // measured on the wrong string. It opens with the label the corner blocks keep.
    const runs = texts(layerOf('incident'));
    expect(runs.map((r) => r.name)).toEqual(['flag.incident.label']);
    const run = runs[0]!;
    expect(run.text).toBe('INCIDENT · 4x / 17');
    expect(run.widest).toBe('INCIDENT · 999x / 999');
    const bind = String(run.bindings?.Text?.formula ?? '');
    expect(bind).toContain('[DataCorePlugin.GameRawData.Telemetry.PlayerCarMyIncidentCount]');
    expect(bind).toContain('WeekendOptions.IncidentLimit');
    // A session with no limit writes the count alone rather than "/ unlimited".
    expect(bind).toContain("'unlimited'");
    // Filled in the incident's amber, as the canvas draws it, and so written in onFlag. The amber is
    // the meatball's orange as well, which is why the meatball's outline and the incident's fill have
    // to stay two shapes; the test below holds them apart where no name is written.
    expect(ds.purpose.alert.incident).toBe(ds.purpose.flag.orange);
    expect(run.textColor).toBe(ds.purpose.flag.onFlag);
  });
});

describe('every shape is opaque over the whole band', () => {
  // The black flag was once an outline with nothing behind it, which left band D's fuel page fully
  // readable underneath the most serious thing the band can say. Whatever a band does, and whatever
  // phase a flashing one is in, it has to leave nothing of the page showing.
  for (const frame of [WIDE, NARROW]) {
    test(`at ${frame.width} x ${frame.height} each band grounds itself and stays inside band D`, () => {
      for (const condition of ALERT_CATALOGUE) {
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
    for (const condition of ALERT_CATALOGUE) {
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
    expect(ALERT_CATALOGUE.filter((c) => c.band.shape === 'outlined').map((c) => c.id)).toEqual([
      'ignition',
      'engine',
      'disqualify',
      'furled',
      'black',
      'meatball',
      'startSet',
      'startReady',
      'headlightFlash',
    ]);
  });

  test('a filled band wears its colour on the fill and the border and writes its name in onFlag', () => {
    for (const condition of ALERT_CATALOGUE) {
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
    for (const condition of ALERT_CATALOGUE.filter((c) => c.id !== 'yellowWaving')) {
      const blinking = [...walkItems([layerOf(condition.id)])].filter((i) => i.blink?.enabled);
      expect({ id: condition.id, blinking: blinking.map((i) => i.name) }).toEqual({ id: condition.id, blinking: [] });
    }
  });
});

/**
 * What a layer draws, with its names taken off and its rectangles counted from the frame: two
 * conditions whose drawings are equal are one band to a driver, whatever they are called.
 */
const drawing = (layer: LayerItem, frame: Rect): unknown[] =>
  [...walkItems([layer])]
    .filter((i) => i.kind !== 'layer')
    .map((i) => ({
      kind: i.kind,
      at: { ...i.rect, left: i.rect.left - frame.left, top: i.rect.top - frame.top },
      fill: i.kind === 'rect' ? i.backgroundColor : undefined,
      border: i.kind === 'rect' ? i.border : undefined,
      ink: i.kind === 'text' ? i.textColor : undefined,
      blink: i.blink,
    }));

/**
 * The debris flag is yellow with red stripes, and since #498 every band draws the stripes. Drawn as a
 * yellow named DEBRIS it was the yellow flag wherever no name was written: the nano's strip, the
 * companion's, and the sixteen or twelve pixels a settled flag keeps on a face with no corner block.
 */
describe('the debris flag is its yellow and red wherever it is drawn', () => {
  const unnamed: AlertBandStyle = { ...ALERT_BAND_STYLES.standard, labels: false };
  const FRAMES: { where: string; frame: Rect; style: AlertBandStyle }[] = [
    { where: 'the widest band', frame: WIDE, style: ALERT_BAND_STYLES.standard },
    { where: 'the portrait band', frame: NARROW, style: ALERT_BAND_STYLES.standard },
    { where: 'the nano strip', frame: rect(0, 274, 800, 12), style: ALERT_BAND_STYLES.nano },
    { where: 'the portrait companion strip', frame: rect(0, 838, 480, 12), style: ALERT_BAND_STYLES.nano },
    { where: 'a sixteen-pixel settled block', frame: rect(0, 420, 16, 60), style: unnamed },
    { where: 'a twelve-pixel settled block', frame: rect(0, 630, 12, 56), style: unnamed },
  ];

  for (const { where, frame, style } of FRAMES) {
    test(`${where}: the yellow, and red stripes of one width that open and close on it`, () => {
      const [ground, ...rest] = rects(layerOf('debris', frame, style));
      // A pattern with no border, as the chequer has none: the stripes run to the band's own edges.
      expect({ where, rect: ground!.rect, fill: ground!.backgroundColor, border: ground!.border }).toEqual({ where, rect: frame, fill: ds.purpose.flag.debris, border: undefined });
      const stripes = rest.filter((r) => r.backgroundColor === ds.purpose.flag.debrisStripe);
      expect({ where, stripes: stripes.length >= 1 }).toEqual({ where, stripes: true });
      for (const stripe of stripes) {
        expect({ stripe: stripe.name, top: stripe.rect.top, height: stripe.rect.height }).toEqual({ stripe: stripe.name, top: frame.top, height: frame.height });
      }
      // Yellow at both ends, and every run of either colour within a pixel of every other, which is
      // what an odd count of equal stripes is.
      const edges = [frame.left, ...stripes.flatMap((s) => [s.rect.left, s.rect.left + s.rect.width]), frame.left + frame.width];
      const runs = edges.slice(1).map((edge, i) => edge - edges[i]!);
      expect({ where, runs, even: Math.max(...runs) - Math.min(...runs) <= 1, empty: runs.some((r) => r <= 0) }).toMatchObject({ where, even: true, empty: false });
    });
  }

  test('its name sits on a plate of its yellow, over the stripes and inside the band', () => {
    for (const frame of [WIDE, NARROW]) {
      const layer = layerOf('debris', frame);
      const order = layer.children.map((c) => c.name);
      const plate = rects(layer).find((r) => r.name === 'flag.debris.plate');
      const name = texts(layer)[0];
      if (!plate || !name) throw new Error('the debris band names itself on a plate');
      expect({ fill: plate.backgroundColor, inside: contains(frame, plate.rect), ink: name.textColor }).toEqual({ fill: ds.purpose.flag.debris, inside: true, ink: ds.purpose.flag.onFlag });
      // Over every stripe and under the name.
      const lastStripe = Math.max(...order.map((n, i) => (/\.s\d+$/.test(n) ? i : -1)));
      expect({ plate: order.indexOf(plate.name) > lastStripe, name: order.indexOf(name.name) > order.indexOf(plate.name) }).toEqual({ plate: true, name: true });
      // Round the name's ink, measured in the face it is drawn in, and round its canvas line box.
      const ink = measureText('BarlowBold', name.text, name.fontSize);
      const centre = frame.left + frame.width / 2;
      const lineTop = frame.top + (frame.height - name.fontSize) / 2;
      expect({
        left: plate.rect.left <= centre - ink / 2,
        right: plate.rect.left + plate.rect.width >= centre + ink / 2,
        top: plate.rect.top <= lineTop,
        bottom: plate.rect.top + plate.rect.height >= lineTop + name.fontSize,
      }).toEqual({ left: true, right: true, top: true, bottom: true });
    }
  });

  test('and where no name is written, it is not the yellow flag', () => {
    for (const { where, frame, style } of FRAMES.filter((f) => !f.style.labels)) {
      expect({ where, drawn: drawing(layerOf('debris', frame, style), frame) }).not.toEqual({ where, drawn: drawing(layerOf('yellow', frame, style), frame) });
    }
  });
});

/**
 * The meatball and the incident are one amber, `purpose.flag.orange` and `purpose.alert.incident`
 * both being `#FFB300`, and a driver who has just hit something is the driver a meatball is likeliest
 * to be for. Where a name is written the name tells them apart; where none is, only the shape can, so
 * the meatball is the black flag's outline in its orange and the incident is the canvas's filled band
 * (#498). Were both outlined, or both filled, the nano would draw one band for the two.
 */
describe('the meatball and the incident are two drawings where no name is written', () => {
  const unnamed: AlertBandStyle = { ...ALERT_BAND_STYLES.standard, labels: false };
  for (const { where, frame, style } of [
    { where: 'the nano strip', frame: rect(0, 274, 800, 12), style: ALERT_BAND_STYLES.nano },
    { where: 'the portrait companion strip', frame: rect(0, 838, 480, 12), style: ALERT_BAND_STYLES.nano },
    { where: 'a sixteen-pixel settled block', frame: rect(0, 420, 16, 60), style: unnamed },
    { where: 'a twelve-pixel settled block', frame: rect(0, 630, 12, 56), style: unnamed },
  ]) {
    test(where, () => {
      expect(ds.purpose.alert.incident).toBe(ds.purpose.flag.orange);
      expect({ where, drawn: drawing(layerOf('meatball', frame, style), frame) }).not.toEqual({ where, drawn: drawing(layerOf('incident', frame, style), frame) });
    });
  }
});

describe('several conditions raised at once', () => {
  // iRacing raises more than one bit constantly: a caution is yellow plus caution plus
  // cautionWaving, and the last lap of a race under a black flag is two at once. The band draws
  // one, so the only question that matters is which -- and it has to be the same answer the box
  // gives, or a driver with both in front of them is told two different things.
  const cases: { name: string; bits: Raised[]; expect: string | undefined }[] = [
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
    // The car alerts, ranked in the same list. A car that cannot move outranks every flag, which is
    // the canvas's order: nothing else on the band is actionable until the engine is running.
    { name: 'a stall under a red flag', bits: ['engine', 'red'], expect: 'engine' },
    { name: 'the ignition off names the switch rather than the stall it causes', bits: ['ignition', 'engine', 'yellow'], expect: 'ignition' },
    { name: 'an incident under a yellow waits for the yellow', bits: ['incident', 'yellow'], expect: 'yellow' },
    { name: 'an incident under a caution waits for it too', bits: ['incident', 'yellow', 'caution', 'cautionWaving'], expect: 'caution' },
    { name: 'an incident under debris waits for it', bits: ['incident', 'debris'], expect: 'debris' },
    { name: 'an incident is told over a blue flag', bits: ['incident', 'blue'], expect: 'incident' },
    { name: 'an incident on the last lap', bits: ['incident', 'white'], expect: 'incident' },
    { name: 'a meatball after the contact that earned it', bits: ['incident', 'repair'], expect: 'meatball' },
    { name: 'push to pass on the last lap', bits: ['pushToPass', 'white'], expect: 'white' },
    { name: 'push to pass under a chequered finish', bits: ['pushToPass', 'checkered'], expect: 'chequered' },
    { name: 'push to pass alone', bits: ['pushToPass'], expect: 'pushToPass' },
    { name: 'a flash while pushing to pass', bits: ['headlightFlash', 'pushToPass'], expect: 'pushToPass' },
    { name: 'a flash while being lapped', bits: ['headlightFlash', 'blue'], expect: 'blue' },
    { name: 'a flash alone', bits: ['headlightFlash'], expect: 'headlightFlash' },
    { name: 'every car alert at once', bits: ['ignition', 'engine', 'incident', 'pushToPass', 'headlightFlash'], expect: 'ignition' },
  ];
  for (const c of cases) test(c.name, () => expect(shown(c.bits)).toBe(c.expect));

  test('exactly one band is ever drawn, whichever conditions are raised', () => {
    // Every pair in the catalogue, which is the case a hand-written list of examples misses, and
    // the winner of every pair is the one the catalogue ranks first.
    const raisers: Raised[] = ALERT_CATALOGUE.flatMap((c): Raised[] => (isFlag(c) ? [...c.bits] : [c.id as Raised]));
    for (const a of raisers) for (const b of raisers) expect(() => shown([a, b])).not.toThrow();
    const alone = (r: Raised): string | undefined => shown([r]);
    const rank = (id: string | undefined): number => ALERT_CATALOGUE.findIndex((c) => c.id === id);
    for (const a of raisers) {
      for (const b of raisers) {
        const [ra, rb] = [alone(a), alone(b)];
        if (ra === undefined || rb === undefined) continue;
        expect({ a, b, shown: shown([a, b]) }).toEqual({ a, b, shown: rank(ra) <= rank(rb) ? ra : rb });
      }
    }
  });

  test('the green is the flag SimHub limits, so band D is not green for a whole race', () => {
    // iRacing holds the `green` bit for the entire green-flag stint, where the green flag is an
    // event the canvas gives three seconds. SimHub's GreenLimiter is the only clock OpenDash has.
    expect(shown(['green'])).toBeUndefined();
    expect(shown(['green'], ['Flag_Green'])).toBe('green');
    // And it yields to anything above it, limiter or no limiter.
    expect(shown(['green', 'yellow'], ['Flag_Green'])).toBe('yellow');
  });
});
