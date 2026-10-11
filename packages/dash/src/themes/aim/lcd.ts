/**
 * The LCD pass (#204): what the house draws on a page or over the face, turned into the LCD's register
 * without moving what the house decided. The house's layout says what is on a row, where it stands and
 * what is shed; this says how it looks.
 *
 *   - Every colour is the ink, or the ground where the house drew the ground: the LCD is monochrome,
 *     so a colour binding that said a state is dropped, and the state is left to the sign the figure
 *     already carries.
 *   - A figure is already in the seven-segment face, the overlay having made it the data face, so its
 *     cells are the house's own; a figure laid in cells gets its ghost behind it, every cell of its
 *     widest string as an `8`, and stands on it cell for cell, right-aligned where that widest has
 *     another shape than its sample.
 *   - A text in the house's label face is set in the 14-segment face in capitals, at four fifths of the
 *     house's size, which is the height its capitals take, and smaller only where the box asks for it.
 *     A name the house cut to its cell stays cut there: the segment cell is narrower than the widest
 *     letter the house cut it for, so it fits at that size.
 *   - A filled box holding text on a page is either a chip, a class or a licence, which loses its fill
 *     and keeps its text, or a highlight, the driver's own row, which becomes inverse video: the box in
 *     the ink, its text in the ground. Over the face, the pit family's one-line banners are inverse
 *     video whether the house filled them or outlined them, and the boxes over the content, the
 *     pop-ups, the change notifications and the lap review, are outlines: 3 px of ink on the ground
 *     with their figures in the ink, as the canvas's warning box is, since a block of ink that size
 *     hides the page under it and no LCD draws one. A filled box holding no text is a bar or a rule
 *     when it is thin, and drawn in the ink, and otherwise an area, drawn as its outline.
 *   - A box, a dot or a gauge whose colour is bound says a state by it, a segment lit or not: the
 *     binding is kept and every colour it can give is turned into the ink, or into the ghost's tone
 *     where the house gives an unlit, dim or ground colour, so the segment still lights.
 *   - A picture is coloured artwork no ink can tint, and is not drawn.
 *
 * The flag is never handed to this pass: a flag that is not its colour is not a flag.
 */
import type { Binding, BindingTarget, Hex, Item, Rect, TextItem } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings, withoutBindings, type Expr } from '../../bind.ts';
import { measureText } from '../../design/advances.ts';
import { BOX_SLACK, LINE_SPACING } from '../../design/metrics.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import { ghostOf, GHOST_OPACITY, lcdColour, SEGMENT_FAMILY, spacedWords, spacedWordsExpr } from './register.ts';

const { iff, str, ucase } = ncalc;

/** The 14-segment capitals fill the em where Barlow's take 0.7 of it, so four fifths reads as the same height. */
const CAPTION_SCALE = 0.8;
/** No caption is set smaller than this. */
const SMALLEST_CAPTION = 7;
/** Under this a box is a bar or a rule rather than an area. */
const THIN = 8;
/** A chip is at most this big; a box holding text that is larger is a highlight. */
const CHIP = { width: 120, height: 40 } as const;
/** The outline an area is drawn as. */
const OUTLINE = 2;
/** The ink round a box over the content: the canvas's warning box. */
const BOX_OUTLINE = 3;

export interface LcdOptions {
  /** How every box holding text over the face is drawn: in inverse video, or as an outline on the ground. */
  boxes?: 'inverse' | 'outline';
}

const COLOUR_BINDINGS: readonly BindingTarget[] = ['TextColor', 'BackgroundColor', 'FillColor', 'EllipseColor', 'LineColor', 'GaugeColor', 'AlternateGaugeColor'];

const expressionOf = (binding: Binding | undefined): string | undefined => (binding && 'formula' in binding && typeof binding.formula === 'string' ? binding.formula : undefined);

const withoutColourBindings = <T extends Item>(item: T): T => withoutBindings(item as unknown as Extract<Item, { kind: T['kind'] }>, COLOUR_BINDINGS) as unknown as T;

