/**
 * The one door from @opendash/dash into @opendash/generator: the scene-graph model types, the
 * NCalc helpers, the serialiser, the validator and the package writer. Everything in this
 * package imports them from here, so the dependency on the generator is visible in one place
 * and its entry point can change without touching the rest of the dash.
 */
export * from '@opendash/generator';
