/** Public surface of @opendash/dash. */
export { ds, DS_TOKEN_PATHS, TRANSPARENT, resolveToken, tokenExists, tokenNode, type DesignSystem } from './tokens.ts';
export * from './contract.ts';
export * from './design/metrics.ts';
export * from './design/geometry.ts';
export * from './design/rung.ts';
export * from './elements/index.ts';
export * from './components/index.ts';
export { CARDS, cardByNumber, defineCard, type Card, type CardBuilder } from './cards/index.ts';
export * from './cards/chars.ts';
export { hero, revItems, gearItems, flagItems, type HeroGeometry, type RevVariant, type GearVariant, type FlagVariant } from './hero/hero.ts';
export * from './layouts/index.ts';
export * from './slots.ts';
export * from './walk.ts';
export * from './dashboard.ts';
export { build, main, parseArgs, readVersion, validateOrThrow, BuildError, DEFAULT_OUT_DIR, MANIFEST_FILE, MANIFEST_SCHEMA_VERSION, STRATEGIES, STRATEGY_ENV, USAGE, type BuildArgs, type BuildOptions, type BuildResult, type BuiltPackage, type Manifest, type ManifestEntry } from './build.ts';
export { formula, bindings, withMoreBindings, type BindingSpec, type Expr } from './bind.ts';
