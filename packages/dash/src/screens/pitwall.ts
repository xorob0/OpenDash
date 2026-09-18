/**
 * The pit wall: a big screen for someone who is not driving. Three landscape pages (the race, the
 * tower and the telemetry traces) and one portrait page, each built from the same modules the
 * companion uses plus four configurable zones.
 *
 * The leaderboard is one continuous list rather than a set of per-class blocks: SimHub exposes
 * per-class rows only for the player's own class, so class headings would be a picture of data
 * that is not there. The class is a chip on every row instead. What the screen's own setting does
 * offer is the other reading of "by class": `PitWallClassOnly` narrows the list to the player's
 * class rather than grouping the field into blocks, which is the half of it SimHub can answer.
 */
import type { Dashboard, DashboardMetadata, Item, Rect, Screen } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { secondScreen } from '../contract.ts';
import { rect } from '../design/geometry.ts';
import { rule } from '../elements/rule.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { fieldRowFitted, fitFields, rowHeight, type FieldSpec } from '../second/field.ts';
import { panel, PANEL_TITLE_HEIGHT } from '../second/header.ts';
import { centreZeroGauge } from '../second/gauge.ts';
import { sectorFields } from '../second/sectors.ts';
import { rowsThatFit as boardRowsThatFit, table, type ColumnId } from '../second/table.ts';
import { LEGEND_HEIGHT, trace, type Series } from '../second/trace.ts';
import { track, trackFrameWidth } from '../modules/track.ts';
import { fld, type ModuleContext } from '../modules/module.ts';
import { airTemperature, bestLap, brake, carPosition, CHARS, classOpponentCount, clock, clutch, deltaColour, estimatedLap, fieldSize, isTimedSession, lapTime, lastLap, player, playerClass, referenceDelta, referenceLabel, roadTemperature, rpm, sessionBestLap, sessionTimeLeft, sessionType, speed, speedUnit, steering, STEERING_RANGE, throttle } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { PIT_WALL_HEADER, pitWallHeader } from './pitwallHeader.ts';
import { zoneWidget } from './zones.ts';
import type { Expr } from '../bind.ts';

const { fmt, concat, str, iff, eq, gt, num, isnull, ucase, driver, game, signed, raw } = ncalc;

/** A pit wall panel draws at zone density with the pit wall's own label: 24 px numerals, 13 px labels. */
const DENSITY = 'panel' as const;

/** A context for a module drawn inside a pit wall panel. */
const ctxOf = (frame: Rect, prefix: string): ModuleContext => ({ frame, density: DENSITY, prefix });

/**
 * One row of fields across a panel body, drawn from its top edge.
 *
 * `fitFields` is the wrong instrument for a fixed panel. It answers a box that is too short by
 * shedding fields and then by shrinking what survives, which is right for a zone drawing whichever
 * page the driver put in it and wrong here: these fields are what the panel exists to draw, and a
 * Session panel keeping only "Race" has lost its point. So the row is explicit and sheds nothing.
 *
 * It is drawn downwards from the top rather than up from the bottom because the sheet's own panel
 * heights leave out the five pixels between a label and its value: `PitWall1920x1080.dc.html` gives
 * Session 108 where 14 + 13 + 8 + 13 + 46 + 14 comes to 108 only with that gap dropped, and Lap
 * data 96 the same way. A row hung from the bottom would answer those five pixels by climbing into
 * the title; hung from the top it spends the last of the bottom padding instead, which is what the
 * sheet does too, and stays inside the panel frame either way.
 */
function panelRow(body: Rect, specs: readonly FieldSpec[], gap?: number): Item[] {
  return fieldRowFitted(specs, body.left, body.top + rowHeight(specs, DENSITY), body.width, DENSITY, { gap }).items;
}

/**
 * The longest session name the panel's first label can draw, which is what its box is measured by.
 *
 * `pitwallHeader.ts` measures the same list for the same property; the two constants stay apart
 * because neither file owns the other, and `values.ts` is where a third consumer would put it.
 */
const WIDEST_SESSION_LABEL = 'OFFLINE TESTING';

