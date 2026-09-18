/**
 * Module 17, Gear: the gear, with the speed and the engine speed under it.
 *
 * The design sheet paired the gear with the speed, and the dash face's hero then settled the
 * question the other way -- a module shows one thing, and the speed has the speedo module. Testing
 * on a rig reversed it again, and the reason is worth keeping: the gear alone is the one module
 * that spends a whole page saying something a driver already knows from their hand. Speed and revs
 * beside it make the page worth the space it takes, on the face, the companion and the pit wall
 * alike, which is why the three are not told apart here.
 *
 * **The gear still wins every argument about room.** It is sized from what is left after the two
 * fields, and where that leaves it below the size a glance can read, the fields go instead and the
 * gear has the box to itself -- which is exactly what this module drew before. That is rule 17 as
 * `shedding.ts` states it, applied to the one module whose hero is a component rather than a field.
 */
import { ncalc } from '../generator.ts';
import { gear as gearComponent } from '../components/gear.ts';
import { densityOf } from '../second/density.ts';
import { rowHeight } from '../second/field.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';
import { ROW_TAIL, stack } from '../second/layout.ts';
import { rect } from '../design/geometry.ts';
import { CHARS, rpm, speed, speedUnit } from '../second/values.ts';

const { fmt } = ncalc;

/**
 * The gear fills what is left of the page: as tall as the box allows, up to the face's own 260. The
 * factor is bounded by the line box, which is 1.2 times the font size, so a gear sized to the box
 * itself would hang out of it.
 */
export const gearSizeFor = (height: number): number => Math.max(72, Math.min(260, Math.floor(height * 0.8)));

/**
 * The size below which the gear stops being a hero and the two fields are not worth their room.
 *
 * Ninety-six, which is well above the 72 the sizer will go to and well below the 180 a face's own
 * smallest gear is. The face's number is the wrong floor here and choosing it was a mistake caught
 * by the snapshot: every zone body the build produces is short enough that a gear sized around a
 * field row lands between 120 and 170, so a floor of 180 meant the fields never appeared anywhere
 * and the module was exactly what it had been. What the floor is really for is the 60 px band and
 * the 150 px strip, where a gear and two fields would leave three unreadable things instead of one
 * readable one.
 */
const GEAR_FLOOR = 96;

export const gear = defineModule('gear', (ctx) => {
  const d = densityOf(ctx.density);
  const fields = [
    fld(ctx, 'speed', 'Speed', {
      sample: '187',
      bind: fmt(speed(), '0'),
      chars: CHARS.speed,
      fs: d.big,
      follower: { text: 'km/h', bind: speedUnit(), widest: 'km/h' },
    }),
    fld(ctx, 'rpm', 'RPM', { sample: '7,420', bind: fmt(rpm(), '#,0'), chars: CHARS.rpm, fs: d.big }),
  ];
  const fieldsHeight = rowHeight(fields, ctx.density);
  // Less the tail `stack` reserves at each end for the line boxes of the first and last rows. Left
  // out, the two rows come to exactly the frame and `rowsThatFit` sheds the fields -- which is the
  // module drawing what it always drew, silently, at every zone the build produces.
  const left = Math.floor(ctx.frame.height - fieldsHeight - d.gapY - 2 * ROW_TAIL);
  // Below the floor the fields are what goes, not the gear. Measured against what the gear would be
  // rather than against the frame, because the same frame gives a different answer at each density.
  if (gearSizeFor(left) < GEAR_FLOOR) return gearComponent(ctx.frame, gearSizeFor(ctx.frame.height), `${ctx.prefix}hero`);
  return stack(
    ctx.frame,
    [
      blockRow(left, (bottom) => gearComponent(rect(ctx.frame.left, bottom - left, ctx.frame.width, left), gearSizeFor(left), `${ctx.prefix}hero`)),
      fieldsRow(fields, ctx, d.gapX),
    ],
    ctx.density,
    d.gapY,
  );
});
