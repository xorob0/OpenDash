/**
 * Module 20, Damage: the car seen from above with its hurt panels coloured, and a word for each of
 * them beside it. #582.
 *
 * Assetto Corsa and ACC publish the car's damage, and SimHub hands it on as `CarDamage1` to `5`, the
 * sims' own figures unscaled, in the order front, rear, left, right and centre. iRacing publishes
 * none, and SimHub's iRacing reader fills the same five members with zeros, which is worse than
 * nothing: a car painted from them would read undamaged after a crash. So the gate is not the values
 * but whether the sim reports damage at all, which the plugin answers per sim
 * ({@link sim.damageReported}), and with no plugin the answer is no. The page is built both ways and
 * the answer decides which half is on the screen: the drawing where the sim reports damage, and the
 * notice where it does not. The module ships off and stays in the catalogue for the sims that carry it.
 *
 * The drawing is a layer whose visibility is the gate, as the session gate's is, so a sim that reports
 * nothing never has the readings evaluated. The readings are defaulted all the same, so that no
 * binding compares a null whichever way the gate falls.
 *
 * The centre member has no panel on the drawing and is not drawn. The thresholds between a light hit
 * and a heavy one are provisional: the range a crash reaches in either sim is for a drive to measure
 * (`docs/research/sims.md`, "The damage scale").
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { CAR_MIN_WIDTH, carTopView, carTopViewSize, type CarPanel, type CarPaint } from '../second/carTopView.ts';
import { densityOf } from '../second/density.ts';
import { cellWidth, fieldBlockHeight, fieldWidth, planLines, type FieldSpec } from '../second/field.ts';
import { ROW_TAIL, stack } from '../second/layout.ts';
import { placeholder } from '../second/placeholder.ts';
import { sim } from '../second/values.ts';
import { ds } from '../tokens.ts';
import { defineModule, fieldsRow, fld } from './module.ts';

const { game, gt, iff, isnull, not, num, str } = ncalc;

export const DAMAGE_MESSAGE = 'Damage · Not published by this sim';

/**
 * Above this a panel is hit, and above {@link DAMAGE_HEAVY} it is badly hit. Both are provisional:
 * the members are the sims' own unscaled figures and nobody has yet driven a car into a wall to see
 * what they reach, so the light threshold is "any damage at all" and the heavy one a guess the first
 * drive should replace.
 */
export const DAMAGE_LIGHT = 0;
export const DAMAGE_HEAVY = 50;

/** The four panels the drawing has, the SimHub member each is read from, and the word over it. */
export const DAMAGE_PANELS: readonly { id: string; label: string; panel: CarPanel; member: number }[] = [
  { id: 'front', label: 'Front', panel: 'nose', member: 1 },
  { id: 'rear', label: 'Rear', panel: 'tail', member: 2 },
  { id: 'left', label: 'Left side', panel: 'leftSill', member: 3 },
  { id: 'right', label: 'Right side', panel: 'rightSill', member: 4 },
];

/** The three words a panel reads, and the one the box is measured by. */
export const DAMAGE_WORDS = { none: 'OK', light: 'Light', heavy: 'Heavy' } as const;
export const DAMAGE_WORD_WIDEST = DAMAGE_WORDS.heavy;

/** One member, defaulted to no damage so that nothing compares a null. */
export const carDamage = (member: number): Expr => isnull(game(`CarDamage${member}`), num(0));

/** One of three answers by how badly a member's panel is hit. */
const bySeverity = (member: number, heavy: string, light: string, none: string): Expr =>
  iff(gt(carDamage(member), num(DAMAGE_HEAVY)), str(heavy), iff(gt(carDamage(member), num(DAMAGE_LIGHT)), str(light), str(none)));

export const damageWord = (member: number): Expr => bySeverity(member, DAMAGE_WORDS.heavy, DAMAGE_WORDS.light, DAMAGE_WORDS.none);
export const damageWordColour = (member: number): Expr => bySeverity(member, ds.color.danger.primary, ds.color.caution.primary, ds.color.text.primary);
/** A panel with nothing wrong with it stays the drawing's structure colour, as the canvas paints it. */
export const damagePanelColour = (member: number): Expr => bySeverity(member, ds.color.danger.primary, ds.color.caution.primary, ds.purpose.illustration.dim);

