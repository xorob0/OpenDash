/**
 * The AiM draws the pit view's car as an outline with its cockpit at every size (#1183).
 *
 * The LCD pass turns a filled box holding text into a highlight, the box in the ink. The pit view
 * draws two layouts, one while the sim publishes the pit service and one while it does not, each a
 * layer whose Visible is the other's negated, so only one is ever shown; but on the 800x286 face with
 * the rev bar on, the unpublished layout's pit-time caption is centred inside the published layout's
 * car body, and the pass counted it, so the body came out a solid block of ink and the car a black
 * silhouette with no cockpit. A text on a layer that is out while the box is in no longer counts.
 *
 * The AiM's colours are its own, so this runs in a process drawing them: the default process starts
 * this file again with the AiM's theme, as `aimBannerRow.test.ts` does.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, RectangleItem, TextItem } from '../src/generator.ts';
import { ncalc } from '../src/generator.ts';
import { withMoreBindings } from '../src/bind.ts';
import { lcd } from '../src/themes/aim/lcd.ts';
import { lcdColour } from '../src/themes/aim/register.ts';
import { buildThemeFace, drawsInThisProcess } from '../src/themes/faces.ts';
import { DEFAULT_THEME_ID, THEME_ENV, THEMES } from '../src/themes/index.ts';
import { ds, THEME_ID, TRANSPARENT } from '../src/tokens.ts';
import { walkItems } from '../src/walk.ts';

const AIM = 'aim';
const OPTS = { version: '0.0.0-test', simHubVersion: '9.12.6', author: 'test' };
const BODY = 'pitView.car.body';

/** Every pit view car body of the face, by the zone dashboard it is drawn in. */
function bodies(size: { width: number; height: number }): Map<string, RectangleItem> {
  const face = buildThemeFace(AIM, size, OPTS);
  const found = new Map<string, RectangleItem>();
  for (const dashboard of [face.built.main, ...face.built.zones]) {
    for (const screen of dashboard.screens) {
      for (const item of walkItems(screen.items)) if (item.kind === 'rect' && item.name.endsWith(BODY)) found.set(dashboard.name, item);
    }
  }
  return found;
}

/** How a body is drawn: its ground and the ink round it, the corner's radius aside, since it scales with the car. */
const drawn = (body: RectangleItem) => {
  const { radius: _radius, ...border } = body.border ?? {};
  return { backgroundColor: body.backgroundColor, border };
};

if (drawsInThisProcess(AIM)) {
  describe("the AiM's pit view car", () => {
    test('is an outline at 276x152, as at 276x220, on the 800x286 face', () => {
      const found = bodies({ width: 800, height: 286 });
      const narrow = found.get('zoneface-module-276x152');
      const tall = found.get('zoneface-module-276x220');
      expect({ narrow: narrow !== undefined, tall: tall !== undefined }).toEqual({ narrow: true, tall: true });
      expect(narrow!.backgroundColor).toBe(TRANSPARENT);
      expect(drawn(narrow!)).toEqual(drawn(tall!));
    });

    for (const size of THEMES[AIM]!.anatomy.sizes) {
      test(`is never filled at ${size.width}x${size.height}`, () => {
        const filled = [...bodies(size)].filter(([, body]) => body.backgroundColor !== TRANSPARENT).map(([name, body]) => `${name}: ${body.backgroundColor}`);
        expect(filled).toEqual([]);
      });
    }
  });

  describe('the LCD pass', () => {
    const shown = '[DataCorePlugin.GameRunning]';
    const box: RectangleItem = { kind: 'rect', name: 'box', rect: { left: 0, top: 0, width: 200, height: 100 }, backgroundColor: '#FF0000' };
    const caption: TextItem = { kind: 'text', name: 'caption', rect: { left: 50, top: 40, width: 100, height: 20 }, text: 'PIT', font: ds.font.label, fontWeight: 'Bold', fontSize: 14, textColor: '#FFFFFF', hAlign: 'center', vAlign: 'center' };
    const layer = (name: string, when: string, children: Item[]): Item => withMoreBindings<'layer'>({ kind: 'layer', name, children }, { Visible: when });
    const boxOf = (items: Item[]): RectangleItem => [...walkItems(items)].find((item): item is RectangleItem => item.name === 'box')!;

    test('ignores a text on a sibling layer whose Visible excludes the box', () => {
      const out = lcd([layer('in', shown, [box]), layer('out', ncalc.not(shown), [caption])]);
      expect(boxOf(out).backgroundColor).toBe(TRANSPARENT);
    });

    test('still takes a box for a highlight when its text can show with it', () => {
      expect(boxOf(lcd([layer('in', shown, [box]), layer('also', shown, [caption])])).backgroundColor).toBe(lcdColour('ink'));
      expect(boxOf(lcd([layer('in', shown, [box, caption])])).backgroundColor).toBe(lcdColour('ink'));
    });
  });
}

if (THEME_ID === DEFAULT_THEME_ID && !drawsInThisProcess(AIM)) {
  test(
    "the AiM's pit view car, in a process drawing its colours",
    () => {
      const run = Bun.spawnSync([process.execPath, 'test', import.meta.path], { env: { ...process.env, [THEME_ENV]: AIM }, stdout: 'pipe', stderr: 'pipe' });
      const output = run.exitCode === 0 ? '' : `${run.stdout.toString()}${run.stderr.toString()}`;
      expect({ theme: AIM, exit: run.exitCode, output }).toEqual({ theme: AIM, exit: 0, output: '' });
    },
    120_000,
  );
}
