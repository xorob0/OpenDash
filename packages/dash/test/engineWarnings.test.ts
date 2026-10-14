/**
 * The engine warnings light on a fault and not on a stopped engine (#1115).
 *
 * iRacing raises the oil-pressure bit of `EngineWarnings` on an engine that is merely stopped as
 * readily as on one that is failing. The LED strip gated it on the engine turning; the dash surfaces
 * read it alone, so the AiM's ENGINE box, band D's engine lamp and the Porsche's oil and warning
 * telltales were red in every garage and after every stall while the strip on the same rig was dark.
 * Every one of them now reads the bit through `engineWarning` in `zones/telltales.ts`, which gates it
 * on `engineRunning` in `second/values.ts`, the strip's own gate.
 *
 * Each surface is evaluated as it ships, against the same two frames: the bit set with the engine at
 * 0 rpm, where all of them are dark, and the bit set at 3000 rpm, where all of them are lit. A theme's
 * colours are its own, so the AiM and the Porsche are built in a process drawing them, as
 * `themeFuelPage.test.ts` builds them.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, RectangleItem } from '../src/generator.ts';
import { SIDE_EFFECTS } from '../src/leds/effects.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { ds, THEME_ID } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';
import { bandPageItems } from '../src/zones/bandPages.ts';
import { ENGINE_WARNING_BITS, TELLTALE_PAGE, TELLTALES } from '../src/zones/telltales.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };

/** A driver in the car, `EngineWarnings` reading `bits`, the engine at `rpm`. */
const frame = (bits: number, rpm: number): Props => ({
  'DataCorePlugin.GameRawData.Telemetry.EngineWarnings': bits,
  'DataCorePlugin.GameData.Rpms': rpm,
  'DataCorePlugin.GameRawData.Telemetry.IsOnTrack': true,
});

const { oilPressure, waterTemperature } = ENGINE_WARNING_BITS;
/** The oil-pressure bit in the pit box with the engine off, and on an engine that is turning. */
const STOPPED = frame(oilPressure, 0);
const RUNNING = frame(oilPressure, 3000);

const formulaOf = (item: Item, binding: 'Visible' | 'Text'): string => {
  const bound = item.bindings?.[binding];
  if (bound?.mode !== 'formula' || typeof bound.formula !== 'string') throw new Error(`${item.name} has no ${binding} formula`);
  return bound.formula;
};
const holds = (expression: string, props: Props): boolean => Boolean(evalNcalc(expression, props));

if (THEME_ID === DEFAULT_THEME_ID) {
  describe("the house's engine warnings", () => {
    const engine = TELLTALES.find((lamp) => lamp.id === 'engine')!;

    test("band D's engine lamp is dark on a stopped engine and lit on a turning one", () => {
      expect({ stopped: holds(engine.on!, STOPPED), running: holds(engine.on!, RUNNING) }).toEqual({ stopped: false, running: true });
    });

    test('the lamp as the band draws it takes the same answer, in its edge', () => {
      const band = { left: 0, top: 420, width: 1280, height: 60 };
      const chip = [...walkItems(bandPageItems(TELLTALE_PAGE, band, '', true))].find((item): item is RectangleItem => item.kind === 'rect' && item.name === 'engine.chip')!;
      const edge = chip.border!.colorBinding!.formula as string;
      expect({ stopped: evalNcalc(edge, STOPPED), running: evalNcalc(edge, RUNNING) }).toEqual({ stopped: ds.color.surface.raised, running: ds.purpose.telltale.danger });
    });

    test('a water temperature warning is not the engine stopping, and lights the lamp at any speed', () => {
      expect(holds(engine.on!, frame(waterTemperature, 0))).toBe(true);
    });

    test("the strip's oil-pressure lamp agrees with the band", () => {
      const oil = SIDE_EFFECTS.find((effect) => effect.id === 'oilPressure')!;
      expect({ stopped: holds(oil.when, STOPPED), running: holds(oil.when, RUNNING) }).toEqual({ stopped: false, running: true });
    });
  });
}

/** Every item of every screen of a theme's every face whose name `wanted` accepts. */
function shippedItems(id: string, wanted: (name: string) => boolean): Item[] {
  return THEMES[id]!.anatomy.sizes.flatMap((size) => {
    const { built } = buildThemeFace(id, size, OPTS);
    return [built.main, ...built.zones].flatMap((dashboard) => dashboard.screens.flatMap((screen) => [...walkItems(screen.items)].filter((item) => wanted(item.name))));
  });
}

if (THEME_ID === 'aim') {
  describe("the AiM's engine warning", () => {
    const layers = shippedItems('aim', (name) => name === 'warning');
    const readings = shippedItems('aim', (name) => name === 'warning.reading');

    test('is drawn on every face', () => {
      expect(layers.length).toBeGreaterThan(0);
      expect(readings.length).toBe(layers.length);
    });

    test('is not drawn on a stopped engine with the oil-pressure bit set, and is on a turning one', () => {
      for (const layer of layers) {
        const visible = formulaOf(layer, 'Visible');
        expect({ stopped: holds(visible, STOPPED), running: holds(visible, RUNNING) }).toEqual({ stopped: false, running: true });
      }
    });

    test('names the water temperature on a stopped engine rather than an oil pressure the engine is not making', () => {
      for (const reading of readings) {
        // The segment font's word space is two cells, so the words are read apart from it (#751).
        const words = (rpm: number): string => String(evalNcalc(formulaOf(reading, 'Text'), frame(oilPressure + waterTemperature, rpm))).replace(/ +/g, ' ');
        expect({ stopped: words(0), running: words(3000) }).toEqual({
          stopped: 'WATER TEMP HIGH',
          running: 'OIL PRESS LOW',
        });
      }
    });
  });
}

if (THEME_ID === 'porsche') {
  describe("the Porsche's telltales", () => {
    for (const id of ['warning', 'oil']) {
      test(`the ${id} pictogram is dim on a stopped engine with the oil-pressure bit set, and lit on a turning one`, () => {
        const lit = shippedItems('porsche', (name) => name.endsWith(`telltales.${id}.lit`));
        const dim = shippedItems('porsche', (name) => name.endsWith(`telltales.${id}.dim`));
        expect(lit.length).toBeGreaterThan(0);
        expect(dim.length).toBe(lit.length);
        for (const item of lit) {
          const visible = formulaOf(item, 'Visible');
          expect({ stopped: holds(visible, STOPPED), running: holds(visible, RUNNING) }).toEqual({ stopped: false, running: true });
        }
        for (const item of dim) {
          const visible = formulaOf(item, 'Visible');
          expect({ stopped: holds(visible, STOPPED), running: holds(visible, RUNNING) }).toEqual({ stopped: true, running: false });
        }
      });
    }
  });
}

if (THEME_ID === DEFAULT_THEME_ID) {
  for (const id of ['aim', 'porsche'].filter((each) => !drawsInThisProcess(each))) {
    test(
      `the ${id} theme's engine warnings, in a process drawing its colours`,
      () => {
        const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: id }, stdout: 'pipe', stderr: 'pipe' });
        const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
        expect({ theme: id, exit: run.exitCode, output }).toEqual({ theme: id, exit: 0, output: '' });
      },
      120_000,
    );
  }
}
