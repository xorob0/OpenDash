/**
 * What a rank does when a field is not there at all.
 *
 * The two behaviours are opposite on purpose -- a field the sim does not publish is removed and
 * the rank closes over the hole, a telltale that is unlit keeps its place and goes dim -- so the
 * thing worth proving is that each mode does its own and nothing of the other's.
 *
 * The closing arithmetic is proved rather than matched: the `Left` formula is evaluated with a
 * member switched off and compared with the rank the build would have drawn without that member.
 * A formula that merely looks right is how a rank ends up one gap off centre on a driver's wheel.
 */
import { describe, expect, test } from 'bun:test';
import type { Item, TextItem } from '../src/generator.ts';
import { dimUnless, rank, rankThatFits, rankWidth, type RankMember } from '../src/second/rank.ts';
import { ds } from '../src/tokens.ts';

/** A member that draws one named text item at its own left edge, so a test can read the geometry. */
const member = (id: string, width: number, present?: string): RankMember => ({
  id,
  width,
  present,
  draw: (at) => [
    {
      kind: 'text',
      name: id,
      rect: { left: at.x, top: 0, width: at.width, height: 20 },
      text: id,
      font: 'Barlow',
      fontWeight: 'Medium',
      fontSize: 16,
      textColor: ds.color.text.primary,
      hAlign: 'left',
      vAlign: 'top',
      backgroundColor: '#00000000',
      bindings: {
        ...(at.leftAt() ? { Left: { mode: 'formula' as const, formula: at.leftAt()! } } : {}),
        ...(at.visibleBind ? { Visible: { mode: 'formula' as const, formula: at.visibleBind } } : {}),
        ...(dimUnless(at.litBind, ds.purpose.flag.green) ? { TextColor: { mode: 'formula' as const, formula: dimUnless(at.litBind, ds.purpose.flag.green)! } } : {}),
      },
    } satisfies TextItem,
  ],
});

const leftOf = (items: readonly Item[], name: string): number => (items.find((i) => i.name === name) as TextItem).rect.left;
const formula = (items: readonly Item[], name: string, target: string): string | undefined => {
  const b = (items.find((i) => i.name === name) as TextItem | undefined)?.bindings?.[target as 'Left'];
  return b && b.mode === 'formula' && typeof b.formula === 'string' ? b.formula : undefined;
};

/**
 * The NCalc subset a `Left` formula uses: numbers, the four operators, parentheses and `if`.
 * `truth` says what each `[P*]` marker is, which is how a member is switched off.
 */
function evaluate(expression: string, truth: Record<string, boolean>): number {
  let text = expression;
  for (const [name, value] of Object.entries(truth)) text = text.split(name).join(value ? '1=1' : '1=0');
  let at = 0;
  const skip = (): void => {
    while (text[at] === ' ') at += 1;
  };
  const parseExpression = (): number => {
    let value = parseTerm();
    for (;;) {
      skip();
      const op = text[at];
      if (op !== '+' && op !== '-') return value;
      at += 1;
      const rhs = parseTerm();
      value = op === '+' ? value + rhs : value - rhs;
    }
  };
  const parseTerm = (): number => {
    let value = parseAtom();
    for (;;) {
      skip();
      const op = text[at];
      if (op !== '*' && op !== '/') return value;
      at += 1;
      const rhs = parseAtom();
      value = op === '*' ? value * rhs : value / rhs;
    }
  };
  const parseAtom = (): number => {
    skip();
    if (text.startsWith('if(', at)) {
      at += 3;
      const condition = parseCondition();
      skip();
      at += 1; // ,
      const whenTrue = parseExpression();
      skip();
      at += 1; // ,
      const whenFalse = parseExpression();
      skip();
      at += 1; // )
      return condition ? whenTrue : whenFalse;
    }
    if (text[at] === '(') {
      at += 1;
      const value = parseExpression();
      skip();
      at += 1; // )
      return value;
    }
    const match = /^-?\d+(\.\d+)?/.exec(text.slice(at));
    if (!match) throw new Error(`rank formula: cannot read ${text.slice(at, at + 20)}`);
    at += match[0].length;
    return Number(match[0]);
  };
  /** `(1=1) = (1)` and the like: every condition a present marker turns into. */
  const parseCondition = (): boolean => {
    const depth = (): number => {
      let d = 0;
      let i = at;
      for (; i < text.length; i += 1) {
        if (text[i] === '(') d += 1;
        else if (text[i] === ')') d -= 1;
        else if (text[i] === ',' && d === 0) break;
      }
      return i;
    };
    const end = depth();
    const source = text.slice(at, end);
    at = end;
    return source.includes('1=1');
  };
  const result = parseExpression();
  return result;
}

const BOX = { left: 100, width: 400, gap: 10, when: 'close' as const };

