/**
 * The row the AiM's pit banner cuts out of zone C goes whole, on every face in both arrangements
 * (#1165).
 *
 * The AiM lays the house's pit banner over the top 30 px of zone C, where a row of the relative is
 * 64, so a driver in the box read the lower half of the first row under the banner: a number, a name
 * and a gap with no tops, and nothing to say they were the car ahead. The face now draws the zone's
 * ground over the row each page loses to the banner while the banner's row is in use, and this file
 * evaluates that the way SimHub would: zone C is set to each page in turn, each state of the pit
 * family is brought out and so is low fuel, which holds the same row, and what the face covers is the
 * banner and the grounds shown in that frame. Under it every figure of every page is whole, wholly
 * covered or wholly below, and so is every part of the relative, its rows' grounds included.
 *
 * A drawing that is the page itself, a radar, a map, a trace or a car, runs under the banner as it
 * would under any box, and is left: blanking it would leave zone C empty for a stop.
 *
 * The AiM's figures are set in its segment faces, so this is measured in a process drawing them, as
 * `themeFuelPage.test.ts` does: the default process starts this file again with the AiM's colours.
 */
import { describe, expect, test } from 'bun:test';
import { FUEL_POP_UP } from '../src/components/popUp.ts';
import { FACE_SIZES, pagesForZone, zonePageSettingName } from '../src/contract.ts';
import { bottom, right, translate } from '../src/design/geometry.ts';
import { inkOf } from '../src/design/metrics.ts';
import type { Item, LayerItem, Rect } from '../src/generator.ts';
import { regionRect, zoneRect, type Regions } from '../src/themes/anatomy.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { kindOf, regionsWithoutRevBar, zoneDashboardName } from '../src/zones/index.ts';
import { evalNcalc, type Calls, type Props } from './ncalcEval.ts';

const AIM = 'aim';
const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const G = 'DataCorePlugin.GameData';

/** The properties the pit family reads, in one state of it; the in-car limiter control is present. */
const car = (lane: boolean, limiter: boolean, ignition = true, started = ignition): Props => ({
  [`${G}.IsInPitLane`]: lane ? 1 : 0,
  [`${G}.PitLimiterOn`]: limiter ? 1 : 0,
  [`${G}.EngineIgnitionOn`]: ignition ? 1 : 0,
  [`${G}.EngineStarted`]: started ? 1 : 0,
  'DataCorePlugin.GameRawData.Telemetry.dcPitSpeedLimiterToggle': 1,
});

/** Every property an expression reads, set to 1. */
const reads = (expression: string): Props => Object.fromEntries([...expression.matchAll(/\[([A-Za-z0-9_.]+)\]/g)].map((m) => [m[1]!, 1]));

/** The moments the banner's row is in use: each state of the pit family, and low fuel on the circuit. */
const IN_USE: { moment: string; frame: Props }[] = [
  { moment: 'ENGINE OFF', frame: car(true, false, false) },
  { moment: 'START ENGINE', frame: car(true, false, true, false) },
  { moment: 'ENGAGE LIMITER', frame: car(true, false) },
  { moment: 'DISENGAGE LIMITER', frame: car(false, true) },
  { moment: 'PIT LIMITER', frame: car(true, true) },
  { moment: 'low fuel', frame: { ...car(false, false), ...reads(FUEL_POP_UP.when), 'DataCorePlugin.Computed.Fuel_RemainingLaps': 0.5 } },
];

const WINDOWS: Calls = { changed: () => true, isincreasing: () => false, isdecreasing: () => false };
const shown = (item: Item, frame: Props): boolean => Boolean(evalNcalc(String(item.bindings?.Visible?.formula ?? 'true'), frame, WINDOWS));

/** Whether a run from `top` to `foot` is cut by a horizontal edge at `y`. */
const cut = (r: Rect, y: number): boolean => r.top < y && bottom(r) > y;

