/**
 * The walker reads every expression SimHub evaluates for a dashboard, not only the bindings written
 * on its items. Each of the other three places is a property read that nothing else would record:
 * a border's colour is bound inside `BorderStyle`, a screen's enabled expression is on the screen,
 * and a dashboard's variables are expressions of their own. #581 is the year in which the scan read
 * none of them and the traces carried none of what they read.
 */
import { describe, expect, test } from 'bun:test';
import type { Dashboard, Item, LayerItem, RectangleItem, TextItem } from '../src/generator.ts';
import { dashboardExpressionsOf, expressionsIn, expressionsOf, isVariableRead, propertiesIn } from '../src/walk.ts';

const text = (name: string, extra: Partial<TextItem> = {}): TextItem => ({
  kind: 'text',
  name,
  rect: { left: 0, top: 0, width: 10, height: 10 },
  text: '',
  font: 'Barlow',
  fontWeight: 'Normal',
  fontSize: 10,
  textColor: '#FFFFFF',
  hAlign: 'left',
  vAlign: 'top',
  bindings: { Text: { mode: 'formula', formula: '[A.Text]' } },
  ...extra,
});

const bordered = text('chip', {
  border: { color: '#000000', top: 1, bottom: 1, left: 1, right: 1, colorBinding: { mode: 'formula', formula: { expression: "if([B.Border] > 0, '#FF0000', '#000000')", preExpression: '[C.Pre]' } } },
});

const plain: RectangleItem = { kind: 'rect', name: 'plain', rect: { left: 0, top: 0, width: 1, height: 1 } };

const group: LayerItem = { kind: 'layer', name: 'group', children: [text('inner')] };

const dashboard: Dashboard = {
  name: 'walk',
  width: 100,
  height: 100,
  backgroundColor: '#000000',
  variables: [{ name: 'held', expression: 'if([D.Variable] > 0, 1, [variable.held])', beforeScreenRoles: true }],
  screens: [
    { name: 'on', enabledExpression: '[variable.held] = 1 and [E.Enable] = 1', items: [bordered] },
    { name: 'off', items: [group as Item] },
  ],
  metadata: { title: 'walk', author: 'test', version: '0.0.0', simHubVersion: '9.12.6' },
};

describe('the expressions of an item', () => {
  test('a border colour binding is one of them, pre-expression included', () => {
    expect(expressionsOf(bordered)).toEqual(['[A.Text]', "if([B.Border] > 0, '#FF0000', '#000000')", '[C.Pre]']);
  });

  test('an item with no border and no bindings has none', () => {
    expect(expressionsOf(plain)).toEqual([]);
  });
});

describe('the expressions of a dashboard', () => {
  test('its variables come first, then the enabled expressions, then every item of every screen, layers flattened', () => {
    expect(dashboardExpressionsOf(dashboard)).toEqual(['if([D.Variable] > 0, 1, [variable.held])', '[variable.held] = 1 and [E.Enable] = 1']);
    expect(expressionsIn(dashboard)).toEqual([
      'if([D.Variable] > 0, 1, [variable.held])',
      '[variable.held] = 1 and [E.Enable] = 1',
      '[A.Text]',
      "if([B.Border] > 0, '#FF0000', '#000000')",
      '[C.Pre]',
      '[A.Text]',
    ]);
  });

  test('the properties it reads come from all four places, and a read of its own variable is not one', () => {
    expect(propertiesIn(dashboard)).toEqual(['D.Variable', 'E.Enable', 'A.Text', 'B.Border', 'C.Pre']);
    expect(isVariableRead('variable.held')).toBe(true);
    // SimHub matches the prefix case-sensitively, so a wrong case is a property read that the
    // validator refuses; the scan does not second-guess it.
    expect(isVariableRead('Variable.held')).toBe(false);
  });
});
