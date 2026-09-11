/**
 * Module 6, Energy: virtual energy, which Le Mans Ultimate has and iRacing does not. SimHub's
 * ERS members are never filled by the iRacing reader, so there is nothing honest to draw here and
 * the module says so. It ships off and stays in the catalogue for the sims that do carry it.
 */
import { placeholder } from '../second/placeholder.ts';
import { defineModule } from './module.ts';

export const ENERGY_MESSAGE = 'VIRTUAL ENERGY · NOT AVAILABLE IN IRACING';

export const energy = defineModule('energy', (ctx) => placeholder(ctx.prefix, ENERGY_MESSAGE, ctx.frame, ctx.density));