/**
 * The session panel: how long is left, where you are in the field, where you are in your class.
 *
 * Three fields rather than the five this used to draw, and the portrait sheet's rather than the
 * landscape one's. The portrait sheet folds the session type into the label of the time left, which
 * is the reading a pit wall wants, and the lap is in the header of every page already.
 *
 * The landscape sheet still draws five at 46 px, and no arrangement of those fits the panel it
 * declares: measured against their character budgets the five come to 820 px across a 599 px body,
 * and a single 46 px value needs 70 px of a 59 px one. Drawing the portrait set on both pages is
 * therefore a choice between two sheets rather than a deviation from one.
 */
export function sessionPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Session' });
  const classPosition = isnull(driver('classposition', player()), num(0));
  return [
    ...items,
    ...panelRow(body, [
      fld(
        ctxOf(body, `${name}.`),
        'left',
        'Race',
        { sample: '0:42:15', bind: iff(isTimedSession(), clock(sessionTimeLeft()), str('-:--:--')), chars: CHARS.clock, fs: d.mid },
        { labelBind: ucase(sessionType()), labelWidest: WIDEST_SESSION_LABEL },
      ),
      fld(ctxOf(body, `${name}.`), 'position', 'Position', {
        sample: '4',
        bind: fmt(carPosition(player()), '0'),
        chars: CHARS.position,
        fs: d.mid,
        // A denominator rather than a unit: the sheet scales "/ 24" with the value it follows -- 23
        // beside 34, 32 beside the landscape sheet's 46 -- where a unit is the density's 13 px.
        follower: { text: '/ 24', kind: 'denominator', bind: concat(str('/ '), fmt(fieldSize(), '0')) },
      }),
      fld(
        ctxOf(body, `${name}.`),
        'class',
        'Class',
        { sample: 'GT3 · P4', bind: concat(playerClass(), str(' · P'), fmt(classPosition, '0')), chars: CHARS.classPosition, fs: d.mid },
        { visibleBind: gt(classOpponentCount(), num(0)) },
      ),
    ]),
  ];
}

/**
 * The delta bar's width, which is the sheet's and not the panel's.
 *
 * A bar stretched to whatever the body has left is a scale whose graduations move: the quarters
 * mark a second either side of zero, and a reader who has learnt where half a second sits on this
 * bar should find it there on every page that draws one. So the bar is a fixed width and the room
 * beyond it stays background, which is what the artboard does with its own 77 spare pixels.
 */
const DELTA_BAR_WIDTH = 330;

/** The lap delta panel: the live delta, its bar, and the three sectors of the last lap. */
export function lapDeltaPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Lap delta' });
  const value = referenceDelta();
  const deltaField = fld(ctxOf(body, `${name}.`), 'delta', 'VS SESSION BEST', { sample: '\u22120.21', bind: signed(value, '0.00'), chars: CHARS.delta, fs: d.big, colorBind: deltaColour(value) }, {
    labelBind: referenceLabel(),
    labelWidest: 'VS ALL-TIME BEST',
  });
  const deltaWidth = 160;
  // `Panels.dc.html`'s track, which the gauge's own overhangs then turn into 20 px graduations and
  // a 24 px centre marker. The two sheets disagree about this one number and are left disagreeing:
  // the component sheet draws the bar 320 by 12 with 20 px graduations where `PitWall1920x1080`
  // draws it 330 by 10 with 18, and the component sheet is the one that defines the part.
  const barHeight = 12;
  const topHeight = d.label + d.fieldGap + d.big;
  const sectorTop = body.top + topHeight + Math.round(d.gapY / 2);
  const sectorHeight = Math.max(0, body.top + body.height - sectorTop);
  return [
    ...items,
    // Drawn as a row rather than fitted: the box the sheet leaves this field is its label, its gap
    // and its numeral and nothing for the tail the numeral's line box hangs below that, so a fitter
    // measuring the tail found the box short and answered by shrinking the one number the panel
    // exists to show. The tail spends the gap over the sectors instead, as it does on every other
    // panel of the column.
    ...fieldRowFitted([deltaField], body.left, body.top + topHeight, deltaWidth, DENSITY).items,
    // The sheet sets this row wider than a row of fields, at 32 rather than 24: the bar is a second
    // reading of the number beside it and not the next field along.
    ...centreZeroGauge(`${name}.bar`, rect(body.left + deltaWidth + ds.space[6], body.top + topHeight - barHeight - 6, DELTA_BAR_WIDTH, barHeight), value, { range: 2 }),
    ...sectorFields(`${name}.sector.`, rect(body.left, sectorTop, body.width, sectorHeight), DENSITY, d.small),
  ];
}

