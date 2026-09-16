/**
 * Band D against the drawings it is read from.
 *
 * `zoneFace.test.ts` already measures every text of every face against its box and keeps the page
 * rank clear of the corner blocks; those are the constraints, and they hold whatever the band is
 * drawn to. What they cannot say is whether it is drawn to the *right* numbers, so this file
 * carries the values themselves: the gaps and sizes off the band of each face's own artboard, and
 * the fields, formats and colours off `design/canvas/ZoneCatalogue.dc.html`, section "band D".
 *
 * The band rectangles are the ones `src/zones/faces/*.ts` give, written out here rather than
 * imported, so that a face that quietly changed its band would fail this file rather than pass it
 * against its own new number.
 */
import { describe, expect, test } from 'bun:test';
import type { DrawableItem, Item, RectangleItem, TextItem } from '../src/generator.ts';
import { BAND_PAGES, BAND_PAGE_IDS, bandCorners, bandCornerWidths, bandMetrics, bandPageItems, bandPageRoom } from '../src/zones/bandPages.ts';
import { TELLTALES, TELLTALE_GAP, TELLTALE_PAGE, telltaleArt, telltaleArtwork } from '../src/zones/telltales.ts';
import { assetNamed } from '../src/design/assets.ts';
import { ds } from '../src/tokens.ts';

const BANDS = {
  '1920x480': { left: 0, top: 420, width: 1920, height: 60, corners: true },
  '1280x480': { left: 0, top: 420, width: 1280, height: 60, corners: true },
  '1280x400': { left: 0, top: 346, width: 1280, height: 54, corners: true },
  '1280x720': { left: 0, top: 660, width: 1280, height: 60, corners: true },
  '850x480': { left: 0, top: 420, width: 850, height: 60, corners: false },
  '800x286': { left: 0, top: 228, width: 800, height: 58, corners: false },
  '600x686': { left: 0, top: 630, width: 600, height: 56, corners: false },
} as const;

const textsIn = (items: readonly Item[]): TextItem[] => items.filter((i): i is TextItem => i.kind === 'text');

/** Everything the band draws that carries a box, which on D8 is the lamps rather than any text. */
const drawablesIn = (items: readonly Item[]): DrawableItem[] => items.filter((i): i is DrawableItem => i.kind !== 'layer');

const pageTexts = (face: keyof typeof BANDS, page: string): TextItem[] => {
  const band = BANDS[face];
  return textsIn(bandPageItems(page, band, '', band.corners));
};

const named = (items: readonly TextItem[], name: string): TextItem => items.find((i) => i.name === name)!;

/** Ids of the fields a page kept, in drawing order. */
const keptIds = (items: readonly TextItem[]): string[] => [...new Set(items.map((i) => i.name.split('.')[0]!))];

const bound = (item: TextItem, target: 'Left' | 'Visible' | 'TextColor' | 'Text'): string | undefined => {
  const b = item.bindings?.[target];
  return b && b.mode === 'formula' && typeof b.formula === 'string' ? b.formula : undefined;
};

