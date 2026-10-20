/**
 * The leaderboard, the relative and the radar say SimHub reads no other car rather than draw a field
 * of one (#1131). On Assetto Corsa without SimHub's in-game app, and on ACC without its broadcasting,
 * SimHub's opponents are the player alone, named `Me`; the plugin's `FieldReported` says so, and each
 * module is built both ways with that capability deciding which half is on the screen. These tests
 * evaluate the bindings the build wrote, against the frame a driver in that game produces.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, TextItem } from '../src/generator.ts';
import { rect } from '../src/design/geometry.ts';
import { LEADERBOARD_FIELD_MESSAGE, leaderboard } from '../src/modules/leaderboard.ts';
import { RADAR_FIELD_MESSAGE, radar } from '../src/modules/radar.ts';
import { RELATIVE_FIELD_MESSAGE, relative } from '../src/modules/relative.ts';
import type { Module } from '../src/modules/module.ts';
import { walkItems } from '../src/walk.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const FIELD = 'OpenDash.FieldReported';
/** A race under way, which is what the session gate in front of the leaderboard and the relative asks. */
const RACING: Props = { 'DataCorePlugin.GameRunning': 1, 'DataCorePlugin.GameData.SessionTypeName': 'Race' };

const visible = (item: Item, props: Props): unknown => {
  const binding = item.bindings?.Visible;
  if (binding === undefined) return true;
  if (binding.mode !== 'formula') throw new Error(`${item.name} has no Visible formula`);
  return evalNcalc(binding.formula as string, props);
};

/** What the module shows a driver: whether its drawing is up, and the notice's words while they are. */
function read(module: Module, id: string, props: Props) {
  const all = [...walkItems(module.build({ frame: rect(0, 0, 600, 300), density: 'companion', prefix: `${id}.` }))];
  const drawing = all.find((i) => i.name === `${id}.field.reported`);
  if (drawing === undefined) throw new Error(`no item ${id}.field.reported`);
  const notice = all.filter((i): i is TextItem => i.kind === 'text' && i.name.startsWith(`${id}.field.placeholder`));
  expect(notice.length).toBeGreaterThan(0);
  return {
    drawing: visible(drawing, props),
    notice: notice.every((i) => visible(i, props) === true) ? notice.map((i) => i.text).join(' ') : null,
  };
}

const MODULES = [
  { id: 'leaderboard', module: leaderboard, message: LEADERBOARD_FIELD_MESSAGE },
  { id: 'relative', module: relative, message: RELATIVE_FIELD_MESSAGE },
  { id: 'radar', module: radar, message: RADAR_FIELD_MESSAGE },
];

describe('a module that draws the field says when SimHub reads none', () => {
  test('the leaderboard, on a frame whose field is not reported, shows the notice and not the board', () => {
    const shown = read(leaderboard, 'leaderboard', { ...RACING, [FIELD]: false });
    expect(shown.drawing).toBe(false);
    expect(shown.notice).toBe(LEADERBOARD_FIELD_MESSAGE);
    expect(LEADERBOARD_FIELD_MESSAGE).toBe("Leaderboard · Other cars need SimHub's AC app or ACC broadcasting");
  });

  for (const { id, module, message } of MODULES) {
    test(`the ${id} says it with the capability false`, () => {
      const shown = read(module, id, { ...RACING, [FIELD]: false });
      expect(shown.drawing).toBe(false);
      expect(shown.notice).toBe(message);
    });

    test(`the ${id} draws the field with the capability true, or with no plugin to say`, () => {
      for (const props of [{ ...RACING, [FIELD]: true }, RACING]) {
        const shown = read(module, id, props);
        expect(shown.drawing).toBe(true);
        expect(shown.notice).toBeNull();
      }
    });
  }
});
