/**
 * How a slot shows its card. The default strategy embeds cards.djson (one screen per card) in a
 * WidgetItem per slot whose InitialScreenIndex is bound to the slot's setting. The inline
 * fallback (SLOT_STRATEGY=inline) emits every card in every slot inside a Layer whose Visible is
 * `setting.slot(i) = cardNumber`; it multiplies the item count and exists for the spike fallback.
 */
import type { LayerItem, Rect, Screen, WidgetItem } from './generator.ts';
import { ncalc } from './generator.ts';
import { withMoreBindings } from './bind.ts';
import { CARDS } from './cards/index.ts';
import { defaultCardForSlot, setting, slotSettingName } from './contract.ts';
import { rect } from './design/geometry.ts';
import { cardRung, type Layout } from './layouts/layout.ts';
import { TRANSPARENT } from './tokens.ts';

const { eq, num } = ncalc;

export type SlotStrategy = 'widget' | 'inline';
export const DEFAULT_STRATEGY: SlotStrategy = 'widget';

/** Name of the cards widget dashboard and of its file inside the package. */
export const CARDS_DASHBOARD_NAME = 'cards';
export const CARDS_FILE = `${CARDS_DASHBOARD_NAME}.djson`;

/** `Slot01`..: the WidgetItem (or inline layer) name, the same as the setting name. */
export const slotName = (slot: number): string => slotSettingName(slot);

/** The rect a card is drawn into inside cards.djson: the slot size at the origin. */
export const cardOrigin = (layout: Layout): Rect => rect(0, 0, layout.slotSize.width, layout.slotSize.height);

/** One screen per card, in card-number order, so that a card number is a screen index. */
export function cardScreens(layout: Layout): Screen[] {
  const origin = cardOrigin(layout);
  const rung = cardRung(layout);
  return CARDS.map((card) => ({
    name: card.id,
    inGame: true,
    idle: true,
    pit: true,
    backgroundColor: layout.background,
    items: card.build(origin, `${card.id}.`, rung),
  }));
}

/** A WidgetItem per slot pointing at cards.djson, its screen index bound to the slot setting. */
export function widgetSlotItems(layout: Layout): WidgetItem[] {
  return layout.slots.map((slot, i) => {
    const n = i + 1;
    return withMoreBindings({
      kind: 'widget',
      name: slotName(n),
      rect: { ...slot },
      fileName: CARDS_FILE,
      initialScreenIndex: defaultCardForSlot(n),
      autoSize: true,
      backgroundColor: TRANSPARENT,
    }, { InitialScreenIndex: setting.slot(n) });
  });
}

/** Every card in every slot, each in a Layer visible only when the slot setting selects it. */
export function inlineSlotItems(layout: Layout): LayerItem[] {
  const rung = cardRung(layout);
  return layout.slots.map((slot, i) => {
    const n = i + 1;
    const name = slotName(n);
    return {
      kind: 'layer',
      name,
      children: CARDS.map(
        (card): LayerItem => withMoreBindings({
          kind: 'layer',
          name: `${name}.${card.id}`,
          children: card.build(slot, `${name}.${card.id}.`, rung),
        }, { Visible: eq(setting.slot(n), num(card.number)) }),
      ),
    };
  });
}
