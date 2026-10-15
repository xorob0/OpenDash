/**
 * The flags on a sim that publishes no `SessionFlagsDetails`, #551, and the plugin's answers for the
 * ones SimHub gets wrong on Le Mans Ultimate, #1129.
 *
 * Assetto Corsa, ACC and Le Mans Ultimate publish SimHub's six `Flag_*` summaries and none of iRacing's
 * bits. Band D, the flag box and the strip lamps read the bits, and until #551 they were dark on those
 * sims for every flag, a yellow included. Here each surface is evaluated over one frame as SimHub
 * would answer it, a comparison with an absent property throwing and a throw read the way that
 * surface reads it, and the three must show the same flag: the one the summary says, for the six
 * conditions that have one, and nothing for the rest.
 *
 * Only what main already exported is imported, so that these fail there on behaviour rather than on
 * a missing name.
 */
import { describe, expect, test } from 'bun:test';
import { bandRaised, conditionVisible, FLAG_CATALOGUE, flagBit, type FaceFlag } from '../src/flags.ts';
import { flagEffects } from '../src/leds/effects.ts';
import { flagContainers } from '../src/leds/profile.ts';
import { flagVisible } from '../src/components/flagStrip.ts';
import type { MatrixContainer } from '../src/generator.ts';
import { evalTyped } from './ncalcEval.ts';

type Frame = Record<string, unknown>;

/** A frame from a sim with no flag bits: the six summaries, the named ones raised. */
const summaries = (...raised: FaceFlag[]): Frame => {
  const frame: Frame = {};
  for (const flag of ['Flag_Black', 'Flag_Checkered', 'Flag_Yellow', 'Flag_Blue', 'Flag_White', 'Flag_Green'] as const) {
    frame[`DataCorePlugin.GameData.${flag}`] = raised.includes(flag) ? 1 : 0;
  }
  return frame;
};

/** The same frame with every iRacing bit published and clear, which is a frame with no flag out. */
const withBitsClear = (frame: Frame): Frame => {
  const all: Frame = { ...frame };
  for (const condition of FLAG_CATALOGUE) for (const bit of condition.bits) all[flagBit(bit).slice(1, -1)] = 0;
  return all;
};

/** The plugin's answers, as it publishes them on Le Mans Ultimate. */
const answers = (shown: Partial<Record<'YellowFlagShown' | 'BlackFlagShown' | 'WhiteFlagShown' | 'ChequeredFlagShown', boolean>>): Frame =>
  Object.fromEntries(Object.entries(shown).map(([name, value]) => [`OpenDash.${name}`, value]));

/** A formula's truth, a throw being `fallback`: false for a `when`, true for a CustomStatus. */
const truth = (formula: string, frame: Frame, fallback: boolean): boolean => {
  try {
    return evalTyped(formula, frame) === true;
  } catch {
    return fallback;
  }
};

const formulaOf = (c: MatrixContainer): string => (c.kind === 'when' ? (typeof c.formula === 'string' ? c.formula : c.formula.expression) : '');

/** The box's conditions that hold, which its ranking makes one at most. */
const box = (frame: Frame): string[] => flagContainers(1).filter((c) => truth(formulaOf(c), frame, false)).map((c) => c.description ?? '');

/** Band D's conditions that hold. */
const band = (frame: Frame): string[] => FLAG_CATALOGUE.filter((c) => truth(conditionVisible(c, false, FLAG_CATALOGUE, bandRaised), frame, false)).map((c) => c.id);

/** The strip's flag rows that are on; a CustomStatus takes a throw for on. */
const strip = (frame: Frame): string[] => flagEffects().filter((e) => truth(e.when, frame, true)).map((e) => e.id.replace(/^flag\./, ''));

