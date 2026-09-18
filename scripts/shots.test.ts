/**
 * What `bun run shots` decides before it touches the VM: how its arguments are read, which
 * packages it walks by default, and what it calls the files it writes. The loop itself is a
 * sequence of remote steps and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { LIST_ORDER } from './dev.ts';
import { FACES, parseArgs, shotName } from './shots.ts';

describe('reading the arguments', () => {
  test('nothing means every face on green', () => {
    expect(parseArgs([])).toMatchObject({ packages: FACES, scenarios: ['green'], noBuild: false, keep: false });
  });

  test('packages and scenarios are comma separated lists', () => {
    expect(parseArgs(['--packages', 'openDash,openDash 850x480', '--scenarios', 'green,yellow'])).toMatchObject({
      packages: ['openDash', 'openDash 850x480'],
      scenarios: ['green', 'yellow'],
    });
  });

  test('either may be written with an equals sign', () => {
    expect(parseArgs(['--scenarios=pit'])).toMatchObject({ scenarios: ['pit'] });
  });

  test('blank entries and stray spaces are dropped', () => {
    expect(parseArgs(['--scenarios', ' green , , yellow '])).toMatchObject({ scenarios: ['green', 'yellow'] });
  });

  test('the switches are read', () => {
    expect(parseArgs(['--no-build', '--keep'])).toMatchObject({ noBuild: true, keep: true });
  });

  test('the output directory defaults inside build/, which is already ignored', () => {
    const opts = parseArgs([]);
    expect('help' in opts ? '' : opts.outDir).toMatch(/build\/shots$/);
  });

  test('help wins over everything else', () => {
    expect(parseArgs(['--packages', 'openDash', '--help'])).toEqual({ help: true });
  });
});

describe('which packages a bare run walks', () => {
  test('every face, and neither second screen', () => {
    // Ten card faces and the zone faces built beside them. The count moves as the zone work lands
    // and moves back when the card path is retired, so it is checked against the list rather than
    // written down.
    expect(FACES).toEqual(LIST_ORDER.filter((n) => !n.includes('Companion') && !n.includes('Pit wall')));
    expect(FACES.length).toBeGreaterThanOrEqual(10);
    expect(FACES.some((f) => f.includes('Companion') || f.includes('Pit wall'))).toBe(false);
  });

  test('the zone face is among them, under the name it now ships as', () => {
    // Since #169 the zone face is plain "openDash"; the card face it replaced says "slots", and
    // both are captured while the two are being compared.
    expect(FACES).toContain('openDash');
    expect(FACES).toContain('openDash slots 1920x480');
  });

  test('every one of them is a package the opener knows how to click', () => {
    for (const face of FACES) expect(LIST_ORDER).toContain(face);
  });
});

describe('what a capture is called', () => {
  test('numbered, so a listing reads in the order the loop ran', () => {
    expect(shotName(1, 'openDash', 'green')).toBe('01-opendash-green.png');
    expect(shotName(12, 'openDash', 'green')).toBe('12-opendash-green.png');
  });

  test('the package and the scenario are in the name, so a file needs no caption', () => {
    expect(shotName(4, 'openDash 850x480', 'yellow')).toBe('04-opendash-850x480-yellow.png');
  });

  test('a name with spaces and a round size still makes one path segment', () => {
    expect(shotName(9, 'openDash 480 round', 'pit')).toBe('09-opendash-480-round-pit.png');
  });

  test('every face produces a distinct file name in one scenario', () => {
    const names = new Set(FACES.map((f, i) => shotName(i + 1, f, 'green')));
    expect(names.size).toBe(FACES.length);
  });
});
