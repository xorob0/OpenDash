/**
 * The modules of zones B and C in the car's register: the container with an inset, as #205 measures
 * it off the Race 1 render. Each field is one row of the grey panel, its label at the left in Barlow
 * 500 and in the case the house already writes it, which is the car's mixed case, and its value
 * right-aligned in a dark inset at the right, radius 5 inside the panel's 7. The rows are as tall as
 * the car's, so a zone holds four of them, and a page with more sheds them in its own declared order
 * through the stack, exactly as the house sheds its fields; nothing is scaled down past what the inset
 * holds and nothing is drawn outside the row.
 *
 * What is not a rank of fields is dressed the same way rather than redrawn: a gauge, a bar or a
 * drawing in the middle of a page sits in an inset of its own, and the pages that are a single table
 * or a single drawing (`INSET_PAGES`) are drawn on one inset as large as the module's body.
 */
import type { Item, Rect } from '../../generator.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import type { ModuleContext } from '../../modules/module.ts';
import { field, fieldWidth, FIT_LADDER, scaleFields, type FieldSpec } from '../../second/field.ts';
import type { StackRow } from '../../second/layout.ts';
import { ds } from '../../tokens.ts';
import type { ModuleRegister } from '../drawing.ts';
import { carColour, INSET_RADIUS } from './register.ts';

/**
 * One row of the car's panel: the inset 42 tall, which holds a 32 px value in its line box, and 4
 * between two rows, the panel's padding. The label is the car's 23 px where the label column holds
 * it, and the inset keeps 14 px either side of the value.
 */
const ROW = { inset: 42, gap: 4, value: 32, label: 23, pad: 14, labelPad: 10 };

/** The share of the row the label column takes at most, which is the car's 164 of 315. */
const LABEL_SHARE = 0.52;

/** The pages that are one table or one drawing, drawn on a single inset rather than in rows. */
const INSET_PAGES: ReadonlySet<string> = new Set(['radar', 'track', 'inputs', 'leaderboard', 'relative', 'opponents', 'lapHistory', 'damage', 'trackRivals']);

/** A field with its label taken off, which is what the inset draws. */
const bare = (spec: FieldSpec): FieldSpec => ({ ...spec, label: '', labelBind: undefined, labelWidest: undefined, labelBelow: false });

/** The largest label size, at most the car's, at which every label of the rank fits its column. */
function labelSize(specs: readonly FieldSpec[], column: number): number {
  const widest = (fs: number): number => Math.max(0, ...specs.map((spec) => measureText('BarlowMedium', spec.labelWidest ?? spec.label, fs)));
  let fs = ROW.label;
  while (fs > 12 && widest(fs) + 2 > column - ROW.labelPad) fs--;
  return fs;
}

/** The value as large as the inset holds it, in its height and in the width the label leaves it. */
function fittedValue(spec: FieldSpec, room: number, density: ModuleContext['density']): FieldSpec {
  const tall = Math.min(1, ROW.value / spec.value.fs);
  const fits = (factor: number): FieldSpec | undefined => {
    const scaled = scaleFields([bare(spec)], tall * factor)[0]!;
    return fieldWidth(scaled, density) <= room ? scaled : undefined;
  };
  for (const step of FIT_LADDER) {
    const scaled = fits(step);
    if (scaled) return scaled;
  }
  return scaleFields([bare(spec)], tall * FIT_LADDER[FIT_LADDER.length - 1]!)[0]!;
}

function carRows(specs: readonly FieldSpec[], ctx: ModuleContext, order: readonly string[]): StackRow {
  const frame = ctx.frame;
  const labelled = specs.some((spec) => spec.label !== '' || spec.labelBind !== undefined);
  const widestLabel = Math.max(0, ...specs.map((spec) => measureText('BarlowMedium', spec.labelWidest ?? spec.label, ROW.label)));
  const column = labelled ? Math.min(Math.round(LABEL_SHARE * frame.width), Math.ceil(widestLabel) + 2 + 2 * ROW.labelPad) : 0;
  const labelFs = labelSize(specs, column);
  const inset = { left: frame.left + column, width: frame.width - column };
  const height = specs.length * (ROW.inset + ROW.gap) - ROW.gap;
  const draw = (bottom: number): Item[] => {
    const top = bottom - height;
    return specs.flatMap((spec, i) => {
      const rowTop = top + i * (ROW.inset + ROW.gap);
      const box = rect(inset.left, rowTop, inset.width, ROW.inset);
      const value = fittedValue(spec, box.width - 2 * ROW.pad, ctx.density);
      const width = fieldWidth(value, ctx.density);
      const valueTop = rowTop + (ROW.inset - value.value.fs) / 2;
      const items: Item[] = [band(`${spec.name}.inset`, box, carColour('inset'), { radius: INSET_RADIUS, visibleBind: spec.visibleBind })];
      if (labelled && (spec.label !== '' || spec.labelBind !== undefined)) {
        items.push(
          label(`${spec.name}.label`, spec.label, frame.left + ROW.labelPad, rowTop + (ROW.inset - labelFs) / 2, column - ROW.labelPad, {
            size: labelFs,
            color: ds.color.text.label,
            bind: spec.labelBind,
            widest: spec.labelWidest,
            visibleBind: spec.visibleBind,
          }),
        );
      }
      items.push(...field(value, box.left + box.width - ROW.pad - width, valueTop + value.value.fs, ctx.density, width));
      return items;
    });
  };
  return {
    height,
    draw,
    rigid: true,
    shed: {
      ids: specs.map((spec) => spec.id ?? spec.name),
      order,
      without: (ids) => {
        const left = specs.filter((spec) => !ids.includes(spec.id ?? spec.name));
        return left.length === 0 ? undefined : carRows(left, ctx, order);
      },
    },
  };
}

/** A row that is not fields, in an inset of its own as wide as the module. */
function insetRow(row: StackRow, frame: Rect): StackRow {
  return {
    ...row,
    draw: (bottom) => {
      const items = row.draw(bottom);
      const first = items[0];
      if (!first) return items;
      return [band(`${first.name}.inset`, rect(frame.left, bottom - row.height, frame.width, row.height), carColour('inset'), { radius: INSET_RADIUS }), ...items];
    },
  };
}

export const porscheModules: ModuleRegister = { fieldsRow: carRows, blockRow: insetRow };

/** The inset a table or a drawing page sits on, as large as the module's body. */
export const porscheModuleGround = (page: string, body: Rect, prefix: string): Item[] =>
  INSET_PAGES.has(page) ? [band(`${prefix}inset`, body, carColour('inset'), { radius: INSET_RADIUS })] : [];

