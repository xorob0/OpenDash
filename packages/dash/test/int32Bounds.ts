/**
 * The `max` and `min` calls of an expression that may round what they bound to a whole number (#831,
 * #1046).
 *
 * NCalc answers both in the type of the left operand and converts the right one to it, so an Int32
 * on the left rounds a right operand that has a fraction. This reads the tree without a frame and
 * asks two things of each call: whether its left operand may be an Int32, and whether its right one
 * may have a fraction. A call of which both are true is one SimHub may round, and the build writes
 * its bound with `real` instead.
 *
 * "May" is answered from the tree alone. A literal is typed by its spelling, `0` an Int32 and `0.0` a
 * double, and NCalc's arithmetic keeps an Int32 an Int32 except over `/`, which divides as a double.
 * An `if` may be either branch, an `isnull` either side, and an inner `max` or `min` its left
 * operand's type or, where that is null, its right one's. A property is typed by what SimHub
 * publishes, which a tree cannot see, so it is taken to have a fraction and not taken to be an
 * Int32, except the few of the plugin's own that are known to be one; so is a call to anything but
 * the functions below. `isnull([X], 0)` may therefore be an Int32, on a frame where X is null, and
 * is held to the rule like a bare `0`.
 */
import { ncalcEvaluator as E } from '../src/generator.ts';

const ARITHMETIC: ReadonlySet<string> = new Set(['+', '-', '*', '%']);

/**
 * Properties the plugin publishes as an Int32 (`int` and `int?` in `OpenDashSettings.cs`), which the
 * lights clamp against each other: the brightnesses, in whole percent.
 */
const INT32_PROPERTIES: ReadonlySet<string> = new Set(['OpenDash.LedBrightness', 'OpenDash.LightsBrightness', 'OpenDash.LightsNightBrightness']);

/** Functions whose answer is a double or a decimal whatever they are given. */
const REAL_FUNCTIONS: ReadonlySet<string> = new Set(['truncate', 'round', 'abs', 'sin', 'cos', 'timespantoseconds']);

/** Whether the node may evaluate to null, which makes an enclosing `max` or `min` hand on its other side. */
function mayBeNull(node: E.Node): boolean {
  switch (node.type) {
    case 'literal':
      return node.value === null;
    case 'property':
      return true;
    case 'unary':
    case 'binary':
      // NCalc's arithmetic throws on a null rather than answering one.
      return false;
    case 'call': {
      const [first, second, third] = node.args;
      switch (node.name.toLowerCase()) {
        case 'if':
          return mayBeNull(second!) || mayBeNull(third!);
        case 'isnull':
          return second !== undefined && mayBeNull(second);
        case 'max':
        case 'min':
          return mayBeNull(first!) && mayBeNull(second!);
        case 'repeatindex':
          return false;
        case 'timespantoseconds':
          // Null for anything that is not a TimeSpan, a null included.
          return true;
        default:
          return !REAL_FUNCTIONS.has(node.name.toLowerCase());
      }
    }
  }
}

/** Whether NCalc may type the node as an Int32 on some frame. */
export function mayBeInt32(node: E.Node): boolean {
  switch (node.type) {
    case 'literal':
      return E.isNumber(node.value) && node.value.kind === 'int';
    case 'property':
      return INT32_PROPERTIES.has(node.name);
    case 'unary':
      return node.op === '-' && mayBeInt32(node.operand);
    case 'binary':
      return ARITHMETIC.has(node.op) && mayBeInt32(node.left) && mayBeInt32(node.right);
    case 'call': {
      const [first, second, third] = node.args;
      switch (node.name.toLowerCase()) {
        case 'if':
          return mayBeInt32(second!) || mayBeInt32(third!);
        case 'isnull':
          return second !== undefined && (mayBeInt32(first!) || mayBeInt32(second));
        case 'max':
        case 'min':
          return mayBeInt32(first!) || (mayBeNull(first!) && mayBeInt32(second!));
        case 'repeatindex':
          return true;
        default:
          return false;
      }
    }
  }
}

/** Whether NCalc certainly types the node as an Int32, on every frame. */
function isInt32(node: E.Node): boolean {
  switch (node.type) {
    case 'literal':
      return E.isNumber(node.value) && node.value.kind === 'int';
    case 'property':
      // Not even one of the plugin's Int32s, which is null where the plugin is not running.
      return false;
    case 'unary':
      return node.op === '-' && isInt32(node.operand);
    case 'binary':
      return ARITHMETIC.has(node.op) && isInt32(node.left) && isInt32(node.right);
    case 'call': {
      const [first, second, third] = node.args;
      switch (node.name.toLowerCase()) {
        case 'if':
          return isInt32(second!) && isInt32(third!);
        case 'max':
        case 'min':
          return isInt32(first!);
        case 'repeatindex':
          return true;
        default:
          return false;
      }
    }
  }
}

/** Whether the node may evaluate to a number with a fraction on some frame. */
export function mayHaveFraction(node: E.Node): boolean {
  switch (node.type) {
    case 'literal':
      return E.isNumber(node.value) && !Number.isInteger(node.value.value);
    case 'property':
      return !INT32_PROPERTIES.has(node.name);
    case 'unary':
      return node.op === '-' && mayHaveFraction(node.operand);
    case 'binary':
      if (node.op === '/') return true;
      return ARITHMETIC.has(node.op) && (mayHaveFraction(node.left) || mayHaveFraction(node.right));
    case 'call': {
      const [first, second, third] = node.args;
      switch (node.name.toLowerCase()) {
        case 'if':
          return mayHaveFraction(second!) || mayHaveFraction(third!);
        case 'isnull':
          return second !== undefined && (mayHaveFraction(first!) || mayHaveFraction(second));
        case 'max':
        case 'min':
          // An Int32 on the left makes the answer whole, which is what an outer bound wants to know.
          return !isInt32(first!) && (mayHaveFraction(first!) || mayHaveFraction(second!));
        case 'truncate':
          return false;
        case 'round':
          // To no places it is whole; to any other number of them, or one the tree computes, it may not be.
          return !(second?.type === 'literal' && E.isNumber(second.value) && second.value.value === 0);
        case 'abs':
          return mayHaveFraction(first!);
        case 'repeatindex':
          return false;
        default:
          return true;
      }
    }
  }
}

/** Every `max` and `min` in the tree whose left operand may be an Int32 and whose right may have a fraction. */
export function roundingBounds(root: E.Node): E.Node[] {
  const out: E.Node[] = [];
  for (const node of E.nodesOf(root)) {
    if (node.type !== 'call' || !['max', 'min'].includes(node.name.toLowerCase())) continue;
    const [left, right] = node.args;
    if (left !== undefined && right !== undefined && mayBeInt32(left) && mayHaveFraction(right)) out.push(node);
  }
  return out;
}