/**
 * What the lap times of the Lap data panel are set at, which is closer than a row of fields.
 *
 * The portrait sheet's 20 rather than the landscape sheet's 28. The landscape row is four times
 * wide and this panel draws the portrait set of three, so it is drawn at the gap that set is drawn
 * at rather than at one borrowed from a row it is not.
 */
const LAP_DATA_GAP = 20;

/**
 * The lap data panel: what the last lap was, what your best is, what this one is heading for.
 *
 * The session best has gone to the Track panel, which is where both landscape sheets draw it and
 * the only place it was ever read against the board's own purple.
 */
export function lapDataPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Lap data' });
  const ctx = ctxOf(body, `${name}.`);
  return [
    ...items,
    ...panelRow(
      body,
      [
        fld(ctx, 'estimated', 'Est.', { sample: '1:42.1', bind: lapTime(estimatedLap(), 1), chars: CHARS.lapTime, fs: d.mid }),
        fld(ctx, 'yourBest', 'Your best', { sample: '1:42.311', bind: lapTime(bestLap()), chars: CHARS.lapTime, fs: d.mid }),
        fld(ctx, 'last', 'Last', { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs: d.mid }),
      ],
      LAP_DATA_GAP,
    ),
  ];
}

/**
 * What the Track panel's field column is set at, both off the sheet: 20 between the readings of a
 * row, where the rest of a panel gives 24, and 10 between its rows, where `fitFields` would
 * otherwise derive half the density's 12.
 */
const TRACK_FIELD_GAP = 20;
const TRACK_LINE_GAP = 10;

/**
 * The track panel: the map, with the session best, the conditions and the car's assists beside it.
 *
 * Both landscape sheets draw the session best in this panel and draw it larger than the conditions,
 * so it leads the column rather than closing it: a greedy wrap that lists it last puts it on the
 * second line beside the brake bias, which buries the one lap the whole board is read against. The
 * track's name and its surface state are the map's own header row, where `track.ts` already draws
 * them, and are not repeated here. Road comes before Air, as both sheets order them.
 */
export function trackPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Track' });
  // Cut rather than shared out. Both sheets give the map about half the panel -- 300 of 599 on the
  // race page, 520 of 999 on the tower -- and the ratio is what the circuit can actually spend of
  // that half at the height the panel leaves it, so the box is whichever of the two is smaller and
  // the field column keeps the rest. A box wider than the ratio asks for buys no ink.
  const mapWidth = trackFrameWidth(body.height, DENSITY, Math.floor(body.width / 2));
  const right = rect(body.left + mapWidth + d.gapX, body.top, Math.max(0, body.width - mapWidth - d.gapX), body.height);
  const ctx = ctxOf(right, `${name}.`);
  return [
    ...items,
    ...track.build({ frame: rect(body.left, body.top, mapWidth, body.height), density: DENSITY, prefix: `${name}.map.` }),
    ...fitFields(
      [
        fld(ctx, 'sessionBest', 'Session best', { sample: '1:41.877', bind: lapTime(sessionBestLap()), chars: CHARS.lapTime, fs: d.mid, color: ds.purpose.lap.sessionBest }),
        fld(ctx, 'road', 'Road', { sample: '31', bind: fmt(roadTemperature(), '0'), chars: CHARS.temperature, fs: d.small, follower: { text: '°' } }),
        fld(ctx, 'air', 'Air', { sample: '24', bind: fmt(airTemperature(), '0'), chars: CHARS.temperature, fs: d.small, follower: { text: '°' } }),
        // Each of the three hides where the car has no such control, which is the rule the settings
        // bar and the car settings page both apply to the same readings: SimHub normalises traction
        // control and ABS into `TCLevel` and `ABSLevel` and reports 0 for a car with neither, so a
        // cell drawn unconditionally says the dial is turned off where there is no dial. The test is
        // the raw iRacing field behind each, which is absent rather than zero.
        fld(ctx, 'tc', 'TC', { sample: '3', bind: fmt(isnull(game('TCLevel'), num(0)), '0'), chars: CHARS.setting, fs: d.small }, { visibleBind: present(raw('dcTractionControl')) }),
        fld(ctx, 'abs', 'ABS', { sample: '2', bind: fmt(isnull(game('ABSLevel'), num(0)), '0'), chars: CHARS.setting, fs: d.small }, { visibleBind: present(raw('dcABS')) }),
        fld(ctx, 'bb', 'BB', { sample: '54.2', bind: fmt(isnull(game('BrakeBias'), num(0)), '0.0'), chars: CHARS.setting, fs: d.small }, { visibleBind: present(game('BrakeBias')) }),
      ],
      right,
      DENSITY,
      { gap: TRACK_FIELD_GAP, lineGap: TRACK_LINE_GAP },
    ),
  ];
}

