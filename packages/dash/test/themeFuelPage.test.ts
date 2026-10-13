/**
 * What band D's fuel page keeps, for every theme at every size it draws (#899).
 *
 * `bandPages.test.ts` pins the house page band by band. The Porsche's foot and the AiM's draw the same
 * page in their own cells, keeping what the house page keeps in the room they are given, so a cell
 * the house can afford can cost a theme a field without any house test noticing: the fuel time's
 * third minute digit cost the Porsche's foot one field at 850 x 480, two at 800 x 480 and three at
 * 800 x 286, and the AiM's one at 800 x 480 and at 800 x 286, and only a throwaway probe saw it. The
 * author's ruling was that no field is shed for it, and the room was won back by drawing the page's
 * quantities whole from 10 up. This file is that probe kept.
 *
 * `BEFORE` is what each theme kept on `main` at d2b73d70, before the fuel time grew, and is the floor:
 * no theme at no size may keep less. `KEEPS` is what each keeps now, pinned exactly, so that a gain is
 * a line changed here as well as a loss is.
 *
 * A theme with colours of its own cannot be drawn in a process drawing another theme's (`faces.ts`),
 * so the default process runs this file again once per such theme, as `conformance.test.ts` does.
 * The band's rectangle is the one the build sizes the band's dashboard to, with the rev bar on and
 * off, and the page is drawn by the theme's own band page hook, or the house's where it has none.
 */
import { describe, expect, test } from 'bun:test';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { BAND_PAGES, bandPageItems } from '../src/zones/bandPages.ts';
import { regionsWithoutRevBar, ZONE_FACES, zonesOf } from '../src/zones/index.ts';

type Kept = Readonly<Record<string, readonly string[]>>;

const ALL = ['fuel', 'time', 'toEnd', 'laps', 'refuel', 'perLap', 'lastLap'] as const;
const SIX = ALL.slice(0, 6);
const FIVE = ALL.slice(0, 5);
const FOUR = ALL.slice(0, 4);

/** What each theme's fuel page kept on `main` at d2b73d70, size by size: the floor. */
const BEFORE: Readonly<Record<string, Kept>> = {
  default: { '1920x480': ALL, '1280x480': FIVE, '1280x400': SIX, '850x480': SIX, '800x480': SIX, '1280x720': FIVE, '800x286': SIX, '600x686': SIX },
  porsche: { '1920x480': ALL, '1280x480': ALL, '1280x400': ALL, '850x480': SIX, '800x480': SIX, '1280x720': ALL, '800x286': ALL, '600x686': FOUR },
  aim: { '1920x480': ALL, '1280x480': SIX, '1280x400': ALL, '850x480': FOUR, '800x480': FOUR, '1280x720': SIX, '800x286': SIX, '600x686': ['fuel', 'time'] },
};

/** What each keeps now that the quantities are whole from 10 up. */
const KEEPS: Readonly<Record<string, Kept>> = {
  default: { '1920x480': ALL, '1280x480': SIX, '1280x400': ALL, '850x480': ALL, '800x480': ALL, '1280x720': SIX, '800x286': ALL, '600x686': ALL },
  porsche: { '1920x480': ALL, '1280x480': ALL, '1280x400': ALL, '850x480': SIX, '800x480': SIX, '1280x720': ALL, '800x286': ALL, '600x686': FOUR },
  aim: { '1920x480': ALL, '1280x480': ALL, '1280x400': ALL, '850x480': FIVE, '800x480': FOUR, '1280x720': ALL, '800x286': ALL, '600x686': ['fuel', 'time', 'toEnd'] },
};

const THEME_IDS = Object.keys(KEEPS);

/** The fields the theme's fuel page keeps on each band D rectangle the face sizes a dashboard to. */
function keptBy(themeId: string, width: number, height: number): string[][] {
  const theme = THEMES[themeId]!;
  const drawing = THEME_DRAWINGS[themeId] ?? {};
  const house = ZONE_FACES.find((f) => f.width === width && f.height === height)!;
  const layout = drawing.bandCorners === undefined ? house : { ...house, bandCorners: drawing.bandCorners };
  const regions = theme.anatomy.regions(layout);
  const bands = [...zonesOf(layout, regions), ...zonesOf(layout, regionsWithoutRevBar(regions))].filter((z) => z.zone === 'D');
  const ids = BAND_PAGES.fuel!.map((f) => f.id);
  return bands.map(({ size, corners }) => {
    const frame = { left: 0, top: 0, width: size.width, height: size.height };
    const items = drawing.bandPage ? drawing.bandPage('fuel', frame, 'fuel.') : bandPageItems('fuel', frame, 'fuel.', corners);
    const names = [...walkItems(items)].map((item) => item.name);
    return ids.filter((id) => names.some((name) => name === `fuel.${id}` || name.startsWith(`fuel.${id}.`)));
  });
}

test('the tables name every shipped theme and every size each one draws', () => {
  expect(Object.keys(BEFORE)).toEqual(THEME_IDS);
  for (const id of THEME_IDS) {
    const sizes = THEMES[id]!.anatomy.sizes.map((s) => `${s.width}x${s.height}`);
    expect({ id, sizes: Object.keys(KEEPS[id]!) }).toEqual({ id, sizes });
    expect({ id, sizes: Object.keys(BEFORE[id]!) }).toEqual({ id, sizes });
  }
});

for (const id of THEME_IDS.filter(drawsInThisProcess)) {
  describe(`the ${id} theme's band D fuel page`, () => {
    for (const size of THEMES[id]!.anatomy.sizes) {
      const at = `${size.width}x${size.height}`;
      test(`at ${at} keeps every field it kept before #899, and exactly the ones pinned`, () => {
        for (const kept of keptBy(id, size.width, size.height)) {
          expect({ id, at, lost: BEFORE[id]![at]!.filter((f) => !kept.includes(f)) }).toEqual({ id, at, lost: [] });
          expect({ id, at, kept }).toEqual({ id, at, kept: [...KEEPS[id]![at]!] });
        }
      });
    }
  });
}

if (THEME_ID === DEFAULT_THEME_ID) {
  for (const id of THEME_IDS.filter((each) => !drawsInThisProcess(each))) {
    test(
      `the ${id} theme's band D fuel page, in a process drawing its colours`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: id }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ theme: id, exit: run.exitCode, output }).toEqual({ theme: id, exit: 0, output: '' });
      },
      120_000,
    );
  }
}
