/** rule: a 1 px separator in surface.raised. Zones are separated by rules, never boxes. */
import type { RectangleItem } from '../generator.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';

export function rule(name: string, x: number, y: number, width: number, height: number): RectangleItem {
  return { kind: 'rect', name, rect: roundRect(rect(x, y, width, height)), backgroundColor: ds.color.surface.raised };
}