/**
 * Gears the trace's axis spans, reverse at the floor.
 *
 * A ChartItem's Maximum is a number in the file and not an expression, so the top cannot follow
 * the car's own `DriverCarGearNumForward`; eight forward gears is above everything these sims
 * publish, and a car with fewer simply never reaches the top of the plot. Autoscaling the top
 * instead would move third gear up and down the plot as the window turned over, which on a screen
 * read from a metre away is worse than unused headroom.
 */
const GEAR_RANGE = { min: -1, max: 8 } as const;

/**
 * The gear as a number, which is what a ChartItem samples.
 *
 * SimHub's `[Gear]` is a string ("R", "N", "1"), so it is mapped here. The numeric
 * `GameRawData.Telemetry.Gear` that `shift.ts` reads would be one line instead, but only iRacing
 * publishes it and every other sim would then trace a flat line at neutral, which is a picture of
 * data that is not there. Neutral and anything unrecognised fall to zero.
 */
/** True while the sim publishes this reading at all, which is how a cell without a control hides. */
const present = (expr: Expr): Expr => ncalc.not(ncalc.isNull(expr));

const gearNumber = (): Expr =>
  Array.from({ length: GEAR_RANGE.max }, (_, i) => i + 1).reduce<Expr>(
    (fallback, g) => iff(eq(game('Gear'), str(String(g))), num(g), fallback),
    iff(eq(game('Gear'), str('R')), num(GEAR_RANGE.min), num(0)),
  );

/**
 * The five traces of the telemetry page, top to bottom, each with the plot height its artboard
 * declares. The heights are declared rather than shared out over the column, because the canvas
 * gives the speed and pedal plots twice the gear plot and a column divided by weight gave every
 * panel whatever was left over.
 */
export const TELEMETRY_TRACES: { id: string; title: string; titleBind?: Expr; titleWidest?: string; series: () => Series[]; plot: number }[] = [
  // The speed trace carries its unit in the title, which is where the sheet puts it, and the unit
  // is the driver's own rather than a fixed word: the value under it is `SpeedLocal`, so a title
  // reading kilometres over a miles rig would be an engineer reading the wrong number off a graph.
  {
    id: 'speed',
    title: 'Speed · km/h',
    titleBind: concat(str('SPEED · '), ucase(speedUnit())),
    titleWidest: 'SPEED · KM/H',
    plot: 180,
    series: () => [{ name: 'Speed', color: ds.color.text.primary, bind: speed(), min: 0, max: 300 }],
  },
  { id: 'rpm', title: 'RPM', plot: 130, series: () => [{ name: 'RPM', color: ds.color.text.primary, bind: rpm(), min: 0, useMaximum: false }] },
  // The canvas draws the gear as flat runs with a vertical step between them. A ChartItem is a ring
  // buffer drawn oldest to newest with a straight line between consecutive samples, and it has no
  // step mode, so each change of gear is drawn as a ramp one sample wide rather than as a riser.
  // At this plot's ~207 samples that ramp is about six pixels, which is the honest limit; nothing
  // here can close it, and faking it would need a second series per gear.
  { id: 'gear', title: 'Gear', plot: 90, series: () => [{ name: 'Gear', color: ds.color.text.primary, bind: gearNumber(), min: GEAR_RANGE.min, max: GEAR_RANGE.max }] },
  {
    id: 'pedals',
    title: 'Throttle, brake and clutch · %',
    plot: 180,
    series: () => [
      { name: 'Throttle', color: ds.purpose.delta.faster, bind: throttle(), min: 0, max: 100 },
      { name: 'Brake', color: ds.purpose.delta.slower, bind: brake(), min: 0, max: 100 },
      { name: 'Clutch', color: ds.color.text.secondary, bind: clutch(), min: 0, max: 100 },
    ],
  },
  { id: 'steering', title: 'Steering', plot: 110, series: () => [{ name: 'Steering', color: ds.color.text.primary, bind: steering(), min: -STEERING_RANGE, max: STEERING_RANGE }] },
];