describe('the metrics band D is drawn to', () => {
  // The band of each face's artboard: 0 16 padding and a 26 gap at 1920, 850 and the nano, 34 at
  // the 1280s, and 0 12 with an 18 gap in portrait.
  const DRAWN = {
    '1920x480': { padX: 16, fieldGap: 34, valueSize: 34 },
    '1280x480': { padX: 16, fieldGap: 34, valueSize: 34 },
    '1280x400': { padX: 16, fieldGap: 34, valueSize: 24 },
    '1280x720': { padX: 16, fieldGap: 34, valueSize: 34 },
    '850x480': { padX: 16, fieldGap: 26, valueSize: 34 },
    '800x286': { padX: 16, fieldGap: 26, valueSize: 34 },
    '600x686': { padX: 12, fieldGap: 18, valueSize: 24 },
  } as const;

  for (const [face, drawn] of Object.entries(DRAWN)) {
    test(`${face} takes its padding, field gap and value size from its artboard`, () => {
      expect(bandMetrics(BANDS[face as keyof typeof BANDS])).toMatchObject(drawn);
    });
  }

  test('a rectangle no artboard draws falls back to the reference band', () => {
    expect(bandMetrics({ left: 0, top: 0, width: 640, height: 48 })).toMatchObject({ padX: 16, fieldGap: 26, valueSize: 34 });
  });

  for (const [face, band] of Object.entries(BANDS)) {
    test(`${face} spaces the fields of a page by the gap its artboard draws`, () => {
      // The pitch between two fields is the gap plus the width of the field before it, and the
      // field's width is what its own box is measured to; the label boxes carry it.
      const items = pageTexts(face as keyof typeof BANDS, 'sectors');
      const s1 = named(items, 's1.label');
      const s2 = named(items, 's2.label');
      expect(s2.rect.left - (s1.rect.left + s1.rect.width)).toBe(bandMetrics(band).fieldGap);
    });

    test(`${face} sets a value under its label's row with a 5 px gap`, () => {
      const items = pageTexts(face as keyof typeof BANDS, 'fuel');
      const label = named(items, 'fuel.label');
      const value = named(items, 'fuel.value');
      // Both boxes are WPF line boxes, which start a tenth of the size above the line they are
      // given, so the gap is read off the lines rather than off the boxes. The artboards measure it
      // from the label's 13 px row and not from the 15 px label centred in it, which is why the
      // label's own line sits a pixel above the row it is counted from.
      const lineOf = (i: TextItem): number => i.rect.top + 0.1 * i.fontSize;
      expect(label.fontSize).toBe(15);
      const row = lineOf(label) + (15 - 13) / 2;
      expect(Math.round(lineOf(value) - (row + 13))).toBe(5);
    });

    test(`${face} sets a unit 5 px after the value it follows`, () => {
      const items = pageTexts(face as keyof typeof BANDS, 'fuel');
      const value = named(items, 'fuel.value');
      const unit = named(items, 'fuel.unit');
      expect(unit.rect.left - (value.rect.left + value.rect.width)).toBeLessThanOrEqual(5);
      expect(unit.rect.left).toBeGreaterThan(value.rect.left + value.rect.width - 5);
    });
  }

  // 13 + 5 + 34 is the 52 px block every artboard draws, and WPF's boxes around it want 62: the
  // label's box opens two and a half pixels above its row and the value's closes four and a half
  // below its own. A 60 px band holds that once the block stops being centred and rides up to the
  // label's headroom, which is what every 60 px band now draws. The nano's 58 is two pixels short
  // of it and gives those two up from the value, which is the one band that does not draw the size
  // its own artboard asks for; docs/research/design-audit.md carries the question.
  test('every 60 px band draws the 34 its artboard asks for, and the shorter ones what they can hold', () => {
    const sizeOn = (face: keyof typeof BANDS): number => named(pageTexts(face, 'fuel'), 'fuel.value').fontSize;
    expect(sizeOn('1920x480')).toBe(34);
    expect(sizeOn('1280x480')).toBe(34);
    expect(sizeOn('1280x720')).toBe(34);
    expect(sizeOn('850x480')).toBe(34);
    expect(sizeOn('800x286')).toBe(32);
    expect(sizeOn('1280x400')).toBe(24);
    expect(sizeOn('600x686')).toBe(24);
  });

  // The corners are the band's other size, and the two are read together: a page at 34 between two
  // corners at 24 is what every artboard with corner blocks draws.
  test('the corner blocks stay at 24 where the page is drawn at 34', () => {
    const items = textsIn(bandCorners(BANDS['1920x480'], ''));
    for (const id of ['incidents.value', 'clock.value', 'sim.value']) expect(named(items, id).fontSize).toBe(24);
    expect(named(pageTexts('1920x480', 'fuel'), 'fuel.value').fontSize).toBe(34);
  });

  // And nothing the band draws, at any size, hangs out of it.
  test('no band text has a box outside its band', () => {
    for (const [face, frame] of Object.entries(BANDS)) {
      for (const item of [...pageTexts(face as keyof typeof BANDS, 'fuel'), ...textsIn(bandCorners(frame, ''))]) {
        expect({ face, name: item.name, inside: item.rect.top >= frame.top && item.rect.top + item.rect.height <= frame.top + frame.height }).toEqual({
          face,
          name: item.name,
          inside: true,
        });
      }
    }
  });
});

