/** Card 8, TC: the traction control level. */
import { readout } from '../components/readout.ts';
import { assistValue } from './assist.ts';
import { defineCard } from './card.ts';

export const tc = defineCard('tc', (slot, rung, prefix, meta) => readout(slot, rung, prefix, { text: meta.label }, assistValue('dcTractionControl', 'TCLevel', '3')));
