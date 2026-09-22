/**
 * What `bun run previews` decides before it touches the VM: how its arguments are read and what
 * size a capture is scaled to.
 */
import { describe, expect, test } from 'bun:test';
import { LIST_ORDER } from './dev.ts';
import { mergePreviewRun, parseArgs, previewSize, PREVIEW_HEIGHT, PREVIEW_MIN_WIDTH } from './previews.ts';

describe('parseArgs', () => {
  test('defaults to every package on the green scenario', () => {
    const opts = parseArgs([]);
    expect('help' in opts ? [] : opts.packages).toEqual([...LIST_ORDER]);
    // Green holds the flag, the session and the field still, so two captures of the same package
    // differ by what was redrawn rather than by the lap.
    expect('help' in opts ? '' : opts.scenario).toBe('green');
  });

  test('lets two laps go by first, as shots does, and takes a number for it', () => {
    const dflt = parseArgs([]);
    expect('help' in dflt ? -1 : dflt.warmLaps).toBe(2);
    const none = parseArgs(['--warm-laps', '0']);
    expect('help' in none ? -1 : none.warmLaps).toBe(0);
  });

  test('reads --packages, --scenario and the two flags', () => {
    const opts = parseArgs(['--packages', 'OpenDash, OpenDash 850x480', '--scenario=race', '--keep', '--no-build']);
    expect('help' in opts ? [] : opts.packages).toEqual(['OpenDash', 'OpenDash 850x480']);
    expect('help' in opts ? '' : opts.scenario).toBe('race');
    expect('help' in opts ? false : opts.keep).toBe(true);
    expect('help' in opts ? false : opts.noBuild).toBe(true);
  });

  test('--help is a request for the usage text and nothing else', () => {
    expect(parseArgs(['--help'])).toEqual({ help: true });
  });
});

describe('previewSize', () => {
  test('a landscape face is sized by its height, as DashStudio sizes its own', () => {
    expect(previewSize(850, 480)).toEqual({ width: 531, height: PREVIEW_HEIGHT });
    expect(previewSize(1920, 1080)).toEqual({ width: 533, height: PREVIEW_HEIGHT });
  });

  test('a portrait screen is sized by its width instead, so SimHub never stretches it back up', () => {
    // SimHub decodes the list's thumbnail at 200 px wide. A 480 by 800 companion scaled to 300 tall
    // would be 180 wide and would then be enlarged to 200, which is a blurred thumbnail for nothing.
    const portrait = previewSize(480, 800);
    expect(portrait.width).toBe(PREVIEW_MIN_WIDTH);
    expect(portrait.height).toBe(333);
  });

  test('a capture already small enough is left exactly as it is', () => {
    expect(previewSize(480, 200)).toEqual({ width: 480, height: 200 });
    expect(previewSize(200, 300)).toEqual({ width: 200, height: 300 });
  });
});

describe('mergePreviewRun', () => {
  const record = (commit: string) => ({ version: '0.3.0', commit, dirty: false, date: '2026-09-22', scenario: 'green', lapsSeen: 2, width: 531, height: 300 });

  test('a refresh of one package leaves what every other package said alone', () => {
    // `bun run previews` is usually pointed at the one face that changed, and a run's provenance is
    // only true of the pictures that run took.
    const before = { schema: 1 as const, previews: { OpenDash: record('aaaaaaa'), 'OpenDash 850x480': record('aaaaaaa') } };
    const after = mergePreviewRun(before, { 'OpenDash 850x480': record('bbbbbbb') });
    expect(after.previews['OpenDash']!.commit).toBe('aaaaaaa');
    expect(after.previews['OpenDash 850x480']!.commit).toBe('bbbbbbb');
  });

  test('the file is written in a stable order, so a refresh is a readable diff', () => {
    const merged = mergePreviewRun({ schema: 1, previews: { 'OpenDash 850x480': record('a') } }, { OpenDash: record('b'), 'OpenDash 480 round': record('b') });
    expect(Object.keys(merged.previews)).toEqual(['OpenDash', 'OpenDash 480 round', 'OpenDash 850x480']);
  });
});
