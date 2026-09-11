/** The card catalogue in card-number order. The number is the value a slot setting takes. */
import { CARD_CATALOGUE } from '../contract.ts';
import { abs } from './abs.ts';
import { bestLap } from './bestLap.ts';
import type { Card } from './card.ts';
import { currentLap } from './currentLap.ts';
import { delta } from './delta.ts';
import { fuel } from './fuel.ts';
import { fuelLaps } from './fuelLaps.ts';
import { lastLap } from './lastLap.ts';
import { position } from './position.ts';
import { session } from './session.ts';
import { speed } from './speed.ts';
import { tc } from './tc.ts';
import { tyrePressures } from './tyrePressures.ts';
import { tyreTemps } from './tyreTemps.ts';

export type { Card, CardBuilder } from './card.ts';
export { defineCard } from './card.ts';

export const CARDS: readonly Card[] = [currentLap, lastLap, bestLap, delta, position, session, fuel, fuelLaps, tc, abs, tyreTemps, tyrePressures, speed];

CARDS.forEach((card, i) => {
  if (card.number !== i || CARD_CATALOGUE[i]?.id !== card.id) {
    throw new Error(`cards: ${card.id} is at index ${i} but the catalogue says ${card.number}`);
  }
});
if (CARDS.length !== CARD_CATALOGUE.length) throw new Error('cards: the catalogue and the modules differ in length');

export function cardByNumber(n: number): Card {
  const card = CARDS[n];
  if (!card) throw new RangeError(`cards: no card number ${n}`);
  return card;
}
