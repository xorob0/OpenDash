/**
 * The incident count and the wheel angle, on a sim that publishes them and on one that does not (#567).
 *
 * Both are iRacing's raw telemetry, `Telemetry.PlayerCarMyIncidentCount` and `Telemetry.SteeringWheelAngle`,
 * which SimHub's Assetto Corsa, ACC and Le Mans Ultimate readers never publish. Each was read behind an
 * `isnull(..., 0)`, so on Assetto Corsa the bar, band D's corner, the pit wall header and the session page
 * drew `0x`, a clean sheet nobody awarded, and the steering dial and the pit wall's steering trace drew a
 * wheel held straight through every corner. Now the incident fields hide, or read `--` where a page keeps
 * the field's place, and the dial and the trace hide, the trace saying why.
 *
 * Each surface is read as a rig draws it: a group whose `Visible` is false hides everything in it, and a
 * text shows its `Text` evaluated against the frame. The frames are the committed traces of the `race`
 * scenario, the first frame of each sim's, so this is what the replays draw.
 */
import { describe, expect, test } from 'bun:test';
import { SIMS } from '../../../scripts/sims.ts';
import { frame, readTrace, traceDirOf } from '../../../scripts/trace.ts';
import { BAR_FIELDS, barFieldSettingName, FACE_SIZES, PROPERTY_PREFIX } from '../src/contract.ts';
import { rect } from '../src/design/geometry.ts';
import type { Item } from '../src/generator.ts';
import { MODULES } from '../src/modules/index.ts';
import { TELEMETRY_TRACES, TRACE_NOT_PUBLISHED, tracePanel } from '../src/screens/pitwall.ts';
import { PIT_WALL_HEADER, pitWallHeader } from '../src/screens/pitwallHeader.ts';
import { hasIncidents, hasSteering } from '../src/second/values.ts';
import { bar } from '../src/zones/bar.ts';
import { bandCorners } from '../src/zones/bandPages.ts';
import { ZONE_FACES } from '../src/zones/index.ts';
import { drawingOf } from './moduleItems.ts';
import { evalNcalc, type Props } from './ncalcEval.ts';

const RAW = 'DataCorePlugin.GameRawData.Telemetry.';
const INCIDENTS = `${RAW}PlayerCarMyIncidentCount`;
const STEERING = `${RAW}SteeringWheelAngle`;

const raceFrame = (simId: string): Props => {
  const sim = SIMS.find((s) => s.id === simId);
  if (!sim) throw new Error(`no sim ${simId}`);
  return frame(readTrace('race', traceDirOf(sim)), 0);
};

const formulaOf = (item: Item, target: 'Visible' | 'Text' | 'CurrentValue'): string => {
  const f: unknown = (item.bindings?.[target] as { formula?: unknown } | undefined)?.formula;
  if (typeof f === 'string') return f;
  if (f && typeof f === 'object' && typeof (f as { expression?: unknown }).expression === 'string') return (f as { expression: string }).expression;
  return '';
};

/** What a rig draws of `items` under `props`: each item it shows, by name, with the text a text shows. */
function drawn(items: readonly Item[], props: Props): { name: string; text?: string }[] {
  return items.flatMap((item) => {
    const visible = formulaOf(item, 'Visible');
    if (visible !== '' && evalNcalc(visible, props) !== true) return [];
    if (item.kind === 'layer') return drawn(item.children, props);
    if (item.kind !== 'text') return [{ name: item.name }];
    const text = formulaOf(item, 'Text');
    return [{ name: item.name, text: text === '' ? item.text : String(evalNcalc(text, props)) }];
  });
}

/** The words a rig draws of the items whose names start with `prefix`. */
const words = (items: readonly Item[], props: Props, prefix: string): string[] =>
  drawn(items, props)
    .filter((i) => i.name.startsWith(prefix) && i.text !== undefined && i.text !== '')
    .map((i) => i.text!);

const LAYOUT = ZONE_FACES.find((f) => f.width === 1920 && f.height === 480)!;
const FACE = FACE_SIZES.find((f) => f.width === 1920 && f.height === 480)!;
const INCIDENTS_FIELD = BAR_FIELDS.find((f) => f.id === 'incidents')!.number;

/** The 1920 x 480 bar with the incident count chosen for its first left slot. */
const barWithIncidents = (props: Props): string[] =>
  words(bar(rect(0, 0, FACE.width, 56), 'bar.', { fieldsPerEnd: FACE.barFieldsPerEnd, face: FACE, scale: LAYOUT.bar! }), { ...props, [`${PROPERTY_PREFIX}.${barFieldSettingName(FACE, 'Left1')}`]: INCIDENTS_FIELD }, 'bar.Left1.incidents.');

const BAND = rect(0, 420, 1920, 60);
const header = (): Item[] => pitWallHeader('h', { frame: rect(0, 0, 1920, PIT_WALL_HEADER.height), pageName: 'Pit wall · test', page: 1, pages: 3 });
const sessionPage = (): Item[] => drawingOf(MODULES.find((m) => m.id === 'session')!, { frame: rect(0, 0, 600, 280), density: 'zone', prefix: 'session.' });

