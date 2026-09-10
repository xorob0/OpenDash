/**
 * Stable identifiers and item paths. SimHub wants a GUID on dashboards, screens and items;
 * deriving it from the object's path (package/dashboard/screen/item) means a rebuild never
 * churns ids, so diffs of the generated JSON only show real changes.
 */

import { createHash } from 'node:crypto';
import type { Item, LayerItem } from './model.ts';

/** Lower-case 8-4-4-4-12 GUID, the form SimHub writes. */
export const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

export const isGuid = (value: unknown): value is string => typeof value === 'string' && GUID_PATTERN.test(value);

/**
 * SHA-1 of the UTF-8 path, truncated to 128 bits and stamped as an RFC 4122 version 5 UUID
 * (version nibble 5, variant 10xx). Deterministic: the same path always yields the same id.
 * Unlike a textbook v5 UUID there is no namespace prefix, so `sha1(path)` alone reproduces it.
 */
export const stableGuid = (path: string): string => {
  const digest = createHash('sha1').update(path, 'utf8').digest();
  const bytes = Uint8Array.from(digest.subarray(0, 16));
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x50;
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
};

/** Path of a dashboard inside a package: `<package>/<dashboard>`. */
export const dashboardPath = (packageName: string, dashboardName: string): string => `${packageName}/${dashboardName}`;

/** Path of a screen: `<package>/<dashboard>/<screen>`. */
export const screenPath = (packageName: string, dashboardName: string, screenName: string): string =>
  `${dashboardPath(packageName, dashboardName)}/${screenName}`;

/** Path of an item under its parent (a screen or a layer): `<parent>/<item>`. */
export const itemPath = (parentPath: string, itemName: string): string => `${parentPath}/${itemName}`;

/** The item's explicit id, or the stable one derived from its path. */
export const resolveItemId = (item: Item, path: string): string => item.id ?? stableGuid(path);

export interface ItemVisit {
  item: Item;
  /** `<package>/<dashboard>/<screen>/<layer...>/<item>` */
  path: string;
  /** The id the serialiser writes for this item. */
  id: string;
  /** Enclosing layers, outermost first. */
  parents: LayerItem[];
  /** Nesting depth; 0 for a screen's direct children. */
  depth: number;
}

/**
 * Depth-first walk of an item tree in document order, yielding every item with the path the
 * serialiser derives its id from. Layer children use their layer's path as the parent path.
 */
export const walkItems = (items: Item[], parentPath: string, visit: (v: ItemVisit) => void, parents: LayerItem[] = []): void => {
  for (const item of items) {
    const path = itemPath(parentPath, item.name);
    visit({ item, path, id: resolveItemId(item, path), parents, depth: parents.length });
    if (item.kind === 'layer') walkItems(item.children, path, visit, [...parents, item]);
  }
};