if (drawsInThisProcess(AIM)) {
  describe('on the AiM, the row the pit banner cuts out of zone C goes whole', () => {
    for (const size of THEMES[AIM]!.anatomy.sizes) {
      const at = `${size.width}x${size.height}`;
      test(at, () => {
        const face = FACE_SIZES.find((f) => f.width === size.width && f.height === size.height)!;
        const pageProp = `OpenDash.${zonePageSettingName(face, 'C')}`;
        const { regions, built } = buildThemeFace(AIM, size, OPTS);
        const arrangements: [string, Regions][] = [
          ['Main', regions],
          ['Main, rev bar off', regionsWithoutRevBar(regions)],
        ];
        for (const [screenName, arranged] of arrangements) {
          const screen = built.main.screens.find((s) => s.name === screenName)!;
          const banner = regionRect(arranged, 'pitAlert');
          const zone = zoneRect(arranged, 'C');
          const dashboard = built.zones.find((d) => d.name === zoneDashboardName(kindOf('C'), { width: zone.width, height: zone.height }))!;
          const names = screen.items.map((item) => item.name);
          const layer = screen.items.find((item): item is LayerItem => item.kind === 'layer' && item.name === 'bannerRow');
          const where = { face: at, screen: screenName };
          /** The grounds the face draws over zone C in a frame, none where it draws no layer of them. */
          const masksIn = (frame: Props): Exclude<Item, LayerItem>[] =>
            layer && shown(layer, frame) ? layer.children.filter((mask): mask is Exclude<Item, LayerItem> => mask.kind !== 'layer' && shown(mask, frame)) : [];

          for (const page of pagesForZone('C')) {
            const drawn = dashboard.screens.find((s) => s.name === page.id)!;
            const parts = [...walkItems(drawn.items)]
              .flatMap((item) => (item.kind === 'layer' ? [] : [{ item, drawn: translate(item.kind === 'text' ? inkOf(item) : item.rect, zone.left, zone.top) }]))
              .filter(({ drawn: r }) => r.left < right(banner) && right(r) > banner.left);

            for (const { moment, frame: car_ } of IN_USE) {
              const frame = { ...car_, [pageProp]: page.number };
              const at_ = { ...where, page: page.id, moment };
              const masks = masksIn(frame);
              expect({ ...at_, masks: masks.length <= 1 }).toEqual({ ...at_, masks: true });
              const edge = Math.max(bottom(banner), ...masks.map((mask) => bottom(mask.rect)));

              // No figure of any page is cut by the edge of what the face covers.
              const figures = parts.filter(({ item, drawn: r }) => item.kind === 'text' && cut(r, edge)).map(({ item, drawn: r }) => `${item.name} ${r.top}-${bottom(r)}`);
              expect({ ...at_, edge, cut: figures }).toEqual({ ...at_, edge, cut: [] });

              // And no part of the relative, the grounds of its rows included; the page's own ground is
              // the zone's and is not a part.
              if (page.id === 'relative') {
                const relative = parts
                  .filter(({ item, drawn: r }) => item.name.startsWith('relative.') && r.height < zone.height && cut(r, edge))
                  .map(({ item, drawn: r }) => `${item.name} ${r.top}-${bottom(r)}`);
                expect({ ...at_, edge, cut: relative }).toEqual({ ...at_, edge, cut: [] });
              }
            }

            // Off those moments the zone is drawn whole: nothing of the face is over it.
            const onCircuit = { ...car(false, false), [pageProp]: page.number };
            expect({ ...where, page: page.id, masks: masksIn(onCircuit).length }).toEqual({ ...where, page: page.id, masks: 0 });
          }

          // The grounds are drawn over zone C's widget and under the flags and the banner, which are drawn
          // over them, each the banner's columns from the banner's top.
          expect({ ...where, afterZone: names.indexOf('bannerRow') > names.indexOf('zoneC'), beforeFlag: names.indexOf('bannerRow') < names.indexOf('flag') }).toEqual({ ...where, afterZone: true, beforeFlag: true });
          for (const mask of layer?.children ?? []) {
            if (mask.kind === 'layer') throw new Error(`${mask.name} is a layer`);
            expect({ ...where, mask: mask.name, left: mask.rect.left, top: mask.rect.top, width: mask.rect.width }).toEqual({ ...where, mask: mask.name, left: banner.left, top: banner.top, width: banner.width });
          }
        }
      });
    }
  });
}

if (THEME_ID === DEFAULT_THEME_ID && !drawsInThisProcess(AIM)) {
  test(
    "the AiM's banner row, in a process drawing its colours",
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: AIM }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ theme: AIM, exit: run.exitCode, output }).toEqual({ theme: AIM, exit: 0, output: '' });
    },
    120_000,
  );
}
