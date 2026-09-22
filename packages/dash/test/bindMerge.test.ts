/**
 * That a second place binding an item keeps what the first place bound.
 *
 * #226 records the shape this replaces. `withBindings` returned a whole `bindings` object, so
 * `{ ...label(...), ...withBindings({ Visible: x }) }` read as "and also bind Visible" while in
 * fact replacing every binding the label had built for itself. It was harmless for exactly as long
 * as the element underneath happened to carry none of its own, and the day that stopped being true
 * a `Left` binding a rank had just computed was thrown away: nothing failed, no test went red, and
 * the strip was simply wrong on the VM.
 *
 * The sweep to `withMoreBindings` was itself invisible to the suite: it was green before and after,
 * and all 398 built files stayed byte-identical, because every site that was replacing bindings
 * happened to replace them with the expressions the element had already built. That is precisely
 * the condition #226 warns about, and it is why the contract is asserted here directly rather than
 * left to the packages. A few geometry and table tests would now fail if the merge became a replace
 * again, since the duplicated targets those sites passed have been dropped, but they would fail by
 * way of a moved rectangle or a missing row value, which names neither the helper nor the rule. The
 * collision throw has no other coverage at all.
 *
 * Deleting `withBindings` did not by itself make the trap unexpressible, since `sectorStrip` in
 * second/sectors.ts and the energy notice in modules/energy.ts had written the replace by hand as a
 * `bindings:` key rather than as a spread of the helper. Both now fold through `withMoreBindings`,
 * and because the elements underneath carry nothing of their own they take the branch that adds to
 * an item with no bindings yet, which is why that branch is asserted below rather than left to the
 * two other cases, both of which start from an element that already binds something.
 */
import { describe, expect, test } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { formula, withMoreBindings } from '../src/bind.ts';
import { label } from '../src/elements/label.ts';

describe('withMoreBindings', () => {
  test('a target bound by the element and a target bound by the caller both survive', () => {
    const drawn = label('lap.delta', '-0.00', 0, 0, 80, { bind: 'delta', size: 20 });
    expect(drawn.bindings).toEqual({ Text: formula('delta') });

    const merged = withMoreBindings(drawn, { Visible: 'showDelta' });

    expect(merged.bindings).toEqual({ Text: formula('delta'), Visible: formula('showDelta') });
  });

  test('an element with no bindings of its own gets exactly what the caller binds', () => {
    const drawn = label('lap.sector', 'S1', 0, 0, 80);
    expect(drawn.bindings).toBeUndefined();

    expect(withMoreBindings(drawn, { Visible: 'timed' }).bindings).toEqual({ Visible: formula('timed') });
  });

  test('three places binding three targets keep all three', () => {
    const drawn = label('lap.name', 'KLX', 0, 0, 80, { bind: 'name', visibleBind: 'shown' });
    const merged = withMoreBindings(withMoreBindings(drawn, { TextColor: 'ink' }), { Left: 'slide' });

    expect(Object.keys(merged.bindings ?? {}).sort()).toEqual(['Left', 'Text', 'TextColor', 'Visible']);
  });

  test('the item is not mutated, so the value handed in keeps the bindings it arrived with', () => {
    const drawn = label('lap.fuel', '0.0', 0, 0, 80, { bind: 'fuel' });
    withMoreBindings(drawn, { Visible: 'shown' });

    expect(drawn.bindings).toEqual({ Text: formula('fuel') });
  });

  test('binding nothing leaves the bindings alone rather than clearing them', () => {
    const drawn = label('lap.gap', '+0.0', 0, 0, 80, { bind: 'gap' });

    expect(withMoreBindings(drawn, { Visible: undefined }).bindings).toEqual({ Text: formula('gap') });
  });

  test('one target bound in two places throws, naming the item and the target', () => {
    const drawn = label('lap.delta', '-0.00', 0, 0, 80, { bind: 'delta' });

    expect(() => withMoreBindings(drawn, { Text: 'somethingElse' })).toThrow(
      'lap.delta: Text is bound in two places; one of them has to stop binding it.',
    );
  });

  test('a literal handed to it is still checked for properties the item does not have', () => {
    // The helper is typed by the item's kind rather than by a free type parameter, because a
    // parameter inferred from a literal is the literal's own type and a misspelt property then
    // compiles and is dropped by the serialiser. `bun run typecheck` fails if the line below stops
    // being an error, which is the whole of what this test asserts.
    const layer = withMoreBindings(
      // @ts-expect-error `repetitons` is not a property of a layer, and the helper must not let it through.
      { kind: 'layer', name: 'probe', children: [], repetitons: 3 },
      { Visible: 'shown' },
    );
    expect(layer.name).toBe('probe');
  });

  test('no file under src writes an item’s bindings by hand', () => {
    // Deleting `withBindings` closed the spread; this closes the other spelling of the same trap, a
    // `bindings:` key written into an item, which two files had carried unnoticed until the sweep.
    const root = path.resolve(import.meta.dir, '../src');
    const written: string[] = [];
    for (const file of readdirSync(root, { recursive: true }) as string[]) {
      if (!file.endsWith('.ts') || file === 'bind.ts') continue;
      const source = readFileSync(path.join(root, file), 'utf8');
      if (/\bbindings:\s/.test(source)) written.push(file);
    }
    expect(written).toEqual([]);
  });
});
