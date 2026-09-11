/**
 * Module 21, Track rivals: a comparison over the current stretch of track. SimHub times sectors,
 * not segments, and has no notion of a rival's time over the piece of track you are on, so there
 * is nothing to compute this from. The module says so and ships off.
 */
import { placeholder } from '../second/placeholder.ts';
import { defineModule } from './module.ts';

export const TRACK_RIVALS_MESSAGE = 'TRACK RIVALS · NOT A SIMHUB VALUE';

export const trackRivals = defineModule('trackRivals', (ctx) => placeholder(ctx.prefix, TRACK_RIVALS_MESSAGE, ctx.frame, ctx.density));
