/**
 * The two pieces of chrome every pit wall panel and every trace is drawn through.
 *
 * Both are shared, so a number in either reaches four instruments at once: the panel is the pit
 * wall's four right-column panels and its five telemetry panels, and the trace is those five, the
 * inputs page and the wide car-telemetry zone. That is why they are pinned here rather than in the
 * test of any one page, which would hold the number for one caller and let it move for the rest.
 */
import { describe, expect, test } from 'bun:test';
import { rect } from '../src/design/geometry.ts';
import { panel, PANEL_TITLE_HEIGHT } from '../src/second/header.ts';
import { LEGEND_HEIGHT, SWATCH, legend, legendWidth, trace, type Series } from '../src/second/trace.ts';
import { ds } from '../src/tokens.ts';
import type { ChartItem, TextItem } from '../src/generator.ts';

const FRAME = rect(0, 0, 800, 240);

const SERIES: Series[] = [
  { name: 'Throttle', color: ds.purpose.delta.faster, bind: '1', min: 0, max: 100 },
  { name: 'Brake', color: ds.purpose.delta.slower, bind: '2', min: 0, max: 100 },
];

describe('a pit wall panel', () => {
  test('writes its title in the sheet´s own label grey, not the zone title´s secondary', () => {
    // The one inline override the pit wall zone sheets carry is on a zone title, and a panel title
    // carries none, so the two chromes differ by exactly that colour. Reading them as one drew
    // every panel title a step too bright.
    const { items } = panel('p', { frame: FRAME, title: 'Lap delta' });
    const title = items.find((i): i is TextItem => i.kind === 'text' && i.name === 'p.title');
    expect(title?.textColor).toBe(ds.color.text.label);
    expect(ds.color.text.label).not.toBe(ds.color.text.secondary);
  });

  test('gaps its title from its body by eight, and by what a caller asks for instead', () => {
    const plain = panel('a', { frame: FRAME, title: 'Session' }).body;
    const tight = panel('b', { frame: FRAME, title: 'Speed', titleGap: 6 }).body;
    expect(plain.top - FRAME.top).toBe(14 + ds.size.labelSm + ds.space[2]);
    expect(tight.top).toBe(plain.top - 2);
    // The body keeps what the title gives back, so a tighter gap is two pixels of plot rather than
    // two pixels of air at the foot.
    expect(tight.height).toBe(plain.height + 2);
    expect(PANEL_TITLE_HEIGHT).toBe(ds.size.labelSm + ds.space[2]);
  });
});

describe('a trace´s legend', () => {
  test('sets its swatch eight from its name, which is what every sheet draws', () => {
    const items = legend('l', SERIES, 0, 0, 'zone');
    const swatch = items.find((i) => i.name === 'l.Throttle.swatch');
    const text = items.find((i): i is TextItem => i.kind === 'text' && i.name === 'l.Throttle.legend');
    expect(swatch?.rect.width).toBe(SWATCH.width);
    expect((text?.rect.left ?? 0) - SWATCH.width).toBe(ds.space[2]);
  });

  test('is measured by what it draws, so a caller can right-align it', () => {
    const drawn = legend('l', SERIES, 0, 0, 'zone');
    const last = drawn.filter((i): i is TextItem => i.kind === 'text').at(-1);
    expect(last).toBeDefined();
    expect(legendWidth(SERIES, 'zone')).toBe((last!.rect.left ?? 0) + last!.rect.width);
  });

  test('costs the plot a row under it, and none when the caller puts it on the title row', () => {
    const plotOf = (items: ReturnType<typeof trace>): ChartItem => {
      const chart = items.find((i): i is ChartItem => i.kind === 'chart');
      if (!chart) throw new Error('no chart');
      return chart;
    };
    const below = trace('b', FRAME, SERIES, 'zone', { legend: true });
    const above = trace('t', FRAME, SERIES, 'zone', { legend: true, legendAt: 'title' });
    expect(plotOf(below).rect.height).toBe(FRAME.height - LEGEND_HEIGHT - 4);
    expect(plotOf(above).rect.height).toBe(FRAME.height);
    // And the entries are the caller's to place, so the trace itself draws none of them.
    expect(below.filter((i) => i.name.endsWith('.legend'))).not.toEqual([]);
    expect(above.filter((i) => i.name.endsWith('.legend'))).toEqual([]);
  });

  test('sheds an entry that will not fit rather than drawing it past the edge, in either placement', () => {
    // Rule 17 applied to chrome: a colour without its label is a guess, so the last one goes.
    const narrow = legend('n', SERIES, 0, 0, 'zone', 60);
    expect(narrow.filter((i) => i.name.endsWith('.legend')).length).toBeLessThan(SERIES.length);
    expect(legend('w', SERIES, 0, 0, 'zone', 400).filter((i) => i.name.endsWith('.legend')).length).toBe(SERIES.length);
  });
});
