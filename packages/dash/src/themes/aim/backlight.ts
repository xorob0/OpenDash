/**
 * The AiM's backlight (#715): the LCD's ground and ink, and the four greys that follow them, bound to
 * the driver's choice of backlight on every item of a built dashboard.
 *
 * The LCD is one ink on one ground, so wherever a dashboard of this theme draws one of the overlay's
 * six colours it draws the LCD and not a state, and the pass can bind by colour rather than by item:
 * every literal of the six gets the binding of its colour, and every colour binding the drawing already
 * carries has the six substituted inside it. That keeps the backlight out of the drawing's own files,
 * and a part drawn later is backlit without being told to be.
 *
 * The ghosts follow without a binding of their own, being the ink at an opacity. The flags do not
 * follow: a flag is drawn in its own colours, and the ink on it is the ink it was drawn for.
 *
 * The white backlight is the overlay as it stands, greys included. The seven others take their greys
 * from their pair by the proportions the overlay's descriptions give the white's, which is the ink at
 * four fifths for the captions and nine twentieths for the dim text, and about a twelfth and a fifth
 * for the shade and the rule.
 */
import type { Binding, BindingTarget, Border, Dashboard, Hex, Item, Screen } from '../../generator.ts';
import { formula, withMoreBindings, withoutBindings, type BindingSpec } from '../../bind.ts';
import type { ThemeSettingChoice } from '../../contract.ts';
import { resolveToken } from '../../tokens.ts';
import { choiceColours, defaultChoice, themeSettingColour } from '../settings.ts';

const THEME = 'aim';
const SETTING = 'backlight';

const LCD_COLOURS = ['ground', 'ink', 'shade', 'rule', 'caption', 'dim'] as const;
type LcdColour = (typeof LCD_COLOURS)[number];

/** How far each grey stands from the ground towards the ink, on a backlight other than the white. */
const TOWARDS_INK: Readonly<Record<Exclude<LcdColour, 'ground' | 'ink'>, number>> = { shade: 1 / 12, rule: 1 / 5, caption: 4 / 5, dim: 9 / 20 };

/** The layers the face draws its flags in, which keep their colours whatever the backlight. */
export const FLAG_LAYERS: ReadonlySet<string> = new Set(['flag', 'flagCorner', 'flagFull']);

const channels = (hex: Hex): number[] => [1, 3, 5].map((at) => parseInt(hex.slice(at, at + 2), 16));

/** `ink` over `ground` at `share` of its strength, as `#RRGGBB`. */
const mix = (ground: Hex, ink: Hex, share: number): Hex => {
  const g = channels(ground);
  const i = channels(ink);
  return `#${g.map((c, n) => Math.round(c + share * (i[n]! - c)).toString(16).padStart(2, '0')).join('').toUpperCase()}` as Hex;
};

/** The six colours of the LCD under a backlight. */
function lcdColours(choice: ThemeSettingChoice): Record<LcdColour, Hex> {
  if (choice.id === defaultChoice(THEME, SETTING).id) {
    return Object.fromEntries(LCD_COLOURS.map((name) => [name, String(resolveToken(`palette.aim.${name}`)).toUpperCase() as Hex])) as Record<LcdColour, Hex>;
  }
  const [ground, ink] = choiceColours(THEME, SETTING, choice) as [Hex, Hex];
  return { ground, ink, shade: mix(ground, ink, TOWARDS_INK.shade), rule: mix(ground, ink, TOWARDS_INK.rule), caption: mix(ground, ink, TOWARDS_INK.caption), dim: mix(ground, ink, TOWARDS_INK.dim) };
}

let bound: Map<string, string> | undefined;

/** The expression each of the white's six colours is drawn by, keyed by the colour in capitals. */
function backlitColours(): Map<string, string> {
  if (bound) return bound;
  const white = lcdColours(defaultChoice(THEME, SETTING));
  bound = new Map(LCD_COLOURS.map((name) => [white[name], themeSettingColour(THEME, SETTING, (choice) => lcdColours(choice)[name])]));
  return bound;
}

const QUOTED_HEX = /'(#[0-9A-Fa-f]{6})'/g;

/** A formula with each of the six colours it writes as a literal replaced by that colour's expression. */
const backlitFormula = (expression: string): string => expression.replace(QUOTED_HEX, (whole, hex: string) => backlitColours().get(hex.toUpperCase()) ?? whole);

/** A binding's formula when it is one written out, which a gradient's ramp of states is not. */
const formulaOf = (binding: Binding): string | undefined => (binding.mode === 'formula' && typeof binding.formula === 'string' ? binding.formula : undefined);

/** A binding the drawing made, backlit when it is a formula; a gradient's colours are a ramp of states and are left. */
const backlitBinding = (binding: Binding): Binding => {
  const expression = formulaOf(binding);
  return expression === undefined ? binding : { ...binding, formula: backlitFormula(expression) };
};

/** The expression a literal colour is drawn by, or undefined for a colour that is not the LCD's. */
const expressionFor = (colour: Hex | undefined): string | undefined => (colour === undefined ? undefined : backlitColours().get(colour.toUpperCase()));

/** The item's colours that take a binding, by the field that holds each and the target that binds it. */
const COLOUR_TARGETS = [
  ['textColor', 'TextColor'],
  ['backgroundColor', 'BackgroundColor'],
  ['fillColor', 'FillColor'],
  ['strokeColor', 'EllipseColor'],
  ['lineColor', 'LineColor'],
  ['gaugeColor', 'GaugeColor'],
  ['alternateGaugeColor', 'AlternateGaugeColor'],
] as const;

function backlitBorder(border: Border): Border {
  if (border.colorBinding) return { ...border, colorBinding: backlitBinding(border.colorBinding) };
  const expression = expressionFor(border.color);
  return expression === undefined ? border : { ...border, colorBinding: formula(expression) };
}

function backlitItem(item: Item): Item {
  if (item.kind === 'layer') return { ...item, children: item.children.map(backlitItem) };
  const fields = item as unknown as Record<string, unknown>;
  const spec: BindingSpec = {};
  const rebound: BindingTarget[] = [];
  for (const [field, target] of COLOUR_TARGETS) {
    const existing = item.bindings?.[target];
    const expression = existing === undefined ? expressionFor(fields[field] as Hex | undefined) : formulaOf(existing);
    if (expression === undefined) continue;
    if (existing !== undefined) rebound.push(target);
    spec[target] = existing === undefined ? expression : backlitFormula(expression);
  }
  type Same = Extract<Item, { kind: typeof item.kind }>;
  const bound = withMoreBindings(withoutBindings(item as Same, rebound), spec) as Item;
  const border = fields.border as Border | undefined;
  return border ? ({ ...bound, border: backlitBorder(border) } as Item) : bound;
}

const backlitScreen = (screen: Screen): Screen => ({ ...screen, items: screen.items.map((item) => (item.kind === 'layer' && FLAG_LAYERS.has(item.name) ? item : backlitItem(item))) });

/** A dashboard of the AiM theme under the driver's backlight; see the file comment. */
export const backlit = (dashboard: Dashboard): Dashboard => ({ ...dashboard, screens: dashboard.screens.map(backlitScreen) });
