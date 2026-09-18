/** Public surface of @opendash/generator. */
export * from './model.ts';
export * as ncalc from './ncalc.ts';
export * as leds from './leds/index.ts';
export * from './ids.ts';
export * from './bounds.ts';
export * from './color.ts';
export * from './fonts.ts';
export * from './images.ts';
export * from './intFields.ts';
export * from './serialize.ts';
export * from './validate.ts';
export * from './package.ts';
// Two LED object models, because SimHub has two. The matrix (the flag box) is flat-exported; the
// strip is namespaced, because a strip naturally declares names the matrix already has.
export * from './leds/matrix.ts';
