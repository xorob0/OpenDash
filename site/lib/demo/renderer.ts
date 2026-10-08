/**
 * Draws a {@link Frame} on a 2D canvas, as WPF draws a dashboard.
 *
 * A canvas rather than the DOM, for four reasons. Text is clipped exactly at its item's box, which
 * is what WPF does with `MaxTextWidth` and `MaxTextHeight` and what the fit tests hold every package
 * to. A monospaced run is set one glyph per cell, centred in it. A blinking item is a change of
 * `globalAlpha`, not a style recalculation. And a few hundred items twenty times a second is no
 * work for a canvas and a great deal of layout for a document.
 *
 * Text is set as WPF sets it: a line box 1.2 em tall with the baseline 1.0 em below its top
 * (`WPF_BASELINE` and `LINE_SPACING` in `packages/dash/src/design/metrics.ts`), no kerning, the
 * family `openDash Display` read as Barlow Condensed. Vertical centring places that line box in the
 * middle of the padded box, which is the one alignment rule here not checked against SimHub.
 */
import type { ChartOp, Frame, GaugeOp, Op, ResolvedBorder, TextOp } from './engine.ts';
import { cssFont } from './fonts.ts';
import type { Box } from './scene.ts';

/** `WPF_BASELINE` and `LINE_SPACING` from `packages/dash/src/design/metrics.ts`. */
export const WPF_BASELINE = 1.0;
export const LINE_SPACING = 1.2;

/** A box in the canvas's own CSS pixels, for the outline overlay. */
export interface OutlineBox {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  readonly path: string;
  readonly kind: Op['kind'];
  readonly error: string | null;
}

export interface RenderOptions {
  /** CSS pixels per dashboard pixel. */
  readonly scale: number;
  /** Device pixels per CSS pixel. */
  readonly pixelRatio: number;
  /** Loaded pictures by `<dashboard file>/<image name>`. */
  readonly images: ReadonlyMap<string, CanvasImageSource>;
  /** Collect every drawn box, for the outline overlay. */
  readonly outline?: boolean;
}

interface Transform {
  readonly x: number;
  readonly y: number;
  readonly s: number;
}

type Ctx = CanvasRenderingContext2D;

const widthCache = new Map<string, number>();

function measure(ctx: Ctx, font: string, text: string): number {
  const key = `${font}\u0000${text}`;
  let w = widthCache.get(key);
  if (w === undefined) {
    w = ctx.measureText(text).width;
    if (widthCache.size > 20000) widthCache.clear();
    widthCache.set(key, w);
  }
  return w;
}

type Radii = ResolvedBorder['radius'];
const NO_RADII: Radii = { topLeft: 0, topRight: 0, bottomRight: 0, bottomLeft: 0 };

function roundedRect(ctx: Ctx, x: number, y: number, w: number, h: number, r: Radii): void {
  const max = Math.max(0, Math.min(w, h) / 2);
  const tl = Math.min(r.topLeft, max);
  const tr = Math.min(r.topRight, max);
  const br = Math.min(r.bottomRight, max);
  const bl = Math.min(r.bottomLeft, max);
  ctx.moveTo(x + tl, y);
  ctx.lineTo(x + w - tr, y);
  if (tr) ctx.arcTo(x + w, y, x + w, y + tr, tr);
  ctx.lineTo(x + w, y + h - br);
  if (br) ctx.arcTo(x + w, y + h, x + w - br, y + h, br);
  ctx.lineTo(x + bl, y + h);
  if (bl) ctx.arcTo(x, y + h, x, y + h - bl, bl);
  ctx.lineTo(x, y + tl);
  if (tl) ctx.arcTo(x, y, x + tl, y, tl);
  ctx.closePath();
}

/** The item's background, inside its outer edge, then its border as the ring between the edges. */
function paintBox(ctx: Ctx, box: Box, background: Op['background'], border: ResolvedBorder | null): void {
  const radii = border?.radius ?? NO_RADII;
  if (background.alpha > 0) {
    ctx.beginPath();
    roundedRect(ctx, box.left, box.top, box.width, box.height, radii);
    ctx.fillStyle = background.css;
    ctx.fill();
  }
  if (border && border.color.alpha > 0 && border.top + border.bottom + border.left + border.right > 0) {
    const inner = {
      left: box.left + border.left,
      top: box.top + border.top,
      width: Math.max(0, box.width - border.left - border.right),
      height: Math.max(0, box.height - border.top - border.bottom),
    };
    const shrink = (r: number, a: number, b: number) => Math.max(0, r - Math.max(a, b));
    ctx.beginPath();
    roundedRect(ctx, box.left, box.top, box.width, box.height, radii);
    roundedRect(ctx, inner.left, inner.top, inner.width, inner.height, {
      topLeft: shrink(radii.topLeft, border.top, border.left),
      topRight: shrink(radii.topRight, border.top, border.right),
      bottomRight: shrink(radii.bottomRight, border.bottom, border.right),
      bottomLeft: shrink(radii.bottomLeft, border.bottom, border.left),
    });
    ctx.fillStyle = border.color.css;
    ctx.fill('evenodd');
  }
}