/**
 * What a trace panel spends around its plot, which is what turns a declared plot height into the
 * frame the page has to give it.
 *
 * `panel` pads 14 over the title row and 14 under the body, and `trace` takes a legend row and its
 * gap off the bottom of the body wherever a panel carries more than one series. Both numbers are
 * mirrored here rather than read, because neither module exports them; `telemetryTraces.test.ts`
 * measures the built plots against the canvas so that a change to either is caught here rather
 * than by WPF clipping a legend.
 */
const PANEL_PAD_Y = 14;
const TRACE_LEGEND_ROW = LEGEND_HEIGHT + 4;

/** The frame height a trace panel needs for the plot its spec declares. */
export const tracePanelHeight = (spec: (typeof TELEMETRY_TRACES)[number]): number =>
  spec.plot + 2 * PANEL_PAD_Y + PANEL_TITLE_HEIGHT + (spec.series().length > 1 ? TRACE_LEGEND_ROW : 0);

/** A trace panel: its title and legend, then the plot. */
export function tracePanel(name: string, frame: Rect, spec: (typeof TELEMETRY_TRACES)[number]): Item[] {
  const { items, body } = panel(name, {
    frame,
    title: spec.title,
    ...(spec.titleBind ? { titleBind: spec.titleBind, titleWidest: spec.titleWidest ?? spec.title.toUpperCase() } : {}),
  });
  return [...items, ...trace(`${name}.trace`, body, spec.series(), DENSITY, { legend: spec.series().length > 1 })];
}

/**
 * Columns of each page's board, in the order its artboard heads them.
 *
 * Each list is its artboard's `.th` read left to right, less the three columns nothing can fill.
 * Nat is a flag per country and Licence is the iRacing licence class with its safety rating, both
 * of which live in the session YAML with no per-leaderboard-row reader, and Inc is the same for
 * incident counts; `table.ts` holds a slot for the first two and draws nothing in either, and no
 * page lists them while they are empty. What is left is the canvas's own order: the tower has no
 * Int and no iRating, the portrait board trades the three sector columns for iRating, and the race
 * board keeps its sectors and drops the stint count, which no artboard draws.
 */
export const RACE_COLUMNS: readonly ColumnId[] = ['pos', 'rank', 'num', 'name', 'class', 'rating', 'gap', 'int', 'last', 'best', 's1', 's2', 's3', 'pit', 'tyre'];
export const TOWER_COLUMNS: readonly ColumnId[] = ['pos', 'rank', 'num', 'name', 'class', 'gap', 'last', 'best', 'pit'];
export const PORTRAIT_COLUMNS: readonly ColumnId[] = ['pos', 'rank', 'num', 'name', 'class', 'rating', 'gap', 'int', 'last', 'best', 'pit', 'tyre'];

/** Rows the race and portrait boards stamp: a full grid of cars. */
export const PIT_WALL_ROWS = 24;

/**
 * Rows the tower board stamps, which is the one page that exists to hold the whole field.
 *
 * `Panels.dc.html` reads "33 rows fit the tower page", and at the 28 px row the tower draws they
 * do: thirty-three of them under a 28 px header is 952 of the 1016 px body. A row with no car
 * behind it hides itself, so a grid of twenty-four draws exactly what the other two pages draw and
 * a fuller field is no longer cut at the grid.
 */
export const TOWER_ROWS = 33;

/**
 * The zone column of a landscape page, which both artboards set at 639 px.
 *
 * The board beside it takes what is left rather than the other way about: the zone rectangle is
 * what names a zone dashboard and what every page of the catalogue is then cut for, so it is the
 * fixed half of the split and the board absorbs the odd pixel. `PitWallTelemetry1920x1080` writes
 * the two out as 1279 and 639, which come to 1919 of the 1920 it declares, and giving that pixel
 * to the plot is what leaves the two landscape pages on one split.
 */
const ZONE_COLUMN_WIDTH = 639;

const vRule = (name: string, x: number, top: number, height: number): Item => rule(name, x, top, 1, height);

