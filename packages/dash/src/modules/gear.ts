/**
 * Module 17, Gear: the gear, as large as the page allows.
 *
 * The design sheet paired it with the speed. The dash face settled that question when its hero
 * became the gear alone: a module shows one thing, and the speed has the speedo module. The
 * component is the face's own, so the glyph, the cell and the centring are identical.
 */
import { gear as gearComponent } from '../components/gear.ts';
import { defineModule } from './module.ts';

/**
 * The gear fills the page: as tall as the box allows, up to the face's own 260. The factor is
 * bounded by the line box, which is 1.2 times the font size, so a gear sized to the box itself
 * would hang out of it.
 */
export const gearSizeFor = (height: number): number => Math.max(72, Math.min(260, Math.floor(height * 0.8)));

export const gear = defineModule('gear', (ctx) => gearComponent(ctx.frame, gearSizeFor(ctx.frame.height), `${ctx.prefix}hero`));
