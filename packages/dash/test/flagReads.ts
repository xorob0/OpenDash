/**
 * The flag readings on a sim that publishes every `SessionFlagsDetails` bit, as iRacing does on every
 * frame.
 *
 * `flagRaised` reads a condition's bits and, where none of them is published, its `Flag_*` summary
 * (#551). Most flag tests evaluate the emitted conditions by substituting a truth for each bit read and
 * throw on anything wider, so they take the summary half out first: with every bit published it can
 * never be true, and what is left is exactly the reading they were written against.
 */
import { FLAG_CATALOGUE, summaryRaised } from '../src/flags.ts';

/** `expression` with the half a sim with no bits is read by written as false, which it is on iRacing. */
export function withEveryBitPublished(expression: string): string {
  let s = expression;
  for (const condition of FLAG_CATALOGUE) {
    const summary = summaryRaised(condition);
    if (summary !== undefined) s = s.split(String(summary)).join('0 = 1');
  }
  return s;
}