describe('the fields the catalogue draws on each page', () => {
  test('D1 Fuel is the tank, the time, the laps, the refuel, the last two consumptions', () => {
    expect(BAND_PAGES.fuel!.map((f) => f.id)).toEqual(['fuel', 'time', 'laps', 'refuel', 'perLap', 'lastLap']);
    expect(BAND_PAGES.fuel!.map((f) => f.label)).toEqual(['Fuel', 'Fuel time', 'Est. laps', 'Refuel', 'Per lap', 'Last lap']);
    expect(BAND_PAGES.fuel!.find((f) => f.id === 'fuel')!.after).toBe('L');
  });

  test('the refuel of both fuel pages is amber, which is what says it is a number to act on', () => {
    for (const page of ['fuel', 'energy']) expect(BAND_PAGES[page]!.find((f) => f.id === 'refuel')!.color).toBe(ds.color.caution.primary);
  });

  test('D2 Energy ends with the ratio that converts one budget into the other', () => {
    const ratio = BAND_PAGES.energy!.find((f) => f.id === 'ratio')!;
    expect(BAND_PAGES.energy!.map((f) => f.id)).toEqual(['energy', 'perLap', 'laps', 'refuel', 'ratio']);
    expect(ratio.label).toBe('Fuel to energy');
    expect(ratio.bind).toContain('VirtualEnergyPerLap');
    expect(ratio.bind).toContain('Fuel_LitersPerLap');
  });

  test('D4 Tyres is four temperatures under one label, and a compound a driver can read', () => {
    const [temps, compound] = BAND_PAGES.tyres!;
    expect(BAND_PAGES.tyres!.map((f) => f.id)).toEqual(['temps', 'compound']);
    expect(temps!.label).toBe('Tyres °C');
    expect(temps!.row).toHaveLength(3);
    expect([temps!.bind, ...temps!.row!.map((r) => r.bind)].map((b) => b.match(/(LF|RF|LR|RR)tempCM/)![0])).toEqual([
      'LFtempCM',
      'RFtempCM',
      'LRtempCM',
      'RRtempCM',
    ]);
    expect(compound!.widest).toBe('Medium');
    expect(compound!.bind).not.toContain('ucase');
  });

  test('the four temperatures are drawn as four numerals on one line', () => {
    const items = pageTexts('1920x480', 'tyres');
    const cells = items.filter((i) => i.name.startsWith('temps.') && i.name !== 'temps.label');
    expect(cells.map((i) => i.text)).toEqual(['84', '104', '62', '88']);
    expect(new Set(cells.map((i) => i.rect.top)).size).toBe(1);
    expect(cells.every((i) => i.rect.left >= named(items, 'temps.label').rect.left)).toBe(true);
  });

  test('D7 Relative sets the position of each car beside its gap, not above it', () => {
    expect(BAND_PAGES.relative!.map((f) => f.labelWidest)).toEqual(['P99', 'P99', 'P99']);
    const items = pageTexts('1920x480', 'relative');
    const drawn: Record<string, string> = { ahead: 'P3', you: 'P4', behind: 'P5' };
    for (const id of ['ahead', 'you', 'behind']) {
      const position = named(items, `${id}.position`);
      const gap = named(items, `${id}.value`);
      expect({ id, text: position.text, widest: position.widest ?? '', bound: bound(position, 'Text')?.includes("'P'") ?? false }).toEqual({
        id,
        text: drawn[id]!,
        widest: 'P99',
        bound: true,
      });
      // One line, the same size, the position first and the gap eight pixels after it.
      expect({ id, top: position.rect.top, size: position.fontSize }).toEqual({ id, top: gap.rect.top, size: gap.fontSize });
      expect(gap.rect.left - (position.rect.left + position.rect.width)).toBe(8);
    }
    // And the page says what it is, because P3 P4 P5 name the cars rather than the page.
    expect(named(items, 'word').text).toBe('RELATIVE');
    expect(named(items, 'word').rect.left).toBeLessThan(named(items, 'ahead.position').rect.left);
    // The driver's own position and gap are the primary text; the two cars beside him are not.
    expect(named(items, 'you.position').textColor).toBe(ds.color.text.primary);
    expect(named(items, 'ahead.position').textColor).toBe(ds.color.text.label);
  });

  test('D8 Car is not a page of fields at all, so it is not among them', () => {
    expect(Object.keys(BAND_PAGES)).not.toContain(TELLTALE_PAGE);
    expect(BAND_PAGE_IDS[BAND_PAGE_IDS.length - 1]).toBe(TELLTALE_PAGE);
  });

  test('a fuel time is minutes and seconds, not an hour that is nearly always zero', () => {
    expect(BAND_PAGES.fuel!.find((f) => f.id === 'time')!.sample).toBe('08:46');
    const bind = BAND_PAGES.fuel!.find((f) => f.id === 'time')!.bind;
    expect(bind).toContain("'--:--'");
    expect(bind).not.toContain('3600');
  });
});