function wrapLines(ctx: Ctx, font: string, text: string, width: number): string[] {
  const out: string[] = [];
  for (const paragraph of text.split(/\r?\n/)) {
    let line = '';
    for (const word of paragraph.split(/(\s+)/)) {
      const next = line + word;
      if (line !== '' && measure(ctx, font, next.trimEnd()) > width) {
        out.push(line.trimEnd());
        line = word.trimStart();
      } else line = next;
    }
    out.push(line);
  }
  return out;
}

function drawText(ctx: Ctx, op: TextOp): void {
  const { item, box } = op;
  if (op.text === '' || op.color.alpha <= 0) return;
  const pad = item.padding;
  const font = cssFont(item.font, item.weight, op.size, item.italic);
  ctx.font = font;
  ctx.fillStyle = op.color.css;
  ctx.textBaseline = 'alphabetic';
  ctx.textAlign = 'left';
  const innerWidth = box.width - pad.left - pad.right;
  const innerHeight = box.height - pad.top - pad.bottom;
  const lines = item.wrap ? wrapLines(ctx, font, op.text, innerWidth) : op.text.split(/\r?\n/);
  const lineHeight = LINE_SPACING * op.size;
  const block = lines.length * lineHeight;
  const top =
    item.vAlign === 'top' ? box.top + pad.top : item.vAlign === 'bottom' ? box.top + box.height - pad.bottom - block : box.top + pad.top + (innerHeight - block) / 2;
  const mono = item.monospace;
  lines.forEach((line, i) => {
    const baseline = top + i * lineHeight + WPF_BASELINE * op.size;
    const chars = [...line];
    const widths = mono ? chars.map((c) => (mono.specialChars.includes(c) ? mono.specialCharsWidth : mono.charWidth)) : null;
    const total = widths ? widths.reduce((a, b) => a + b, 0) : measure(ctx, font, line);
    let x = item.hAlign === 'left' ? box.left + pad.left : item.hAlign === 'right' ? box.left + box.width - pad.right - total : box.left + pad.left + (innerWidth - total) / 2;
    if (!widths) {
      ctx.fillText(line, x, baseline);
      return;
    }
    // Each glyph centred in its cell. Unverified against SimHub's own placement inside the cell.
    chars.forEach((c, k) => {
      const cell = widths[k]!;
      ctx.fillText(c, x + (cell - measure(ctx, font, c)) / 2, baseline);
      x += cell;
    });
  });
}

function drawGauge(ctx: Ctx, op: GaugeOp): void {
  const { box, item, fraction } = op;
  if (fraction <= 0 || op.color.alpha <= 0) return;
  ctx.fillStyle = op.color.css;
  if (item.vertical) {
    const h = box.height * fraction;
    const y = item.alignment === 'start' ? box.top + box.height - h : item.alignment === 'end' ? box.top : box.top + (box.height - h) / 2;
    ctx.fillRect(box.left, y, box.width, h);
  } else {
    const w = box.width * fraction;
    const x = item.alignment === 'start' ? box.left : item.alignment === 'end' ? box.left + box.width - w : box.left + (box.width - w) / 2;
    ctx.fillRect(x, box.top, w, box.height);
  }
}

function drawChart(ctx: Ctx, op: ChartOp): void {
  const { box, points } = op;
  if (points.length < 2 || op.color.alpha <= 0) return;
  const span = op.maximum - op.minimum || 1;
  const step = box.width / Math.max(1, op.capacity - 1);
  ctx.beginPath();
  points.forEach((v, i) => {
    const x = box.left + i * step;
    const y = box.top + box.height - ((Math.min(Math.max(v, op.minimum), op.maximum) - op.minimum) / span) * box.height;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  });
  ctx.strokeStyle = op.color.css;
  ctx.lineWidth = op.thickness;
  ctx.lineJoin = 'round';
  ctx.stroke();
}

/** A labelled box where SimHub would draw something the demo has no data or no browser for. */
function drawStandIn(ctx: Ctx, box: Box, label: string): void {
  ctx.save();
  ctx.strokeStyle = 'rgba(140,150,160,0.55)';
  ctx.setLineDash([4, 4]);
  ctx.lineWidth = 1;
  ctx.strokeRect(box.left + 0.5, box.top + 0.5, Math.max(0, box.width - 1), Math.max(0, box.height - 1));
  ctx.setLineDash([]);
  const size = Math.max(9, Math.min(16, box.height / 4));
  ctx.font = cssFont('Barlow', 'Medium', size, false);
  ctx.fillStyle = 'rgba(160,170,180,0.9)';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(label, box.left + box.width / 2, box.top + box.height / 2, Math.max(0, box.width - 8));
  ctx.restore();
}