/** Every surface that draws the incident count, as a driver reads each. */
const incidentReadings = (props: Props): Record<string, string[]> => ({
  bar: barWithIncidents(props),
  bandCorner: words(bandCorners(BAND, 'corner.'), props, 'corner.incidents.'),
  pitWallHeader: words(header(), props, 'h.incidents.'),
  sessionPage: words(sessionPage(), props, 'session.incidents.'),
});

describe('the incident count', () => {
  test('is published by the iRacing trace and not by the Assetto Corsa one', () => {
    expect({ iracing: raceFrame('iracing')[INCIDENTS], ac: raceFrame('ac')[INCIDENTS] }).toEqual({ iracing: 0, ac: null });
    expect({ iracing: evalNcalc(hasIncidents(), raceFrame('iracing')), ac: evalNcalc(hasIncidents(), raceFrame('ac')) }).toEqual({ iracing: true, ac: false });
  });

  test('draws no 0x on Assetto Corsa: the bar, the band corner and the header hide it, and the session page reads --', () => {
    expect(incidentReadings(raceFrame('ac'))).toEqual({ bar: [], bandCorner: [], pitWallHeader: [], sessionPage: ['Incidents', '--'] });
  });

  test("reads iRacing's count where it always did", () => {
    const iracing = raceFrame('iracing');
    expect(incidentReadings(iracing)).toEqual({ bar: ['Incidents', '0x'], bandCorner: ['Incidents', '0x'], pitWallHeader: ['Inc', '0x', '/ unlimited'], sessionPage: ['Incidents', '0x'] });
    const taken = { ...iracing, [INCIDENTS]: 4, 'DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.IncidentLimit': '17' };
    expect(incidentReadings(taken)).toEqual({ bar: ['Incidents', '4x'], bandCorner: ['Incidents', '4x'], pitWallHeader: ['Inc', '4x', '/ 17'], sessionPage: ['Incidents', '4x'] });
  });

  test("keeps the incident limit hidden with the count, though the limit alone is published", () => {
    const limitOnly = { ...raceFrame('ac'), 'DataCorePlugin.GameRawData.SessionData.WeekendInfo.WeekendOptions.IncidentLimit': '17' };
    expect(words(header(), limitOnly, 'h.incidents.')).toEqual([]);
  });
});

const inputsPage = (): Item[] => MODULES.find((m) => m.id === 'inputs')!.build({ frame: rect(0, 0, 600, 280), density: 'zone', prefix: 'inputs.' });
const steeringPanel = (): Item[] => tracePanel('telemetry.steering', rect(0, 0, 1240, 200), TELEMETRY_TRACES.find((t) => t.id === 'steering')!);

/** What a rig draws of the dial and of the trace panel: the names of the parts shown, and the words. */
const steeringReadings = (props: Props) => {
  const dial = drawn(inputsPage(), props).filter((i) => i.name.startsWith('inputs.steer.'));
  const panel = drawn(steeringPanel(), props);
  return {
    dialParts: dial.length,
    traced: panel.filter((i) => i.name.startsWith('telemetry.steering.trace.Steering')).map((i) => i.name),
    panelWords: panel.filter((i) => i.text !== undefined).map((i) => i.text!),
  };
};

describe('the wheel angle', () => {
  test('is published by the iRacing trace and not by the Assetto Corsa one', () => {
    expect(typeof raceFrame('iracing')[STEERING]).toBe('number');
    expect(raceFrame('ac')[STEERING]).toBeNull();
    expect({ iracing: evalNcalc(hasSteering(), raceFrame('iracing')), ac: evalNcalc(hasSteering(), raceFrame('ac')) }).toEqual({ iracing: true, ac: false });
  });

  test('hides the dial and the trace on Assetto Corsa rather than drawing a wheel held straight, and the panel says why', () => {
    expect(steeringReadings(raceFrame('ac'))).toEqual({ dialParts: 0, traced: [], panelWords: ['Steering', TRACE_NOT_PUBLISHED] });
  });

  test('draws the dial and traces the angle on iRacing, as it did', () => {
    const iracing = raceFrame('iracing');
    const readings = steeringReadings(iracing);
    expect(readings.dialParts).toBeGreaterThan(10);
    expect(readings.traced).toEqual(['telemetry.steering.trace.Steering']);
    expect(readings.panelWords).toEqual(['Steering']);
    const chart = drawn(steeringPanel(), iracing).find((i) => i.name === 'telemetry.steering.trace.Steering')!;
    const item = [...steeringPanel()].flatMap((i) => (i.kind === 'layer' ? i.children : [i])).find((i) => i.name === chart.name)!;
    // Clockwise as the driver sees it, so iRacing's left-positive angle is turned round (#1057).
    expect(evalNcalc(formulaOf(item, 'CurrentValue'), iracing)).toBeCloseTo(-(iracing[STEERING] as number), 6);
  });
});