/** The race page: the field on the left, the driver's own numbers and two zones on the right. */
export function racePage(width: number, height: number): Screen {
  const header = rect(0, 0, width, PIT_WALL_HEADER.height);
  const bodyTop = PIT_WALL_HEADER.height;
  const bodyHeight = height - bodyTop;
  const columnWidth = ZONE_COLUMN_WIDTH;
  const columnLeft = width - columnWidth;
  const boardWidth = columnLeft - 1;
  // The artboard's own four heights. They come to 602 with their rules, which leaves 414 of the
  // 1016 px body for the two zones and the rule between them; the sheet spends 403 there, on zones
  // of 195 and 207, and leaves the last eleven pixels unaccounted.
  //
  // So the two are one rectangle here, a few pixels taller than either. A zone rectangle names the
  // zone dashboard the package carries, and two zones of one column that differ by twelve pixels
  // are a second copy of all twenty-one pages, shedding their rows at different heights for a
  // difference a reader of the column cannot see.
  const panels = [
    { id: 'session', height: 108, draw: sessionPanel },
    { id: 'lapDelta', height: 158, draw: lapDeltaPanel },
    { id: 'lapData', height: 96, draw: lapDataPanel },
    { id: 'track', height: 236, draw: trackPanel },
  ];
  const items: Item[] = [
    ...pitWallHeader('race.header', { frame: header, pageName: 'Pit wall · race', page: 1, pages: 3 }),
    ...table({ name: 'race.board', frame: rect(0, bodyTop, boardWidth, bodyHeight), columns: RACE_COLUMNS, mode: 'full', classOnly: secondScreen.classOnly(), density: DENSITY, rows: PIT_WALL_ROWS, rowHeight: 34, board: true }),
    vRule('race.columnRule', boardWidth, bodyTop, bodyHeight),
  ];
  let y = bodyTop;
  for (const p of panels) {
    items.push(...p.draw(`race.${p.id}`, rect(columnLeft, y, columnWidth, p.height)));
    y += p.height;
    items.push(rule(`race.${p.id}.rule`, columnLeft, y, columnWidth, 1));
    y += 1;
  }
  const zoneHeight = Math.floor((bodyTop + bodyHeight - y - 1) / 2);
  items.push(zoneWidget('race.zoneA', rect(columnLeft, y, columnWidth, zoneHeight), 'standard', 'race', 'A'));
  items.push(rule('race.zoneRule', columnLeft, y + zoneHeight, columnWidth, 1));
  items.push(zoneWidget('race.zoneB', rect(columnLeft, y + zoneHeight + 1, columnWidth, zoneHeight), 'standard', 'race', 'B'));
  return { name: 'race', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items, enabledExpression: secondScreen.pitWallPageIs(0) };
}