function drawOps(ctx: Ctx, ops: readonly Op[], options: RenderOptions, t: Transform, outline: OutlineBox[] | null): void {
  for (const op of ops) {
    if (op.alpha <= 0) continue;
    const { box } = op;
    if (outline) outline.push({ x: t.x + box.left * t.s, y: t.y + box.top * t.s, width: box.width * t.s, height: box.height * t.s, path: op.path, kind: op.kind, error: op.error });
    ctx.save();
    ctx.globalAlpha *= op.alpha;
    if (op.rotation) {
      const cx = box.left + box.width / 2;
      const cy = box.top + box.height / 2;
      ctx.translate(cx, cy);
      ctx.rotate((op.rotation * Math.PI) / 180);
      ctx.translate(-cx, -cy);
    }
    if (op.kind === 'ellipse') {
      ctx.beginPath();
      ctx.ellipse(box.left + box.width / 2, box.top + box.height / 2, Math.max(0, box.width / 2), Math.max(0, box.height / 2), 0, 0, Math.PI * 2);
      if (op.fill.alpha > 0) {
        ctx.fillStyle = op.fill.css;
        ctx.fill();
      }
      if (op.thickness > 0 && op.stroke.alpha > 0) {
        ctx.strokeStyle = op.stroke.css;
        ctx.lineWidth = op.thickness;
        ctx.stroke();
      }
    } else if (op.kind !== 'widget') {
      paintBox(ctx, box, op.background, op.border);
    }
    switch (op.kind) {
      case 'text':
        ctx.beginPath();
        ctx.rect(box.left, box.top, box.width, box.height);
        ctx.clip();
        drawText(ctx, op);
        break;
      case 'gauge':
        drawGauge(ctx, op);
        break;
      case 'chart':
        drawChart(ctx, op);
        break;
      case 'image': {
        const image = options.images.get(op.key);
        if (image) ctx.drawImage(image, box.left, box.top, box.width, box.height);
        else drawStandIn(ctx, box, 'image');
        break;
      }
      case 'standIn':
        drawStandIn(ctx, box, op.label);
        break;
      case 'widget': {
        ctx.beginPath();
        ctx.rect(box.left, box.top, box.width, box.height);
        ctx.clip();
        paintBox(ctx, box, op.background, null);
        ctx.translate(box.left + op.offsetX, box.top + op.offsetY);
        ctx.scale(op.scale, op.scale);
        if (op.screenBackground.alpha > 0) {
          ctx.fillStyle = op.screenBackground.css;
          ctx.fillRect(0, 0, op.width, op.height);
        }
        drawOps(ctx, op.ops, options, { x: t.x + (box.left + op.offsetX) * t.s, y: t.y + (box.top + op.offsetY) * t.s, s: t.s * op.scale }, outline);
        break;
      }
      default:
        break;
    }
    ctx.restore();
    if (op.error) {
      // An item a binding of which the evaluator refused: marked, never left looking blank.
      ctx.save();
      ctx.strokeStyle = '#ff2d46';
      ctx.lineWidth = 2;
      ctx.setLineDash([3, 2]);
      ctx.strokeRect(box.left + 1, box.top + 1, Math.max(0, box.width - 2), Math.max(0, box.height - 2));
      ctx.restore();
    }
  }
}

/**
 * Draws a frame onto a canvas already sized to `width x height` dashboard pixels at the given scale
 * and pixel ratio. Returns the boxes drawn when `options.outline` asks for them.
 */
export function render(ctx: Ctx, frame: Frame, width: number, height: number, options: RenderOptions): OutlineBox[] {
  const k = options.scale * options.pixelRatio;
  ctx.setTransform(k, 0, 0, k, 0, 0);
  ctx.globalAlpha = 1;
  ctx.clearRect(0, 0, width, height);
  for (const paint of [{ css: '#000', alpha: 1 }, frame.background, frame.screenBackground]) {
    if (paint.alpha <= 0) continue;
    ctx.fillStyle = paint.css;
    ctx.fillRect(0, 0, width, height);
  }
  (ctx as Ctx & { fontKerning: string }).fontKerning = 'none';
  const outline = options.outline ? ([] as OutlineBox[]) : null;
  drawOps(ctx, frame.ops, options, { x: 0, y: 0, s: options.scale }, outline);
  return outline ?? [];
}
