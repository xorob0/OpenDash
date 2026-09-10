/** Card 9, ABS: the ABS level. */
import { readout } from '../components/readout.ts';
import { assistValue } from './assist.ts';
import { defineCard } from './card.ts';

export const abs = defineCard('abs', (slot, rung, prefix, meta) => readout(slot, rung, prefix, { text: meta.label }, assistValue('dcABS', 'ABSLevel', '2')));
