/**
 * The demo's fake panel against the contract (#395): it publishes a face's whole group of
 * properties under the names `contract.ts` gives them, the rig-wide ones it offers are ones the
 * dashboards read, and its buttons behave as the plugin's do.
 */
import { describe, expect, test } from 'bun:test';
import { dashProperties, DEFAULT_QUICK_GLANCE, FACE_SIZES, facePropertyNames, propertyName, PORSCHE_CREST } from '../../packages/dash/src/contract.ts';
import { beginGlance, cycle, cyclePosition, endGlance, initialPanel, nextEnabled, panelProperties, setPage, setPageEnabled, type PanelState } from '../lib/demo/panel.ts';
import { panelCatalogue } from '../scripts/demo-data.ts';

const catalogue = panelCatalogue();
const declared = new Set(dashProperties());

describe('the names it writes', () => {
  test.each(FACE_SIZES.map((f) => [`${f.width}x${f.height}`, f] as const))('Face%s: exactly the face group the plugin attaches', (_size, face) => {
    const prefix = `Face${face.width}x${face.height}`;
    const written = [...panelProperties(catalogue, initialPanel(catalogue, prefix)).keys()].filter((n) => n.includes(prefix));
    expect(written.sort()).toEqual(facePropertyNames(face).map(propertyName).sort());
  });

  test('every rig-wide name is one the dashboards read, and the crest is not among them', () => {
    const rig = [...panelProperties(catalogue, initialPanel(catalogue, null)).keys()];
    expect(rig.filter((n) => !declared.has(n))).toEqual([]);
    expect(rig).not.toContain(propertyName(PORSCHE_CREST));
    expect(rig).toContain(propertyName('PositionMode'));
    expect(rig).toContain(propertyName('Slot01'));
  });

  test('a fresh panel publishes the contract defaults, as a fresh install does', () => {
    const p = panelProperties(catalogue, initialPanel(catalogue, 'Face850x480'));
    expect(p.get('OpenDash.Face850x480ZoneC')).toBe(14);
    expect(p.get('OpenDash.Face850x480ZoneBPages')).toBe(2 ** 21 - 1);
    expect(p.get('OpenDash.Face850x480QuickGlance')).toBe(DEFAULT_QUICK_GLANCE);
    expect(p.get('OpenDash.Face850x480RevBar')).toBe('shift');
    expect(p.get('OpenDash.Face850x480ZoneCPosition')).toBe(15);
    expect(p.get('OpenDash.PositionMode')).toBe('class');
  });
});

describe('the wheel buttons', () => {
  const base = initialPanel(catalogue, 'Face850x480');

  test('step to the next enabled page and wrap, and back', () => {
    expect(nextEnabled(3, 0b1111, 4, 1)).toBe(0);
    expect(nextEnabled(0, 0b1111, 4, -1)).toBe(3);
    expect(nextEnabled(0, 0b1010, 4, 1)).toBe(1);
    expect(nextEnabled(1, 0b0010, 4, 1)).toBe(1);
    expect(cycle(catalogue, base, 'A', 1).zones[0]).toBe(1);
    expect(cycle(catalogue, base, 'A', -1).zones[0]).toBe(3);
  });

  test('keep at least one page in a cycle, and move a zone off a page that is turned off', () => {
    let s: PanelState = base;
    for (const p of [1, 2, 3]) s = setPageEnabled(catalogue, s, 'A', p, false);
    expect(s.masks[0]).toBe(1);
    expect(setPageEnabled(catalogue, s, 'A', 0, false)).toBe(s);
    const moved = setPageEnabled(catalogue, setPage(catalogue, base, 'A', 2), 'A', 2, false);
    expect(moved.zones[0]).toBe(3);
  });

  test('count the position in the cycle as the plugin publishes it', () => {
    expect(cyclePosition(0, 0b1111, 4)).toBe(1);
    expect(cyclePosition(3, 0b1010, 4)).toBe(2);
  });
});

describe('the glance', () => {
  test('shows its page in its zone while held and gives the zone back on release', () => {
    const base = initialPanel(catalogue, 'Face850x480');
    const held = beginGlance(base);
    expect(held.zones[2]).toBe(DEFAULT_QUICK_GLANCE % 100);
    expect(beginGlance(held)).toBe(held);
    expect(endGlance(held).zones).toEqual(base.zones);
    expect(endGlance(base)).toBe(base);
  });
});