/**
 * D8, the telltale rank, against the band of `design/canvas/Dash1280x480.dc.html`, which is the one
 * artboard that draws it: twelve lamps of 38 by 32 with a 1 px edge, 10 apart, centred in what the
 * corner blocks leave, and a 20 px pictogram in each.
 *
 * Two of the drawing's parts are absences rather than departures, and the tests say which: the
 * pictogram files are not in the repository, and nine of the twelve lamps have nothing that lights
 * them. Both are recorded in docs/design/zones.md §10.
 */
describe('D8, the telltale rank', () => {
  const page = (face: keyof typeof BANDS): Item[] => {
    const band = BANDS[face];
    return bandPageItems(TELLTALE_PAGE, band, '', band.corners);
  };
  const chipsOn = (face: keyof typeof BANDS): RectangleItem[] =>
    page(face).filter((i): i is RectangleItem => i.kind === 'rect' && i.name.endsWith('.chip'));
  const edgeOn = (chips: readonly RectangleItem[], id: string): string | undefined => {
    const formula = chips.find((i) => i.name === `${id}.chip`)?.border?.colorBinding?.formula;
    return typeof formula === 'string' ? formula : undefined;
  };

  test('the rank is the artboard’s twelve lamps, in its order', () => {
    expect(TELLTALES.map((lamp) => lamp.id)).toEqual([
      'tyreLines',
      'tyreSlant',
      'wiper',
      'surface',
      'abs',
      'esp',
      'engine',
      'fuel',
      'battery',
      'limiter',
      'pressure',
      'door',
    ]);
    expect(chipsOn('1280x480').map((i) => i.name)).toEqual(TELLTALES.map((lamp) => `${lamp.id}.chip`));
  });

  test('a lamp is a 38 by 32 box with a 1 px edge, ten apart and centred on the band', () => {
    const band = BANDS['1280x480'];
    const chips = chipsOn('1280x480');
    for (const chip of chips) {
      expect({ name: chip.name, width: chip.rect.width, height: chip.rect.height }).toEqual({ name: chip.name, width: 38, height: 32 });
      // The dark edge is a shade further back than the dark pictogram, so a rank of dark lamps
      // reads as a row of empty boxes rather than a row of grey ones.
      expect(chip.border).toMatchObject({ top: 1, bottom: 1, left: 1, right: 1, color: ds.color.surface.raised });
      expect({ name: chip.name, top: chip.rect.top }).toEqual({ name: chip.name, top: band.top + (band.height - 32) / 2 });
    }
    for (let i = 1; i < chips.length; i++) {
      expect(chips[i]!.rect.left - (chips[i - 1]!.rect.left + chips[i - 1]!.rect.width)).toBe(TELLTALE_GAP);
    }
  });

  test('the rank is centred in what the corner blocks leave, not in the whole band', () => {
    for (const face of ['1920x480', '1280x480', '1280x720'] as const) {
      const chips = chipsOn(face);
      const room = bandPageRoom(BANDS[face], true);
      const left = chips[0]!.rect.left - room.left;
      const right = room.left + room.width - (chips[chips.length - 1]!.rect.left + 38);
      // Within the pixel a box is rounded to: an odd remainder cannot be halved onto the grid.
      expect({ face, clear: left > 0, even: Math.abs(left - right) <= 1 }).toEqual({ face, clear: true, even: true });
    }
  });

  test('the colours the artboard draws lit are the telltale tokens, lamp by lamp', () => {
    const drawn: Record<string, `#${string}`> = {
      tyreLines: ds.purpose.telltale.info,
      tyreSlant: ds.purpose.telltale.good,
      wiper: ds.purpose.telltale.caution,
      surface: ds.purpose.telltale.caution,
      fuel: ds.purpose.telltale.danger,
      limiter: ds.purpose.telltale.neutral,
    };
    for (const [id, colour] of Object.entries(drawn)) {
      const lamp = TELLTALES.find((each) => each.id === id)!;
      expect({ id, colour: lamp.lit === undefined ? undefined : ds.purpose.telltale[lamp.lit] }).toEqual({ id, colour });
    }
  });

  test('a lamp with a source binds its edge to it; one without carries no binding at all', () => {
    const chips = chipsOn('1280x480');
    expect(edgeOn(chips, 'limiter')).toContain('PitLimiterOn');
    expect(edgeOn(chips, 'limiter')).toContain(ds.purpose.telltale.neutral);
    expect(edgeOn(chips, 'limiter')).toContain(ds.color.surface.raised);
    expect(edgeOn(chips, 'fuel')).toContain('Fuel_RemainingLaps');
    expect(edgeOn(chips, 'engine')).toContain('EngineWarnings');
    // Recording the colour the drawing gives a lamp is not the same as having something to light
    // it: the nine below are drawn dark and bind nothing until the author answers §10.
    for (const id of ['tyreLines', 'tyreSlant', 'wiper', 'surface', 'abs', 'esp', 'battery', 'pressure', 'door']) {
      expect({ id, edge: edgeOn(chips, id) }).toEqual({ id, edge: undefined });
    }
  });

  test('a lamp keeps its place, so nothing in the rank hides or moves', () => {
    for (const chip of chipsOn('1920x480')) {
      expect({ name: chip.name, hides: chip.bindings?.Visible !== undefined, moves: chip.bindings?.Left !== undefined }).toEqual({
        name: chip.name,
        hides: false,
        moves: false,
      });
    }
  });

  test('a band too narrow for twelve sheds from the tail rather than drawing outside', () => {
    const wide = chipsOn('1920x480').map((i) => i.name);
    const narrow = chipsOn('600x686').map((i) => i.name);
    expect(narrow.length).toBeLessThan(wide.length);
    expect(wide.slice(0, narrow.length)).toEqual(narrow);
  });

  test('the pictograms are one file per colour, and the rank draws whichever the registry holds', () => {
    // An ImageItem carries no tint, so a lamp that can light owes two files and a lamp nothing
    // lights owes one. None of them is in the repository yet, which is why the lamps are boxes.
    const wanted = telltaleArtwork();
    expect(new Set(wanted).size).toBe(wanted.length);
    const fuel = TELLTALES.find((lamp) => lamp.id === 'fuel')!;
    expect(wanted).toContain(telltaleArt(fuel, 'danger'));
    expect(wanted).toContain(telltaleArt(fuel, 'off'));
    for (const item of page('1280x480')) {
      if (item.kind !== 'image') continue;
      expect({ name: item.name, named: wanted.includes(item.image), registered: assetNamed(item.image) !== undefined }).toEqual({
        name: item.name,
        named: true,
        registered: true,
      });
    }
  });

  test('the count the artboard draws on the wiper waits on a source, as the relative page’s flag does', () => {
    expect(TELLTALES.find((lamp) => lamp.id === 'wiper')!.count).toBe('2');
    // Nothing publishes a wiper state, so the lamp never lights and its count is never drawn: a
    // number under a dark lamp is a number nobody measured.
    expect(textsIn(page('1280x480'))).toEqual([]);
  });
});