describe('a rank is packed and centred, with nothing spread to fill', () => {
  test('three members sit side by side in the middle of the box', () => {
    const { items } = rank([member('a', 100), member('b', 60), member('c', 80)], BOX);
    // 240 of content and two gaps in 400: 70 either side.
    expect(leftOf(items, 'a')).toBe(170);
    expect(leftOf(items, 'b')).toBe(280);
    expect(leftOf(items, 'c')).toBe(350);
  });

  test('a rank that cannot go missing carries no bindings at all', () => {
    const { items } = rank([member('a', 100), member('b', 60)], BOX);
    expect(formula(items, 'a', 'Left')).toBeUndefined();
    expect(formula(items, 'a', 'Visible')).toBeUndefined();
    expect(formula(items, 'b', 'TextColor')).toBeUndefined();
  });

  test('it sheds the least important, which is not always the last drawn', () => {
    const members = [member('slip', 100), member('tc', 100), member('bias', 100)];
    const kept = rankThatFits(members, 250, 10, ['bias', 'tc', 'slip']).map((m) => m.id);
    expect(kept).toEqual(['tc', 'bias']);
    // Without an order of its own, a rank sheds from the tail.
    expect(rankThatFits(members, 250, 10).map((m) => m.id)).toEqual(['slip', 'tc']);
  });

  test('and never sheds its last member, which would draw nothing', () => {
    expect(rankThatFits([member('only', 900)], 100, 10)).toHaveLength(1);
    // Unless the caller has somewhere else for the space to go: the bar's strip rather than a page.
    expect(rankThatFits([member('only', 900)], 100, 10, undefined, 0)).toHaveLength(0);
    expect(rank([member('only', 900)], { ...BOX, width: 10, atLeast: 0 }).items).toHaveLength(0);
  });
});

describe('close: a field the sim does not publish is removed and the rank closes over the hole', () => {
  const members = [member('a', 100, '[PA]'), member('b', 60, '[PB]'), member('c', 80)];

  test('a member that can go missing hides itself', () => {
    const { items } = rank(members, BOX);
    expect(formula(items, 'a', 'Visible')).toBe('[PA]');
    expect(formula(items, 'c', 'Visible')).toBeUndefined();
  });

  test('with everything present the formula lands on the drawn position', () => {
    const { items } = rank(members, BOX);
    for (const id of ['a', 'b', 'c']) {
      expect({ id, x: evaluate(formula(items, id, 'Left')!, { '[PA]': true, '[PB]': true }) }).toEqual({ id, x: leftOf(items, id) });
    }
  });

  test('with one missing, the rest sit where the build would have drawn them without it', () => {
    const { items } = rank(members, BOX);
    const withoutB = rank([member('a', 100, '[PA]'), member('c', 80)], BOX).items;
    const truth = { '[PA]': true, '[PB]': false };
    expect(evaluate(formula(items, 'a', 'Left')!, truth)).toBe(leftOf(withoutB, 'a'));
    expect(evaluate(formula(items, 'c', 'Left')!, truth)).toBe(leftOf(withoutB, 'c'));
  });

  test('and with two missing it closes over both', () => {
    const { items } = rank(members, BOX);
    const alone = rank([member('c', 80)], BOX).items;
    expect(evaluate(formula(items, 'c', 'Left')!, { '[PA]': false, '[PB]': false })).toBe(leftOf(alone, 'c'));
  });

  test('a rank laid out from the left closes over the hole without moving its first member', () => {
    const { items } = rank(members, { ...BOX, align: 'left' });
    expect(leftOf(items, 'a')).toBe(100);
    // Nothing can move the first member of a left-aligned rank, so it carries no Left at all.
    expect(formula(items, 'a', 'Left')).toBeUndefined();
    expect(evaluate(formula(items, 'c', 'Left')!, { '[PA]': true, '[PB]': false })).toBe(100 + 100 + 10);
    expect(evaluate(formula(items, 'c', 'Left')!, { '[PA]': false, '[PB]': false })).toBe(100);
  });

  test('an item inside a member moves with it, its offset kept', () => {
    // A unit sits 40 px into its field; when the rank closes, it has to arrive 40 px into wherever
    // the field landed rather than at the field's old place.
    const unit: RankMember = {
      id: 'b',
      width: 60,
      draw: (at) => [{ ...member('b', 60).draw(at)[0]!, name: 'b.unit', rect: { left: at.x + 40, top: 0, width: 20, height: 20 }, bindings: { Left: { mode: 'formula', formula: at.leftAt(40)! } } }],
    };
    const { items } = rank([member('a', 100, '[PA]'), unit], BOX);
    expect(evaluate(formula(items, 'b.unit', 'Left')!, { '[PA]': true })).toBe(leftOf(items, 'b.unit'));
    expect(evaluate(formula(items, 'b.unit', 'Left')!, { '[PA]': false })).toBe(leftOf(rank([unit], BOX).items, 'b.unit'));
  });
});

describe('dim: a telltale that is unlit is drawn dim and keeps its place', () => {
  const lamps = [member('drs', 40, '[DRS]'), member('p2p', 40, '[P2P]'), member('spt', 40)];

  test('nothing is hidden and nothing moves', () => {
    const { items } = rank(lamps, { ...BOX, when: 'dim' });
    expect(formula(items, 'drs', 'Visible')).toBeUndefined();
    expect(formula(items, 'drs', 'Left')).toBeUndefined();
    expect(leftOf(items, 'drs')).toBe(leftOf(rank(lamps, { ...BOX, when: 'close' }).items, 'drs'));
  });

  test('the ink is the lamp colour while lit and the dim ink while not', () => {
    const { items } = rank(lamps, { ...BOX, when: 'dim' });
    expect(formula(items, 'drs', 'TextColor')).toBe(`if([DRS], '${ds.purpose.flag.green}', '${ds.color.text.dim}')`);
    // A lamp that cannot go out keeps its plain colour rather than a binding that is always true.
    expect(formula(items, 'spt', 'TextColor')).toBeUndefined();
  });

  test('dimUnless is undefined when there is nothing to dim for', () => {
    expect(dimUnless(undefined, ds.purpose.flag.green)).toBeUndefined();
  });
});

test('rankWidth counts the gaps between members and not after the last', () => {
  expect(rankWidth([member('a', 100), member('b', 60)], 10)).toBe(170);
  expect(rankWidth([member('a', 100)], 10)).toBe(100);
  expect(rankWidth([], 10)).toBe(0);
});
