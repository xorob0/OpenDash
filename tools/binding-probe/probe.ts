/**
 * Builds `openDash Probe`, a throwaway package that answers one question per row: does SimHub
 * apply a binding to this property? Each row draws a literal value that says FAIL and binds a
 * constant that says PASS, so the answer is readable from a screenshot with no telemetry running.
 *
 * Written as plain JSON rather than through packages/generator on purpose: the generator's
 * validator only permits the targets openDash already relies on, and the point of the probe is
 * the targets it does not.
 */
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { zipPackage } from '../../packages/generator/src/package.ts';

const W = 1280;
const H = 720;
const GREEN = '#FF00D96A';
const RED = '#FFFF2D46';
const BLUE = '#FF2E7BFF';
const GREY = '#FF8A9099';
const NAME = 'openDash Probe';

type Json = Record<string, unknown>;

const formula = (expression: string): Json => ({ Formula: { Expression: expression }, Mode: 2 });
const gradient = (expression: string, start: string, end: string): Json => ({
  Formula: { Expression: expression },
  StartColor: start,
  StartColorValue: 0,
  EndColor: end,
  EndColorValue: 1,
  EnableMiddleColor: false,
  MiddleColor: '#FF000000',
  MiddleColorValue: 1,
  Mode: 4,
});

let n = 0;
const text = (o: Json): Json => ({
  $type: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins',
  IsTextItem: true,
  Font: 'Barlow',
  FontWeight: 'Medium',
  FontStyle: 'Normal',
  FontSize: 22,
  TextColor: GREY,
  HorizontalAlignment: 0,
  VerticalAlignment: 1,
  TextWrapping: 'NoWrap',
  Visible: true,
  BackgroundColor: '#00FFFFFF',
  BorderStyle: {},
  Name: `probe${++n}`,
  RenderingSkip: 0,
  MinimumRefreshIntervalMS: 0,
  ...o,
});

const rect = (o: Json): Json => ({
  $type: 'SimHub.Plugins.OutputPlugins.GraphicalDash.Models.RectangleItem, SimHub.Plugins',
  Visible: true,
  BackgroundColor: '#00FFFFFF',
  Name: `probe${++n}`,
  RenderingSkip: 0,
  MinimumRefreshIntervalMS: 0,
  ...o,
});

/** One row: a grey question on the left, the subject on the right. */
const ROW_H = 58;
const TOP = 24;
const LABEL_W = 440;
const SUBJECT_X = 480;
const row = (i: number): number => TOP + i * ROW_H;

const label = (i: number, s: string): Json =>
  text({ Text: s, Left: 20, Top: row(i), Width: LABEL_W, Height: 40, FontSize: 20 });

const items: Json[] = [];
let r = 0;
const ask = (question: string, ...subjects: Json[]): void => {
  items.push(label(r, question), ...subjects);
  r++;
};

// 1. TextColor, formula mode: a Color property bound to a colour string.
ask(
  '1  TextColor formula',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(0), Width: 200, Height: 40, FontSize: 26, TextColor: RED, Bindings: { TextColor: formula(`'${GREEN}'`) } }),
);

// 2. TextColor, gradient mode: formula value 1 lands on the ramp's end colour.
ask(
  '2  TextColor gradient (blue)',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(1), Width: 200, Height: 40, FontSize: 26, TextColor: RED, Bindings: { TextColor: gradient('1', RED, BLUE) } }),
);

// 3. FontSize: a double.
ask(
  '3  FontSize 14 -> 34',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(2), Width: 300, Height: 48, FontSize: 14, TextColor: GREEN, Bindings: { FontSize: formula('34') } }),
);

// 4. Font: a string SimHub marks [NoBinding], so its own editor will not offer it.
ask(
  '4  Font -> Courier New',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(3), Width: 300, Height: 40, FontSize: 26, TextColor: GREEN, Bindings: { Font: formula("'Courier New'") } }),
);

// 5. CharWidth: a double, also [NoBinding]. Monospaced text widens when it applies.
ask(
  '5  CharWidth 14 -> 40',
  text({
    Text: '888',
    Left: SUBJECT_X,
    Top: row(4),
    Width: 300,
    Height: 40,
    FontSize: 26,
    TextColor: GREEN,
    UseMonospacedText: true,
    CharWidth: 14,
    SpecialCharsWidth: 14,
    SpecialChars: '.,:',
    Bindings: { CharWidth: formula('40') },
  }),
);

// 6. BorderColor on the BorderStyle sub-object, which is an IBindable of its own.
ask(
  '6  BorderStyle.Bindings.BorderColor',
  rect({
    Left: SUBJECT_X,
    Top: row(5),
    Width: 120,
    Height: 40,
    BorderStyle: { BorderColor: RED, BorderTop: 4, BorderBottom: 4, BorderLeft: 4, BorderRight: 4, Bindings: { BorderColor: formula(`'${GREEN}'`) } },
  }),
);

