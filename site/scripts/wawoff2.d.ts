// wawoff2 ships no types; this is the one function fonts.ts calls, as compress.js defines it: it
// resolves to the woff2 bytes and throws when the conversion fails.
declare module 'wawoff2' {
  export function compress(ttf: Uint8Array): Promise<Uint8Array>;
}
