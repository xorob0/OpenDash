/**
 * SimHub RGB LED profiles. Exported from the package root as `leds` rather than flat, because an
 * LED profile naturally declares names — `validateProfile`, `LedColor`, `LedSegment` — that would
 * collide with the dashboard model in a flat re-export. `ncalc` is the precedent.
 */
export * from './model.ts';
export * from './serialize.ts';
export * from './validate.ts';
export * from './write.ts';
