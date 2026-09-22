/** Small dashboards and packages shared by the generator tests. Not a test file. */

import { deflateSync } from 'node:zlib';
import type { Dashboard, DashPackage, EllipseItem, Item, LayerItem, RectangleItem, Screen, TextItem, WidgetItem } from '../src/model.ts';

const crc32 = (bytes: Uint8Array): number => {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc ^= byte;
    for (let i = 0; i < 8; i++) crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1;
  }
  return (crc ^ 0xffffffff) >>> 0;
};

/**
 * A valid one-colour PNG of the given size, built byte by byte so that a test asserting its width,
 * height, length or MD5 is asserting properties of bytes it can see rather than of a vendored
 * binary nobody reads.
 */
export const pngBytes = (width: number, height: number, byte = 0x7f): Uint8Array => {
  const be32 = (n: number): number[] => [(n >>> 24) & 255, (n >>> 16) & 255, (n >>> 8) & 255, n & 255];
  const chunk = (type: string, body: Uint8Array): number[] => {
    const head = [...Buffer.from(type, 'ascii'), ...body];
    return [...be32(body.length), ...head, ...be32(crc32(Uint8Array.from(head)))];
  };
  const ihdr = Uint8Array.from([...be32(width), ...be32(height), 8, 2, 0, 0, 0]);
  // One filter byte then three bytes a pixel, which is what colour type 2 at depth 8 means.
  const raw = Buffer.concat(Array.from({ length: height }, () => Buffer.from([0, ...Array(width * 3).fill(byte)])));
  return Uint8Array.from([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
    ...chunk('IHDR', ihdr),
    ...chunk('IDAT', new Uint8Array(deflateSync(raw))),
    ...chunk('IEND', new Uint8Array(0)),
  ]);
};

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

/** A 12 px ring: transparent fill, a thick stroke, a rect a few pixels inside a 480 px canvas. */
export const ellipse = (name: string, over: Partial<EllipseItem> = {}): EllipseItem => ({
  kind: 'ellipse',
  name,
  rect: { left: 6, top: 6, width: 468, height: 468 },
  fillColor: '#00FFFFFF',
  strokeColor: '#FFD400',
  strokeThickness: 12,
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
export const samplePackage = (folderName = 'OpenDash'): DashPackage => {
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
