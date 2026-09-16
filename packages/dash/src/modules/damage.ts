/**
 * Module 20, Damage: iRacing publishes no damage values at all. SimHub's damage members exist and
 * are filled with zeros by the iRacing reader, which is worse than nothing: a body diagram painted
 * from them would show an undamaged car after a crash. The module says what it cannot show, ships
 * off, and stays in the catalogue for the sims that do report damage.
 *
 * The drawing it will paint is already here: `second/carTopView.ts` takes a colour per panel, and
 * the pit order draws the same car beside its toggles. What is missing is the reading rather than
 * the picture, together with the expression that would say which sim is running; a property name is
 * written down in `second/values.ts` only once it has been checked against the reader it comes from,
 * and neither the damage members nor a name for the running sim has been.
 */
import { placeholder } from '../second/placeholder.ts';
import { defineModule } from './module.ts';

export const DAMAGE_MESSAGE = 'DAMAGE · NOT AVAILABLE IN IRACING';

export const damage = defineModule('damage', (ctx) => placeholder(ctx.prefix, DAMAGE_MESSAGE, ctx.frame, ctx.density));