/** A bound colour turned over: every colour the expression can give is the ink, or the ghost's tone where the house gave one of its off greys. */
function inkedExpression(expression: string): string {
  const off = new Set([lcdColour('ground'), ds.color.surface.zone, ds.color.surface.raised, ds.color.surface.inset, ds.color.text.dim, ds.purpose.shift.unlit, ds.purpose.telltale.off].map((c) => c.toUpperCase()));
  return expression.replace(/'#([0-9A-Fa-f]{6})'/g, (_, hex: string) => `'${off.has(`#${hex.toUpperCase()}`) ? ds.color.surface.zone : lcdColour('ink')}'`);
}

/** The item's bound colour, if any, turned over by {@link inkedExpression}. */
const boundColour = (item: Item, target: BindingTarget): string | undefined => {
  const expression = expressionOf(item.bindings?.[target]);
  return expression === undefined ? undefined : inkedExpression(expression);
};

const filled = (colour: Hex | undefined): boolean => colour !== undefined && colour !== TRANSPARENT && colour !== lcdColour('ground');

const centreIn = (r: Rect, box: Rect): boolean => {
  const x = r.left + r.width / 2;
  const y = r.top + r.height / 2;
  return x >= box.left && x <= box.left + box.width && y >= box.top && y <= box.top + box.height;
};

const flat = (items: readonly Item[]): Item[] => items.flatMap((item) => (item.kind === 'layer' ? flat(item.children) : [item]));

/** A highlight: the box, and when it shows, which is when its text is in the ground. */
interface Highlight {
  rect: Rect;
  when?: Expr;
}

const isChip = (r: Rect): boolean => r.width <= CHIP.width && r.height <= CHIP.height;

function captionOf(item: TextItem): TextItem {
  const capitals = (s: string): string => s.toUpperCase().replaceAll('·', '-').replaceAll('…', '.');
  const room = item.rect.width - BOX_SLACK - 1;
  const fitOf = (widest: string): number => Math.floor(room / Math.max(1e-6, measureText('DSEG14Regular', widest, 1)));
  // A box the house cut so close that the words two spaces apart do not fit it even at the smallest
  // size keeps the house's one space, since a clipped caption reads worse than a tight one.
  const spaced = fitOf(spacedWords('DSEG14Regular', capitals(item.widest ?? item.text))) >= SMALLEST_CAPTION;
  const upper = (s: string): string => (spaced ? spacedWords('DSEG14Regular', capitals(s)) : capitals(s));
  const widest = upper(item.widest ?? item.text);
  const fit = fitOf(widest);
  const size = Math.max(SMALLEST_CAPTION, Math.min(Math.round(item.fontSize * CAPTION_SCALE), fit));
  const height = Math.ceil(LINE_SPACING * size) + BOX_SLACK;
  // The house's capitals stand from a tenth to three quarters of its box; the segment cell is centred where they were.
  const centre = item.rect.top + 0.65 * item.fontSize;
  const top = Math.max(item.rect.top, Math.min(Math.round(centre - size / 2), item.rect.top + item.rect.height - height));
  const text = expressionOf(item.bindings?.Text);
  const set: TextItem = {
    ...withoutBindings(item, ['Text']),
    font: SEGMENT_FAMILY.DSEG14Regular,
    fontWeight: 'Normal',
    fontSize: size,
    text: upper(item.text),
    ...(item.widest === undefined ? {} : { widest }),
    rect: { ...item.rect, top, height },
  };
  const bound = (expr: Expr): Expr => ncalc.replace(ncalc.replace(ucase(expr), '·', '-'), '…', '.');
  return text === undefined ? set : withMoreBindings(set, { Text: spaced ? spacedWordsExpr('DSEG14Regular', bound(text)) : bound(text) });
}

/**
 * A figure that declares a widest of another shape than its sample, as a delta does that is cut for
 * `−99.99` and ordinarily draws `+1.03`, is set right-aligned, and its ghost with it. Left-aligned,
 * the ordinary reading starts at the ghost's first cell and its point lands a cell short of the
 * ghost's; right-aligned, every reading to the places of the sample stands on its ghost cell for
 * cell, as the readings of the modules do (`themes/aim/modules.ts`). The house cuts such a box to
 * its widest, so the ghost fills it either way and nothing moves but the reading inside it (#886).
 */
function onItsGhost(item: TextItem): TextItem {
  if (!item.monospace || item.widest === undefined || item.hAlign === 'right' || ghostOf(item.widest) === ghostOf(item.text)) return item;
  return { ...item, hAlign: 'right' };
}

function ghostFor(item: TextItem): TextItem | undefined {
  if (!item.monospace) return undefined;
  const ghost = ghostOf(item.widest ?? item.text);
  const kept = Object.keys(item.bindings ?? {}).filter((target) => target !== 'Visible' && target !== 'Left') as BindingTarget[];
  const { widest: _widest, blink: _blink, ...rest } = withoutBindings(item, kept);
  return { ...rest, name: `${item.name}.ghost`, text: ghost, textColor: lcdColour('ink'), opacity: GHOST_OPACITY };
}

/** The house's items in the LCD's register; see the file comment. */
export function lcd(items: readonly Item[], opts: LcdOptions = {}): Item[] {
  const ink = lcdColour('ink');
  const ground = lcdColour('ground');
  const texts = flat(items).filter((item): item is TextItem => item.kind === 'text');
  const holdsText = (r: Rect): boolean => texts.some((t) => centreIn(t.rect, r));
  const highlights: Highlight[] = flat(items)
    .filter((item) => item.kind === 'rect' && opts.boxes !== 'outline' && holdsText(item.rect) && (opts.boxes === 'inverse' ? filled(item.backgroundColor) || item.border !== undefined : filled(item.backgroundColor) && !isChip(item.rect)))
    .map((item) => ({ rect: (item as { rect: Rect }).rect, when: expressionOf(item.bindings?.Visible) }));
  const onHighlight = (t: TextItem): Highlight | undefined => highlights.find((h) => centreIn(t.rect, h.rect));

  const restyle = (item: Item): Item[] => {
    if (item.kind === 'layer') return [{ ...item, children: item.children.flatMap(restyle) }];
    if (item.kind === 'image' || item.kind === 'webPage' || item.kind === 'widget') return item.kind === 'image' ? [] : [item];
    const plain = withoutColourBindings(item);
    switch (plain.kind) {
      case 'text': {
        const set = plain.font === ds.font.label ? captionOf(plain) : plain;
        const lit = onHighlight(plain);
        const coloured = withMoreBindings<'text'>(
          {
            ...set,
            textColor: lit && lit.when === undefined ? ground : ink,
            backgroundColor: TRANSPARENT,
            ...(set.border ? { border: { ...set.border, color: ink, colorBinding: undefined } } : {}),
          },
          { TextColor: lit?.when === undefined ? undefined : iff(lit.when, str(ground), str(ink)) },
        );
        const standing = lit ? coloured : onItsGhost(coloured);
        const ghost = lit ? undefined : ghostFor(standing);
        return ghost ? [ghost, standing] : [coloured];
      }
      case 'rect': {
        const border = plain.border ? { ...plain.border, color: ink, colorBinding: undefined } : undefined;
        const bound = boundColour(item, 'BackgroundColor');
        if (bound !== undefined && !holdsText(plain.rect)) return [withMoreBindings<'rect'>({ ...plain, backgroundColor: ink, ...(border ? { border } : {}) }, { BackgroundColor: bound })];
        // Over the face a box holding text is inverse video whether the house filled it or outlined it, as the pit family's banners are.
        if (opts.boxes === 'inverse' && border && holdsText(plain.rect)) return [{ ...plain, backgroundColor: ink, border }];
        if (opts.boxes === 'outline' && (border || filled(plain.backgroundColor)) && holdsText(plain.rect)) {
          return [{ ...plain, backgroundColor: ground, border: { color: ink, top: BOX_OUTLINE, bottom: BOX_OUTLINE, left: BOX_OUTLINE, right: BOX_OUTLINE } }];
        }
        if (!filled(plain.backgroundColor)) return [{ ...plain, ...(border ? { border } : {}) }];
        const r = plain.rect;
        if (holdsText(r)) {
          const highlight = opts.boxes === 'inverse' || !isChip(r);
          return [{ ...plain, backgroundColor: highlight ? ink : TRANSPARENT, ...(border ? { border } : {}) }];
        }
        if (Math.min(r.width, r.height) <= THIN) return [{ ...plain, backgroundColor: ink, ...(border ? { border } : {}) }];
        return [{ ...plain, backgroundColor: TRANSPARENT, border: { ...border, color: ink, top: OUTLINE, bottom: OUTLINE, left: OUTLINE, right: OUTLINE } }];
      }
      case 'ellipse':
        return [
          withMoreBindings<'ellipse'>(
            { ...plain, fillColor: filled(plain.fillColor) ? ink : plain.fillColor, strokeColor: ink, backgroundColor: TRANSPARENT },
            { FillColor: boundColour(item, 'FillColor'), EllipseColor: boundColour(item, 'EllipseColor') },
          ),
        ];
      case 'chart':
        return [{ ...plain, lineColor: ink, backgroundColor: TRANSPARENT, ...(plain.border ? { border: { ...plain.border, color: ink, colorBinding: undefined } } : {}) }];
      case 'linearGauge':
        return [{ ...plain, gaugeColor: ink, ...(plain.alternateGaugeColor ? { alternateGaugeColor: ink } : {}), backgroundColor: TRANSPARENT, ...(plain.border ? { border: { ...plain.border, color: ink, colorBinding: undefined } } : {}) }];
      case 'radar':
        return [{ ...plain, backgroundColor: TRANSPARENT, playerStyle: inked(plain.playerStyle, ink, true), opponentStyle: inked(plain.opponentStyle, ink, false) }];
      case 'staticMap':
        return [
          {
            ...plain,
            backgroundColor: TRANSPARENT,
            trackColor: ink,
            trackBorderColor: ground,
            ...(plain.alternateTrackSectorColor ? { alternateTrackSectorColor: ink } : {}),
            overrideColorsWithCarClassColors: false,
            mapShadow: false,
            playerStyle: inked(plain.playerStyle, ink, true),
            opponentStyle: inked(plain.opponentStyle, ink, false),
            ...(plain.startLine ? { startLine: { ...plain.startLine, color: ink } } : {}),
          },
        ];
      default:
        return [plain];
    }
  };
  return items.flatMap(restyle);
}

/** A dot in the ink: the player's filled, everyone else's an outline on the ground. */
function inked<T extends { labelColor?: Hex; dotColor?: Hex; dotBorderColor?: Hex; labelFont?: string } | undefined>(style: T, ink: Hex, player: boolean): T {
  if (!style) return style;
  return { ...style, labelColor: player ? lcdColour('ground') : ink, dotColor: player ? ink : lcdColour('ground'), dotBorderColor: ink, ...(style.labelFont ? { labelFont: SEGMENT_FAMILY.DSEG7Regular } : {}) };
}
