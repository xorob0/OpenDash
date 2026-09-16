/**
 * Module 6, Energy: virtual energy, which Le Mans Ultimate publishes and iRacing does not.
 *
 * The page is built either way and the sim decides which half of it is on the screen. Every
 * reading is guarded on its own member being published, exactly as band D's energy page guards the
 * same four, so a sim that fills them draws the readings and a sim that does not draws none of
 * them and leaves the notice. The guard is on the text as well as on the visibility, because a
 * hidden item's bindings are still evaluated every frame and formatting a null would put an error
 * in SimHub's log once a frame on every car that has no virtual energy at all.
 *
 * It used to be the notice alone. That was honest about iRacing and wrong about everything else:
 * the module ships off and stays in the catalogue for the sims that carry it, and a page that
 * cannot draw them is not a page those sims can switch on.
 */
import { formula, type Expr } from '../bind.ts';
import { ncalc, type Item } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { levelGauge } from '../second/gauge.ts';
import { stack } from '../second/layout.ts';
import { placeholder } from '../second/placeholder.ts';
import { CHARS, NO_VALUE } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { blockRow, defineModule, fieldsRow, fld } from './module.ts';

const { fmt, iff, isNull, isnull, not, num, raw, str } = ncalc;

export const ENERGY_MESSAGE = 'VIRTUAL ENERGY · NOT AVAILABLE IN IRACING';

/** The four members the sim either fills or does not; `VirtualEnergy` is the one the page turns on. */
const LEVEL = raw('VirtualEnergy');
const REFUEL = raw('VirtualEnergyRefuel');
const PER_LAP = raw('VirtualEnergyPerLap');
const LAPS = raw('VirtualEnergyLaps');

const published = (member: Expr): Expr => not(isNull(member));

/** A member formatted only where it is there, so the format never runs against a null. */
const reading = (member: Expr, pattern: string): Expr => iff(published(member), fmt(member, pattern), str(NO_VALUE));

/**
 * The notice, drawn only while the sim publishes nothing for this page.
 *
 * It takes the same rectangle as the readings rather than a row of its own, because a row would
 * reserve height on the sims that do have virtual energy and those are the ones the page is for.
 * The two are never on the screen together, so the overlap is only ever in the editor.
 */
const insteadOfTheReadings = (items: readonly Item[]): Item[] =>
  items.map((item) => ({ ...item, bindings: { ...item.bindings, Visible: formula(isNull(LEVEL)) } }));

export const energy = defineModule('energy', (ctx) => {
  const d = densityOf(ctx.density);
  return [
    ...stack(
      ctx.frame,
      [
        fieldsRow(
          [
            fld(
              ctx,
              'level',
              'Virtual energy',
              { sample: '68', bind: reading(LEVEL, '0'), chars: CHARS.percent, fs: d.big, follower: { text: '%' } },
              { visibleBind: published(LEVEL) },
            ),
            fld(
              ctx,
              'refuel',
              'Refuel',
              { sample: '31', bind: reading(REFUEL, '0'), chars: CHARS.percent, fs: d.big, color: ds.color.caution.primary, follower: { text: '%' } },
              { visibleBind: published(REFUEL) },
            ),
          ],
          ctx,
        ),
        fieldsRow(
          [
            fld(
              ctx,
              'perLap',
              'Avg per lap',
              { sample: '5.6', bind: reading(PER_LAP, '0.0'), chars: CHARS.consumption, fs: d.mid, follower: { text: '%' } },
              { visibleBind: published(PER_LAP) },
            ),
            fld(ctx, 'lapsLeft', 'Est. laps', { sample: '12.1', bind: reading(LAPS, '0.0'), chars: CHARS.consumption, fs: d.mid }, { visibleBind: published(LAPS) }),
          ],
          ctx,
        ),
        blockRow(d.bar, (bottom) => [
          levelGauge(`${ctx.prefix}gauge`, rect(ctx.frame.left, bottom - d.bar, ctx.frame.width, d.bar), isnull(LEVEL, num(0)), {
            value: 68,
            visibleBind: published(LEVEL),
          }),
        ]),
      ],
      ctx.density,
      { justify: 'spaceBetween' },
    ),
    ...insteadOfTheReadings(placeholder(ctx.prefix, ENERGY_MESSAGE, ctx.frame, ctx.density)),
  ];
});
