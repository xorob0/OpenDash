/**
 * A module's own drawing, with the session gate taken back off.
 *
 * Since #406 a module that needs a session returns two things: one layer holding everything it
 * draws, gated on `inSession()`, and the notice that takes the frame while there is none. That is
 * the right shape for a screen and the wrong one for a test about the drawing itself -- "the map is
 * the only item when the caller asks for no title" is still a true and worth-failing-over claim
 * about the track module, and it is now a claim about the contents of the layer.
 *
 * So the tests that read a module's items positionally unwrap it through here rather than each
 * knowing how the gate is spelled. Whether the gate is there at all, and that it is there for
 * exactly the modules the catalogue declares, is `sessionNotice.test.ts`'s subject instead.
 */
import type { Item } from '../src/generator.ts';
import type { Module, ModuleContext } from '../src/modules/module.ts';
import { sessionGroupName } from '../src/modules/module.ts';

/** What `module.build(ctx)` drew, inside its session layer where it has one. */
export function drawingOf(module: Module, ctx: ModuleContext): Item[] {
  const items = module.build(ctx);
  const first = items[0];
  if (first?.kind === 'layer' && first.name === sessionGroupName(ctx.prefix)) return first.children;
  return items;
}
