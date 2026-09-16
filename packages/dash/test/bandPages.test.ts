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
import type { Item, TextItem } from '../src/generator.ts';
import { BAND_PAGES, bandCorners, bandCornerWidths, bandMetrics, bandPageItems } from '../src/zones/bandPages.ts';
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

    test(`${face} sets a value under its label with a 5 px gap`, () => {
      const items = pageTexts(face as keyof typeof BANDS, 'fuel');
      const label = named(items, 'fuel.label');
      const value = named(items, 'fuel.value');
      // Both boxes are WPF line boxes, which start a tenth of the size above the line they are
      // given, so the canvas gap is read off the lines rather than off the boxes.
      const lineOf = (i: TextItem): number => i.rect.top + 0.1 * i.fontSize;
      expect(Math.round(lineOf(value) - (lineOf(label) + label.fontSize))).toBe(5);
    });

    test(`${face} sets a unit 5 px after the value it follows`, () => {
      const items = pageTexts(face as keyof typeof BANDS, 'fuel');
      const value = named(items, 'fuel.value');
      const unit = named(items, 'fuel.unit');
      expect(unit.rect.left - (value.rect.left + value.rect.width)).toBeLessThanOrEqual(5);
      expect(unit.rect.left).toBeGreaterThan(value.rect.left + value.rect.width - 5);
    });
  }

  // Every band draws the size its artboard asks for. 13 + 5 + 34 is a 52 px block and WPF's box
  // around the 34 runs six tenths of a pixel past a 60 px band, which used to shrink the value;
  // the block rides up by those six tenths instead, so the 60 and the nano's 58 both keep 34 and
  // only the 54 px band at 1280 by 400 is genuinely too short for it.
  test('every band draws the value size its artboard asks for', () => {
    const sizeOn = (face: keyof typeof BANDS): number => named(pageTexts(face, 'fuel'), 'fuel.value').fontSize;
    expect(sizeOn('1920x480')).toBe(34);
    expect(sizeOn('800x286')).toBe(34);
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

  test('D7 Relative heads each gap with the position of the car it belongs to', () => {
    expect(BAND_PAGES.relative!.map((f) => f.labelWidest)).toEqual(['P99', 'P99', 'P99']);
    const items = pageTexts('1920x480', 'relative');
    const drawn: Record<string, string> = { ahead: 'P3', you: 'P4', behind: 'P5' };
    for (const id of ['ahead', 'you', 'behind']) {
      const label = named(items, `${id}.label`);
      expect({ id, text: label.text, widest: label.widest ?? '', bound: bound(label, 'Text')?.includes("'P'") ?? false }).toEqual({
        id,
        text: drawn[id]!,
        widest: 'P99',
        bound: true,
      });
    }
  });

  test('a fuel time is minutes and seconds, not an hour that is nearly always zero', () => {
    expect(BAND_PAGES.fuel!.find((f) => f.id === 'time')!.sample).toBe('08:46');
    const bind = BAND_PAGES.fuel!.find((f) => f.id === 'time')!.bind;
    expect(bind).toContain("'--:--'");
    expect(bind).not.toContain('3600');
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

  test('the page rank keeps the band’s 22 px between itself and each corner', () => {
    for (const [face, band] of Object.entries(BANDS)) {
      if (!band.corners) continue;
      const taken = bandCornerWidths(band);
      for (const page of Object.keys(BAND_PAGES)) {
        const items = pageTexts(face as keyof typeof BANDS, page);
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
