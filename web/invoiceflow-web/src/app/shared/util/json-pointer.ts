/**
 * Splits an RFC 6901 JSON Pointer into its decoded reference tokens.
 * `''` points at the whole document and yields no tokens; `/a~1b/0` yields `['a/b', '0']`.
 * A pointer without the leading slash is not valid JSON Pointer, but is tolerated as one token
 * so a slightly off server value still reaches the right control.
 */
export function parseJsonPointer(pointer: string | null | undefined): string[] {
  if (!pointer) {
    return [];
  }
  const raw = pointer.startsWith('/') ? pointer.slice(1) : pointer;
  return raw.split('/').map(unescapeToken);
}

/** Builds a JSON Pointer from tokens, escaping `~` and `/`. */
export function toJsonPointer(tokens: readonly string[]): string {
  return tokens.map((token) => '/' + token.replace(/~/g, '~0').replace(/\//g, '~1')).join('');
}

function unescapeToken(token: string): string {
  // The order matters: ~01 is the literal "~1", not "/".
  return token.replace(/~1/g, '/').replace(/~0/g, '~');
}
