/**
 * What an LED shows of a colour at a glance: a hue family, white, or dark.
 *
 * Hex equality was the rule, and it let the caution amber `#FFB300` sit beside the flag yellow
 * `#FFD400` as two colours, which on an RGB LED they are not: ABS, the spotter and the temperature
 * warning all read as a yellow flag (#794). The families are the hues an LED keeps apart in
 * peripheral vision; a colour belongs to the nearest one, so amber is yellow and only an orange a
 * third of the way to red is orange. A lamp drops a condition drawn as one already on it by this
 * measure (`lampConditions` in effects.ts), and `leds.lamps.test.ts` holds every lamp to it.
 */
export const hueFamily = (hex: string): string => {
  const [r, g, b] = [1, 3, 5].map((i) => Number.parseInt(hex.slice(i, i + 2), 16) / 255) as [number, number, number];
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  if (max < 0.15) return 'dark';
  if ((max - min) / max < 0.25) return 'white';
  const d = max - min;
  const hue = (max === r ? 60 * (((g - b) / d + 6) % 6) : max === g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4)) % 360;
  const centres: Record<string, number> = { red: 355, orange: 25, yellow: 52, green: 140, cyan: 190, blue: 220, purple: 275 };
  const apart = (a: number, c: number): number => Math.min(Math.abs(a - c), 360 - Math.abs(a - c));
  return Object.entries(centres).reduce((best, [name, c]) => (apart(hue, c) < apart(hue, centres[best]!) ? name : best), 'red');
};