describe('the corner blocks at the ends of the band', () => {
  const corners = (face: keyof typeof BANDS): TextItem[] => textsIn(bandCorners(BANDS[face], 'corner.'));

  test('incidents are amber and the track state is a word, not a shout', () => {
    const items = corners('1280x720');
    expect(named(items, 'corner.incidents.value').textColor).toBe(ds.color.caution.primary);
    expect(named(items, 'corner.incidents.value').text).toBe('3x');
    const track = named(items, 'corner.trackState.value');
    expect({ text: track.text, widest: track.widest }).toEqual({ text: 'Dry', widest: 'Moderate' });
    expect(bound(track, 'Text')).not.toContain('ucase');
  });

  test('the left corner leaves the zone letter its room and sets its two fields 18 apart', () => {
    const band = BANDS['1280x400'];
    const items = corners('1280x400');
    const incidents = named(items, 'corner.incidents.label');
    const track = named(items, 'corner.trackState.label');
    expect(incidents.rect.left).toBeGreaterThan(band.left + bandMetrics(band).padX);
    expect(track.rect.left - (incidents.rect.left + incidents.rect.width)).toBe(18);
  });

  test('the right corner ends against the padding, with 18 between its groups and 6 between the lamps', () => {
    const band = BANDS['1920x480'];
    const items = corners('1920x480');
    const sim = named(items, 'corner.sim.value');
    const clock = named(items, 'corner.clock.value');
    expect(sim.rect.left + sim.rect.width).toBe(band.width - bandMetrics(band).padX);
    expect(sim.rect.left - (clock.rect.left + clock.rect.width)).toBe(18);
    expect(sim.hAlign).toBe('right');
    expect(clock.hAlign).toBe('right');

    const lamps = ['drs', 'p2p', 'spt'].map((id) => named(items, `corner.${id}`));
    expect(lamps[1]!.rect.left - (lamps[0]!.rect.left + lamps[0]!.rect.width)).toBe(6);
    expect(lamps[2]!.rect.left - (lamps[1]!.rect.left + lamps[1]!.rect.width)).toBe(6);
    expect(named(items, 'corner.clock.label').rect.left - (lamps[2]!.rect.left + lamps[2]!.rect.width)).toBe(18);
  });

  test('a lamp changes colour rather than place when it is not lit', () => {
    for (const id of ['drs', 'p2p', 'spt']) {
      const lamp = named(corners('1920x480'), `corner.${id}`);
      expect({ id, colour: bound(lamp, 'TextColor') !== undefined, hides: bound(lamp, 'Visible') !== undefined, moves: bound(lamp, 'Left') !== undefined }).toEqual({
        id,
        colour: true,
        hides: false,
        moves: false,
      });
    }
  });

  test('a lamp is a 20 px chip outlined in the colour its word is written in', () => {
    // The outline and the ink carry one colour between them, and both are bound: a chip whose
    // border stayed bright around a dim word would read as a lamp half on. The border's colour is
    // written inside BorderStyle, which is the only place SimHub reads one.
    const items = bandCorners(BANDS['1920x480'], 'corner.');
    const lit: Record<string, string> = { drs: ds.purpose.flag.green, p2p: ds.purpose.flag.blue, spt: ds.color.caution.primary };
    for (const id of ['drs', 'p2p', 'spt']) {
      const chip = items.find((i) => i.name === `corner.${id}.chip`);
      if (chip?.kind !== 'rect') throw new Error(`no chip under the ${id} lamp`);
      const word = named(textsIn(items), `corner.${id}`);
      expect({ id, height: chip.rect.height, width: chip.rect.width }).toEqual({ id, height: 20, width: word.rect.width });
      expect(chip.border).toMatchObject({ top: 1, bottom: 1, left: 1, right: 1, color: ds.color.text.dim });
      expect(chip.border?.colorBinding?.formula).toBe(bound(word, 'TextColor'));
      expect(String(chip.border?.colorBinding?.formula)).toContain(lit[id]!);
      // The word sits inside the chip rather than beside it.
      expect(word.rect.top).toBeGreaterThanOrEqual(chip.rect.top);
      expect(word.rect.top + word.rect.height).toBeLessThanOrEqual(chip.rect.top + chip.rect.height + 1);
    }
  });

  test('the page rank keeps the band’s 22 px between itself and each corner', () => {
    for (const [face, band] of Object.entries(BANDS)) {
      if (!band.corners) continue;
      const taken = bandCornerWidths(band);
      // Every page of the cycle, and every box of it rather than only its texts: D8 is twelve lamps
      // whose boxes are what reaches towards the corners.
      for (const page of BAND_PAGE_IDS) {
        const items = drawablesIn(bandPageItems(page, band, '', band.corners));
        const left = Math.min(...items.map((i) => i.rect.left));
        const right = Math.max(...items.map((i) => i.rect.left + i.rect.width));
        expect({ face, page, clearOfLeft: left >= band.left + taken.left + 22 }).toMatchObject({ clearOfLeft: true });
        expect({ face, page, clearOfRight: right <= band.left + band.width - taken.right - 22 }).toMatchObject({ clearOfRight: true });
      }
    }
  });

  test('a face with no corner block still keeps its padding and the letter its room', () => {
    for (const [face, band] of Object.entries(BANDS)) {
      if (band.corners) continue;
      const items = pageTexts(face as keyof typeof BANDS, 'sectors');
      const left = Math.min(...items.map((i) => i.rect.left));
      const right = Math.max(...items.map((i) => i.rect.left + i.rect.width));
      expect({ face, inside: left > band.left + bandMetrics(band).padX && right <= band.left + band.width - bandMetrics(band).padX }).toMatchObject({ inside: true });
    }
  });
});

describe('what a page does when the band is too narrow for all of it', () => {
  test('it sheds from the tail rather than drawing outside', () => {
    // The fuel page is six fields at 1920 and the 600 band is the narrowest there is: whatever it
    // keeps is a prefix of what the widest keeps, never a subset chosen some other way.
    const wide = keptIds(pageTexts('1920x480', 'fuel'));
    const narrow = keptIds(pageTexts('600x686', 'fuel'));
    expect(wide).toEqual(BAND_PAGES.fuel!.map((f) => f.id));
    expect(wide.slice(0, narrow.length)).toEqual(narrow);
  });
});
