/**
 * The top-down car, which is a drawing rather than a rank and so answers to rule 18 rather than to
 * rule 17: it is cut from the box at a ratio of its own and capped at a third of the frame's width,
 * and it says which corners the crew will change by binding a block per wheel to the pit service
 * bit field.
 *
 * Nothing leaving the frame is checked here as well as in `secondScreens.test.ts`, because that
 * suite measures the boxes the build happens to produce and a drawing has to answer any rectangle
 * at all. The shapes below include ones no screen draws.
 */
import { describe, expect, test } from 'bun:test';
import { contains, rect, type Rect } from '../src/design/geometry.ts';
import { MODULES } from '../src/modules/index.ts';
import { CAR_ASPECT, CAR_MIN_WIDTH, CAR_SHARE, carTopView, carTopViewSize } from '../src/second/carTopView.ts';
import { ARCHETYPES, SHAPE_ARCHETYPES } from '../src/second/shape.ts';
import { CORNERS, PIT_SERVICE_BITS, pitServiceFlag } from '../src/second/values.ts';
import { walkItems } from '../src/walk.ts';
import type { BindingTarget, Bindings, Item } from '../src/generator.ts';

const pitView = MODULES.find((m) => m.id === 'pitView')!;

/** Every item of a drawing, flattened, with the layers dropped: a layer has no rect of its own. */
const drawn = (items: readonly Item[]): Exclude<Item, { kind: 'layer' }>[] =>
  items.flatMap((item) => [...walkItems([item])]).filter((item): item is Exclude<Item, { kind: 'layer' }> => item.kind !== 'layer');

/** The car pit view draws in a box, or nothing where the page could not afford one. */
const carIn = (frame: Rect, density: 'zone' | 'companion' | 'wide' = 'zone'): Exclude<Item, { kind: 'layer' }>[] =>
  drawn(pitView.build({ frame, density, prefix: 'p.' })).filter((item) => item.name.startsWith('p.car.'));

/** What an item binds to a target, as the expression text; the model allows a bare string or a `Formula`. */
const formulaOf = (item: { bindings?: Bindings } | undefined, target: BindingTarget): string => {
  const binding = item?.bindings?.[target];
  if (binding === undefined || !('formula' in binding)) return '';
  return typeof binding.formula === 'string' ? binding.formula : binding.formula.expression;
};

const boundsOf = (items: readonly { rect: Rect }[]): Rect => {
  const left = Math.min(...items.map((i) => i.rect.left));
  const top = Math.min(...items.map((i) => i.rect.top));
  return rect(left, top, Math.max(...items.map((i) => i.rect.left + i.rect.width)) - left, Math.max(...items.map((i) => i.rect.top + i.rect.height)) - top);
};

describe('the car is cut from its box', () => {
  for (const archetype of ARCHETYPES) {
    test(`at ${archetype} it is at most a third of the frame`, () => {
      const size = SHAPE_ARCHETYPES[archetype];
      const frame = rect(0, 0, size.width, size.height);
      const car = carIn(frame);
      expect({ archetype, drew: car.length > 0 }).toMatchObject({ drew: true });
      const box = boundsOf(car);
      expect({ archetype, width: box.width, cap: Math.floor(CAR_SHARE * frame.width), within: box.width <= CAR_SHARE * frame.width }).toMatchObject({ within: true });
    });
  }

  test('and it keeps the canvas ratio whatever the box it is cut from', () => {
    for (const frame of [rect(0, 0, 600, 280), rect(0, 0, 430, 300), rect(0, 0, 274, 300), rect(0, 0, 360, 470), rect(0, 0, 1007, 211)]) {
      const size = carTopViewSize(frame);
      const drawnRatio = size.width / size.height;
      expect({ frame, ratio: drawnRatio, held: Math.abs(drawnRatio - CAR_ASPECT) < 0.02 }).toMatchObject({ held: true });
    }
  });

  test('and a box with no room for one is given none rather than a smudge', () => {
    expect(carTopViewSize({ width: 3 * (CAR_MIN_WIDTH - 1), height: 1000 })).toEqual({ width: 0, height: 0 });
    expect(carIn(rect(0, 0, 90, 400))).toEqual([]);
  });
});

describe('nothing the car draws leaves the box it was cut to', () => {
  const paint = { wheel: () => ({}) };
  // Wide, tall, square and the two extremes of the range a module is ever handed, so that the
  // rounding at each edge is exercised rather than one comfortable size.
  for (const box of [rect(0, 0, 44, 76), rect(0, 0, 45, 78), rect(10, 20, 79, 136), rect(0, 0, 106, 182), rect(7, 3, 192, 334), rect(0, 0, 200, 349)]) {
    test(`${box.width} by ${box.height} at ${box.left},${box.top}`, () => {
      for (const item of drawn(carTopView('car', box, paint))) {
        expect({ item: item.name, rect: item.rect, inside: contains(box, item.rect) }).toMatchObject({ inside: true });
      }
    });
  }
});

describe('the four corner blocks say which corners the crew will change', () => {
  const frame = rect(0, 0, SHAPE_ARCHETYPES.wide.width, SHAPE_ARCHETYPES.wide.height);

  test('each block binds its own bit of PitSvFlags', () => {
    const car = carIn(frame);
    for (const corner of CORNERS) {
      const block = car.find((item) => item.name === `p.car.${corner}`);
      const on = pitServiceFlag(PIT_SERVICE_BITS[corner]);
      expect({ corner, found: block !== undefined }).toMatchObject({ found: true });
      expect({ corner, binds: formulaOf(block, 'BackgroundColor').includes(on) }).toMatchObject({ binds: true });
    }
  });

  test('and the tick over it is shown by the same expression', () => {
    const car = carIn(frame);
    for (const corner of CORNERS) {
      const tick = car.find((item) => item.name === `p.car.${corner}.tick`);
      expect({ corner, kind: tick?.kind, visible: formulaOf(tick, 'Visible') }).toMatchObject({ kind: 'image', visible: pitServiceFlag(PIT_SERVICE_BITS[corner]) });
    }
  });

  test('and the four bits are four different bits, not one read four times', () => {
    const car = carIn(frame);
    const formulas = CORNERS.map((corner) => formulaOf(car.find((item) => item.name === `p.car.${corner}`), 'BackgroundColor'));
    expect(new Set(formulas).size).toBe(CORNERS.length);
  });
});
