/** Small dashboards and packages shared by the generator tests. Not a test file. */

import type { Dashboard, DashPackage, Item, LayerItem, RectangleItem, Screen, TextItem, WidgetItem } from '../src/model.ts';

export const FONTS_DIR = new URL('../../dash/fonts/', import.meta.url).pathname;
export const BARLOW_MEDIUM = `${FONTS_DIR}Barlow-Medium.ttf`;
export const BARLOW_CONDENSED_SEMIBOLD = `${FONTS_DIR}BarlowCondensed-SemiBold.ttf`;
export const BARLOW_CONDENSED_BOLD = `${FONTS_DIR}BarlowCondensed-Bold.ttf`;

export const label = (name: string, text: string, over: Partial<TextItem> = {}): TextItem => ({
  kind: 'text',
  name,
  rect: { left: 16, top: 50, width: 223, height: 18 },
  text,
  font: 'Barlow',
  fontWeight: 'Medium',
  fontSize: 15,
  textColor: '#8A9099',
  hAlign: 'left',
  vAlign: 'top',
  ...over,
});

export const numeral = (name: string, expression: string, over: Partial<TextItem> = {}): TextItem => ({
  kind: 'text',
  name,
  rect: { left: 16, top: 65, width: 196, height: 77 },
  text: '-:--.-',
  font: 'Barlow Condensed',
  fontWeight: 'SemiBold',
  fontSize: 64,
  textColor: '#F5F7FA',
  hAlign: 'left',
  vAlign: 'top',
  monospace: { charWidth: 29, specialCharsWidth: 17 },
  bindings: { Text: { mode: 'formula', formula: expression } },
  ...over,
});

export const rect = (name: string, over: Partial<RectangleItem> = {}): RectangleItem => ({
  kind: 'rect',
  name,
  rect: { left: 0, top: 64, width: 1920, height: 1 },
  backgroundColor: '#1C1F24',
  ...over,
});

export const layer = (name: string, children: Item[], over: Partial<LayerItem> = {}): LayerItem => ({
  kind: 'layer',
  name,
  children,
  ...over,
});

export const widget = (name: string, over: Partial<WidgetItem> = {}): WidgetItem => ({
  kind: 'widget',
  name,
  rect: { left: 1, top: 65, width: 255, height: 187 },
  fileName: 'cards.djson',
  initialScreenIndex: 0,
  ...over,
});

export const screen = (name: string, items: Item[], over: Partial<Screen> = {}): Screen => ({ name, items, ...over });

export const dashboard = (name: string, screens: Screen[], over: Partial<Dashboard> = {}): Dashboard => ({
  name,
  width: 1920,
  height: 480,
  backgroundColor: '#0A0B0D',
  screens,
  metadata: { title: 'Test dash', author: 'tests', version: '0.1.0', simHubVersion: '9.12.6' },
  ...over,
});

/** A two-file package like the MVP: a main dashboard with widgets and a cards widget file. */
export const samplePackage = (folderName = 'openDash'): DashPackage => {
  const cards = dashboard(
    'cards',
    [
      screen('currentLap', [label('label', 'CURRENT'), numeral('value', 'toshorttime([DataCorePlugin.GameData.CurrentLapTime], 1, false, true)')]),
      screen('lastLap', [label('label', 'LAST'), numeral('value', 'toshorttime([DataCorePlugin.GameData.LastLapTime], 3, false, true)')]),
    ],
    { width: 255, height: 187 },
  );
  const main = dashboard(folderName, [
    screen('Main', [
      rect('rule'),
      layer('shiftLights', [
        rect('seg00', { rect: { left: 24, top: 12, width: 117, height: 40 }, border: { radius: 2 }, backgroundColor: '#33383F' }),
        rect('seg01', { rect: { left: 149, top: 12, width: 117, height: 40 }, border: { radius: 2 }, backgroundColor: '#33383F' }),
      ], { bindings: { Visible: { mode: 'formula', formula: 'isnull([OpenDash.ShiftLights], true)' } } }),
      widget('Slot01', { bindings: { InitialScreenIndex: { mode: 'formula', formula: 'isnull([OpenDash.Slot01], 0)' } } }),
      widget('Slot02', { rect: { left: 257, top: 65, width: 255, height: 187 }, initialScreenIndex: 1, bindings: { InitialScreenIndex: { mode: 'formula', formula: 'isnull([OpenDash.Slot02], 1)' } } }),
    ]),
  ]);
  return { folderName, dashboards: [main, cards], fonts: [BARLOW_MEDIUM, BARLOW_CONDENSED_SEMIBOLD] };
};


export const DECLARED = ['OpenDash.ShiftLights', 'OpenDash.Slot01', 'Slot02'];