// 7. The same target at item level, which BindingHelper cannot resolve. Expected to stay red.
ask(
  '7  item Bindings.BorderColor (expect red)',
  rect({
    Left: SUBJECT_X,
    Top: row(6),
    Width: 120,
    Height: 40,
    BorderStyle: { BorderColor: RED, BorderTop: 4, BorderBottom: 4, BorderLeft: 4, BorderRight: 4 },
    Bindings: { BorderColor: formula(`'${GREEN}'`) },
  }),
);

// 8. An int on the same sub-object.
ask(
  '8  BorderStyle.Bindings.BorderTop 2 -> 18',
  rect({
    Left: SUBJECT_X,
    Top: row(7),
    Width: 120,
    Height: 40,
    BorderStyle: { BorderColor: GREEN, BorderTop: 2, BorderBottom: 2, BorderLeft: 2, BorderRight: 2, Bindings: { BorderTop: formula('18') } },
  }),
);

// 9. Left: positional, and the amendment says shipping dashboards bind it.
ask(
  '9  Left 480 -> 700',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(8), Width: 300, Height: 40, FontSize: 26, TextColor: GREEN, Bindings: { Left: formula('700') } }),
);

// 10. Opacity: 25 is visibly faded, so a fully lit PASS is the failure.
ask(
  '10 Opacity 100 -> 25 (faded)',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(9), Width: 300, Height: 40, FontSize: 26, TextColor: GREEN, Opacity: 100, Bindings: { Opacity: formula('25') } }),
);

// 11. A property no item has. The row must draw normally: an unresolvable target is a no-op.
ask(
  '11 Bindings.Nonsense (expect drawn)',
  text({ Text: 'PASS', Left: SUBJECT_X, Top: row(10), Width: 300, Height: 40, FontSize: 26, TextColor: GREEN, Bindings: { Nonsense: formula('1') } }),
);

// 12. TextPadding, the other sub-object on a TextItem, to show 6 is the class and not the property.
ask(
  '12 TextPadding.Bindings.PaddingLeft 0 -> 60',
  text({
    Text: 'PASS',
    Left: SUBJECT_X,
    Top: row(11),
    Width: 300,
    Height: 40,
    FontSize: 26,
    TextColor: GREEN,
    TextPadding: { PaddingLeft: 0, Bindings: { PaddingLeft: formula('60') } },
  }),
);

const dashboard: Json = {
  Version: 2,
  Id: '0f8e6a10-1111-4b22-9c33-abcdef000001',
  BaseHeight: H,
  BaseWidth: W,
  BackgroundColor: '#FF0A0B0D',
  Screens: [
    {
      RenderingSkip: 0,
      Name: 'probe',
      InGameScreen: true,
      IdleScreen: true,
      PitScreen: true,
      ScreenId: '0f8e6a10-1111-4b22-9c33-abcdef000002',
      AllowOverlays: false,
      IsForegroundLayer: false,
      IsOverlayLayer: false,
      // Both must be objects. `EditorModel.UpdateScreenEnabledStatus` reads
      // `ScreenEnabledExpression.Expression` unguarded, so a null here is a NullReferenceException
      // the moment the dashboard starts, and SimHub reports it only in its log.
      OverlayTriggerExpression: { Expression: '' },
      ScreenEnabledExpression: { Expression: '' },
      OverlayMaxDuration: 0,
      OverlayMinDuration: 0,
      IsBackgroundLayer: false,
      BackgroundColor: '#FF0A0B0D',
      Background: 'None',
      MinimumRefreshIntervalMS: 0,
      Items: items,
    },
  ],
  SnapToGrid: false,
  HideLabels: false,
  ShowForeground: true,
  ForegroundOpacity: 100,
  ShowBackground: true,
  BackgroundOpacity: 100,
  ShowBoundingRectangles: false,
  GridSize: 10,
  Images: [],
  Metadata: null,
  ShowOnScreenControls: false,
  IsOverlay: false,
  EnableClickThroughOverlay: false,
  EnableOnDashboardMessaging: false,
  UseStrictJSIsolation: true,
  UseStrictJSIsolationWarning: false,
};

const metadata: Json = {
  SimHubVersion: '9.12.6',
  Category: null,
  Title: NAME,
  Description: 'Which properties SimHub applies a binding to (XOR-73)',
  Author: 'openDash contributors',
  Width: W,
  Height: H,
  DashboardVersion: '0.0.0-probe',
  ScreenCount: 1,
  InGameScreensIndexs: [0],
  IdleScreensIndexs: [0],
  PitScreensIndexs: [0],
  MainPreviewIndex: 0,
  IsOverlay: false,
  OverlaySizeWarning: true,
  MetadataVersion: 2,
  EnableOnDashboardMessaging: false,
  PreferredTouchMode: 0,
};

const outDir = process.argv[2] ?? 'build';
const folder = join(outDir, NAME);
rmSync(folder, { recursive: true, force: true });
mkdirSync(folder, { recursive: true });
const djson = JSON.stringify(dashboard, null, 2);
const meta = JSON.stringify(metadata, null, 2);
writeFileSync(join(folder, `${NAME}.djson`), djson);
writeFileSync(join(folder, `${NAME}.djson.metadata`), meta);
const { path } = zipPackage(outDir, NAME);
console.log(`wrote ${folder} and ${path} with ${r} probes`);