/** Between the car and the column of words, the pit view's gap beside its own car. */
const CAR_GAP = ds.space[5];

/** The words in a grid of two, as Front and Rear over the two sides, else one to a line. */
const COLUMN_PLANS = [2, 1] as const;

/** Between the two columns of the grid, the pit view's own. */
const GRID_GAP = ds.space[4];

export const damage = defineModule('damage', (ctx) => {
  const d = densityOf(ctx.density);
  const lineGap = Math.round(d.gapY / 2);
  const reported = sim.damageReported();

  const specsAt = (fs: number): FieldSpec[] =>
    DAMAGE_PANELS.map(({ id, label, member }) =>
      fld(ctx, id, label, {
        sample: DAMAGE_WORDS.none,
        bind: damageWord(member),
        widest: DAMAGE_WORD_WIDEST,
        proportional: true,
        chars: { digits: DAMAGE_WORD_WIDEST.length, specials: 0 },
        fs,
        color: ds.color.text.primary,
        colorBind: damageWordColour(member),
      }),
    );

  /** The largest size, and the grid, at which the four words fit a box; the smallest size if none does. */
  const fit = (box: Rect): { fs: number; columns: number } => {
    for (let fs = d.mid; fs > d.tiny; fs--) {
      for (const columns of COLUMN_PLANS) {
        const lines = planLines(specsAt(fs), box.width, ctx.density, { plan: 'grid', columns, gap: GRID_GAP });
        // A grid whose cell cannot hold a word falls back to the greedy wrap, which draws off the edge
        // rather than smaller, so a word wider than its cell rules the size out.
        if (lines.some((line) => line.some((spec) => fieldWidth(spec, ctx.density) > cellWidth(box.width, columns, GRID_GAP)))) continue;
        if (fieldBlockHeight(lines, ctx.density, lineGap) > box.height - 2 * ROW_TAIL) continue;
        return { fs, columns };
      }
    }
    return { fs: d.tiny, columns: 1 };
  };

  const words = (box: Rect): Item[] => {
    const { fs, columns } = fit(box);
    return stack(box, [fieldsRow(specsAt(fs), { ...ctx, frame: box }, { lines: 'grid', columns, gap: GRID_GAP, lineGap })], ctx.density);
  };

  // The car is cut from the box the way the pit view's is (rule 18): at most a third of the frame's
  // width, and no wider than the words beside it can pay for without drawing smaller than the box
  // alone gives them. Neither cost falls as the car narrows, so the widest car the page can afford
  // is found by halving the range.
  const beside = (width: number): Rect => rect(ctx.frame.left + width + CAR_GAP, ctx.frame.top, ctx.frame.width - width - CAR_GAP, ctx.frame.height);
  const whole = fit(ctx.frame);
  let carWidth = 0;
  let low = CAR_MIN_WIDTH;
  let high = carTopViewSize(ctx.frame).width;
  while (low <= high) {
    const mid = Math.floor((low + high) / 2);
    const box = beside(mid);
    if (box.width > 0 && fit(box).fs >= whole.fs) {
      carWidth = mid;
      low = mid + 1;
    } else high = mid - 1;
  }

  const panels = Object.fromEntries(
    DAMAGE_PANELS.map(({ panel, member }): [CarPanel, CarPaint] => [panel, { color: ds.purpose.illustration.dim, bind: damagePanelColour(member) }]),
  ) as Partial<Record<CarPanel, CarPaint>>;
  const car = carTopViewSize(ctx.frame, carWidth);
  const drawing: Item[] =
    carWidth === 0
      ? words(ctx.frame)
      : [
          ...carTopView(`${ctx.prefix}car`, rect(ctx.frame.left, ctx.frame.top + Math.round((ctx.frame.height - car.height) / 2), car.width, car.height), {
            wheel: () => ({}),
            panels,
          }),
          ...words(beside(carWidth)),
        ];

  return [
    withMoreBindings({ kind: 'layer', name: `${ctx.prefix}reported`, children: drawing }, { Visible: reported }),
    ...placeholder(ctx.prefix, DAMAGE_MESSAGE, ctx.frame, ctx.density).map((item) => withMoreBindings(item, { Visible: not(reported) })),
  ];
});
