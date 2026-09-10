/**
 * Shared value of the TC and ABS cards: the level as an integer, OFF at zero in assist.off, and
 * `--` in assist.none when the car has no such control (the iRacing raw field is absent).
 */
import { ncalc } from '../generator.ts';
import type { ValueSpec } from '../components/readout.ts';
import { ds } from '../tokens.ts';
import { ASSIST_CHARS } from './chars.ts';

const { game, raw, isNull, eq, num, iff, str, fmt } = ncalc;

export function assistValue(rawField: string, levelProperty: string, sample: string): ValueSpec {
  const none = isNull(raw(rawField));
  const level = game(levelProperty);
  const off = eq(level, num(0));
  return {
    sample,
    bind: iff(none, str('--'), iff(off, str('OFF'), fmt(level, '0'))),
    chars: ASSIST_CHARS,
    colorBind: iff(none, str(ds.purpose.assist.none), iff(off, str(ds.purpose.assist.off), str(ds.color.text.primary))),
  };
}
