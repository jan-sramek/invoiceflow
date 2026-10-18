/** A fresh key for one upload attempt of one file. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}