describe('a sim with only the summaries, #551', () => {
  const cases: [FaceFlag, string][] = [
    ['Flag_Yellow', 'yellow'],
    ['Flag_Blue', 'blue'],
    ['Flag_Black', 'black'],
    ['Flag_White', 'white'],
    ['Flag_Checkered', 'chequered'],
  ];
  for (const [flag, id] of cases) {
    test(`${flag} shows the ${id} on the box, band D and the strip, and nothing else`, () => {
      const frame = summaries(flag);
      expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: [id], band: [id], strip: [id] });
    });
  }

  test('the green shows too, band D reading the green SimHub limits as it does on iRacing', () => {
    const frame = summaries('Flag_Green');
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: ['green'], band: ['green'], strip: ['green'] });
  });

  test('nothing out is nothing shown, on every surface', () => {
    const frame = summaries();
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: [], band: [], strip: [] });
  });

  test('the black outranks the yellow there as it does on iRacing', () => {
    const frame = summaries('Flag_Black', 'Flag_Yellow');
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: ['black'], band: ['black'], strip: ['black'] });
  });

  test('a condition with no summary stays dark: no yellow is drawn as a caution, a waved yellow or debris', () => {
    for (const flag of ['Flag_Yellow', 'Flag_Black'] as const) {
      const shown = new Set([...box(summaries(flag)), ...band(summaries(flag)), ...strip(summaries(flag))]);
      for (const dark of ['red', 'disqualify', 'furled', 'meatball', 'caution', 'yellowWaving', 'debris', 'startSet', 'startReady']) {
        expect({ flag, dark, shown: shown.has(dark) }).toEqual({ flag, dark, shown: false });
      }
    }
  });

  test('on iRacing a published bit that is clear wins over the summary, so iRacing reads what it read before', () => {
    // iRacing publishes Flag_Yellow too, folding four bits into it; where the bits say no yellow is
    // out, none is drawn, whatever the summary says.
    for (const flag of ['Flag_Yellow', 'Flag_Black', 'Flag_Blue', 'Flag_White', 'Flag_Checkered'] as const) {
      const frame = withBitsClear(summaries(flag));
      expect({ flag, box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ flag, box: [], band: [], strip: [] });
    }
  });
});

describe("the plugin's answers where the summary is wrong, #1129", () => {
  test('a chequer SimHub never raises is shown when the plugin says the session is over', () => {
    const frame = { ...summaries(), ...answers({ ChequeredFlagShown: true }) };
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: ['chequered'], band: ['chequered'], strip: ['chequered'] });
  });

  test('a black SimHub raises for a retired car is not shown when the plugin says no penalty is out', () => {
    const frame = { ...summaries('Flag_Black'), ...answers({ BlackFlagShown: false }) };
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: [], band: [], strip: [] });
  });

  test('a yellow in another sector is not shown when the plugin says the driver\'s own is clear', () => {
    const frame = { ...summaries('Flag_Yellow'), ...answers({ YellowFlagShown: false }) };
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: [], band: [], strip: [] });
  });

  test('a white SimHub never raises is shown on the last lap', () => {
    const frame = { ...summaries(), ...answers({ WhiteFlagShown: true }) };
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: ['white'], band: ['white'], strip: ['white'] });
  });

  test('the round face\'s ring reads the same answers', () => {
    expect(truth(flagVisible('Flag_Checkered'), { ...summaries(), ...answers({ ChequeredFlagShown: true }) }, false)).toBe(true);
    expect(truth(flagVisible('Flag_Yellow'), { ...summaries('Flag_Yellow'), ...answers({ YellowFlagShown: false }) }, false)).toBe(false);
    expect(truth(flagVisible('Flag_Yellow'), summaries('Flag_Yellow'), false)).toBe(true);
    // ...and an answer of null, which is every answer on iRacing, leaves SimHub's own.
    expect(truth(flagVisible('Flag_Black'), { ...summaries('Flag_Black'), 'OpenDash.BlackFlagShown': null }, false)).toBe(true);
  });

  test('on iRacing the bits are asked first, so an answer could not change a thing there', () => {
    const frame = { ...withBitsClear(summaries()), ...answers({ ChequeredFlagShown: true, WhiteFlagShown: true, BlackFlagShown: true, YellowFlagShown: true }) };
    expect({ box: box(frame), band: band(frame), strip: strip(frame) }).toEqual({ box: [], band: [], strip: [] });
  });
});