/** The tower page: a compact field list, the track, and three zones. */
export function towerPage(width: number, height: number): Screen {
  const bodyTop = PIT_WALL_HEADER.height;
  const bodyHeight = height - bodyTop;
  const boardWidth = 880;
  const columnLeft = boardWidth + 1;
  const columnWidth = width - columnLeft;
  const trackHeight = 400;
  const wideHeight = 255;
  const zonesTop = bodyTop + trackHeight + 1 + wideHeight + 1;
  const zoneHeight = height - zonesTop;
  const zoneWidth = Math.floor((columnWidth - 1) / 2);
  const items: Item[] = [
    ...pitWallHeader('tower.header', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · tower', page: 2, pages: 3 }),
    ...table({ name: 'tower.board', frame: rect(0, bodyTop, boardWidth, bodyHeight), columns: TOWER_COLUMNS, mode: 'full', classOnly: secondScreen.classOnly(), density: DENSITY, rows: TOWER_ROWS, rowHeight: 28, board: true }),
    vRule('tower.columnRule', boardWidth, bodyTop, bodyHeight),
    ...trackPanel('tower.track', rect(columnLeft, bodyTop, columnWidth, trackHeight)),
    rule('tower.trackRule', columnLeft, bodyTop + trackHeight, columnWidth, 1),
    zoneWidget('tower.wide', rect(columnLeft, bodyTop + trackHeight + 1, columnWidth, wideHeight), 'wide', 'tower', 'Wide'),
    rule('tower.wideRule', columnLeft, bodyTop + trackHeight + 1 + wideHeight, columnWidth, 1),
    zoneWidget('tower.zoneA', rect(columnLeft, zonesTop, zoneWidth, zoneHeight), 'standard', 'tower', 'A'),
    vRule('tower.zoneRule', columnLeft + zoneWidth, zonesTop, zoneHeight),
    zoneWidget('tower.zoneB', rect(columnLeft + zoneWidth + 1, zonesTop, zoneWidth, zoneHeight), 'standard', 'tower', 'B'),
  ];
  return { name: 'tower', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items, enabledExpression: secondScreen.pitWallPageIs(1) };
}

/** The telemetry page: the traces of the last minute of driving, and three zones beside them. */
export function telemetryPage(width: number, height: number): Screen {
  const d = densityOf(DENSITY);
  const bodyTop = PIT_WALL_HEADER.height;
  const bodyHeight = height - bodyTop;
  const zoneWidth = ZONE_COLUMN_WIDTH;
  const zoneLeft = width - zoneWidth;
  const plotWidth = zoneLeft - 1;
  const footerHeight = 25;
  const items: Item[] = [...pitWallHeader('telemetry.header', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · telemetry', page: 3, pages: 3 })];
  // The five panels, their rules and the footer come to less than the column, and what is left over
  // stays background: growing the last panel to fill it would draw a steering plot the canvas never
  // asked for, and the canvas's own heights are the point of declaring them.
  let y = bodyTop;
  TELEMETRY_TRACES.forEach((spec) => {
    const h = tracePanelHeight(spec);
    items.push(...tracePanel(`telemetry.${spec.id}`, rect(0, y, plotWidth, h), spec));
    y += h;
    items.push(rule(`telemetry.${spec.id}.rule`, 0, y, plotWidth, 1));
    y += 1;
  });
  // The axis is time, not lap distance: a ChartItem appends one sample per tick and draws the buffer
  // oldest to newest, so the left edge is simply the oldest sample the plot still holds. How long
  // that is depends on the refresh interval, which the dashboard does not fix, so the label says
  // which end is which rather than naming a window it cannot promise. The canvas asks for "0 %",
  // "Lap distance" and "100 %", and a percentage of a lap over a time axis means nothing; drawing
  // one would need the plugin to publish a series resampled against distance.
  const footerY = y + (footerHeight - d.labelSm) / 2;
  items.push(
    label('telemetry.axisStart', 'Earlier', 20, footerY, 60, { size: d.labelSm }),
    label('telemetry.axisName', 'Time', 0, footerY, plotWidth, { size: d.labelSm, hAlign: 'center' }),
    label('telemetry.axisEnd', 'Now', plotWidth - 80, footerY, 60, { size: d.labelSm, hAlign: 'right' }),
    vRule('telemetry.columnRule', plotWidth, bodyTop, bodyHeight),
  );
  const zoneHeight = Math.floor((bodyHeight - 2) / 3);
  (['A', 'B', 'C'] as const).forEach((letter, i) => {
    const top = bodyTop + i * (zoneHeight + 1);
    items.push(zoneWidget(`telemetry.zone${letter}`, rect(zoneLeft, top, zoneWidth, zoneHeight), 'standard', 'telemetry', letter));
    if (i < 2) items.push(rule(`telemetry.zone${letter}.rule`, zoneLeft, top + zoneHeight, zoneWidth, 1));
  });
  return { name: 'telemetry', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items, enabledExpression: secondScreen.pitWallPageIs(2) };
}

/**
 * The height a board of `rows` rows occupies: the header row over them, and the rows.
 *
 * Asked of `table.ts` rather than restated here, because a board's header follows its own row
 * height and only that module knows by how much. The smallest frame that still holds `rows` is
 * that sum exactly: a board stacks its rows flush and opens them against the header rule, so
 * nothing else is spent.
 */
function boardHeightOf(spec: { rows: number; rowHeight: number }): number {
  let height = spec.rows * spec.rowHeight;
  while (boardRowsThatFit(rect(0, 0, 0, height), { ...spec, density: DENSITY, header: true, board: true }) < spec.rows) height += 1;
  return height;
}

/** What the portrait board's rows are set at, off its artboard's own `.trow`. */
const PORTRAIT_ROW_HEIGHT = 32;

/** The portrait page: the field above, the driver's numbers in the middle, four zones below. */
export function portraitPage(width: number, height: number): Screen {
  const bodyTop = PIT_WALL_HEADER.height;
  // `PitWall1080x1920` draws this band 856 px tall, which is 32 + 2 x 28 + 24 x 32: a header, two
  // class bands and the field. The bands are a picture of data SimHub does not publish, as the head
  // of this file says, so the board is composed from the parts it does draw instead of keeping a
  // total that only matches the sheet by accident. The 56 px the bands would have taken go to what
  // follows the board rather than to an empty strip under the last car, which is what the sheet's
  // own column does with them: every band under the board is laid out after it, not against it.
  const boardHeight = boardHeightOf({ rows: PIT_WALL_ROWS, rowHeight: PORTRAIT_ROW_HEIGHT });
  const panelTop = bodyTop + boardHeight + 1;
  const panelHeight = 110;
  const zonesTop = panelTop + panelHeight + 1;
  const zoneHeight = Math.floor((height - zonesTop - 1) / 2);
  const half = Math.floor((width - 1) / 2);
  // The sheet's own two columns, 539 and 540 either side of the rule. An odd width divides into a
  // narrow column and a wide one, and the wide one is the remainder rather than a second `half`:
  // two halves leave the last pixel column of the page unpainted, which is the one place the eye
  // reads a page as cut short. The panel row above draws itself the same way.
  const rightWidth = width - half - 1;
  const items: Item[] = [
    ...pitWallHeader('portrait.header', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · portrait', page: 1, pages: 1, compact: true }),
    ...table({ name: 'portrait.board', frame: rect(0, bodyTop, width, boardHeight), columns: PORTRAIT_COLUMNS, mode: 'full', classOnly: secondScreen.classOnly(), density: DENSITY, rows: PIT_WALL_ROWS, rowHeight: PORTRAIT_ROW_HEIGHT, board: true }),
    rule('portrait.boardRule', 0, bodyTop + boardHeight, width, 1),
    ...sessionPanel('portrait.session', rect(0, panelTop, half, panelHeight)),
    vRule('portrait.panelRule', half, panelTop, panelHeight),
    ...lapDataPanel('portrait.lapData', rect(half + 1, panelTop, rightWidth, panelHeight)),
    rule('portrait.panelBottomRule', 0, panelTop + panelHeight, width, 1),
    zoneWidget('portrait.zoneA', rect(0, zonesTop, half, zoneHeight), 'standard', 'portrait', 'A'),
    vRule('portrait.zoneRuleTop', half, zonesTop, zoneHeight),
    zoneWidget('portrait.zoneB', rect(half + 1, zonesTop, rightWidth, zoneHeight), 'standard', 'portrait', 'B'),
    rule('portrait.zoneRuleMiddle', 0, zonesTop + zoneHeight, width, 1),
    zoneWidget('portrait.zoneC', rect(0, zonesTop + zoneHeight + 1, half, zoneHeight), 'standard', 'portrait', 'C'),
    vRule('portrait.zoneRuleBottom', half, zonesTop + zoneHeight + 1, zoneHeight),
    zoneWidget('portrait.zoneD', rect(half + 1, zonesTop + zoneHeight + 1, rightWidth, zoneHeight), 'standard', 'portrait', 'D'),
  ];
  return { name: 'portrait', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items };
}

export interface PitWallSize {
  folder: string;
  width: number;
  height: number;
  description: string;
  portrait: boolean;
}

export const PIT_WALL_SIZES: readonly PitWallSize[] = [
  { folder: 'openDash Pit wall', width: 1920, height: 1080, description: '1920 x 1080, three pages', portrait: false },
  { folder: 'openDash Pit wall portrait', width: 1080, height: 1920, description: '1080 x 1920, one page', portrait: true },
];

/**
 * The pit wall dashboard: three landscape pages, or the one portrait page.
 *
 * **Only one landscape page is live at a time**, chosen by `PitWallPage`, which the plugin sets from
 * `PitWallStartPage` when SimHub starts and a bindable action moves. That is what makes "which type I
 * want as default" answerable at all: a dashboard opens on the first screen whose expression is true,
 * and nothing else about a screen can be made to depend on a setting.
 *
 * The cost is that SimHub's own next-screen navigation no longer walks the three, because it skips a
 * screen whose expression is false; the openDash action replaces it and can be bound to a key as well
 * as to a wheel button. It is the same mechanism the companion has used since it shipped, for the same
 * reason, and `secondScreen.moduleShown` is where that is written down.
 */
export function pitWallDashboard(size: PitWallSize, metadata: DashboardMetadata): Dashboard {
  const screens = size.portrait ? [portraitPage(size.width, size.height)] : [racePage(size.width, size.height), towerPage(size.width, size.height), telemetryPage(size.width, size.height)];
  return { name: size.folder, width: size.width, height: size.height, backgroundColor: ds.color.surface.base, screens, metadata };
}
