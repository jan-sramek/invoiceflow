const EMPTY = '—';

export function formatMoney(
  value: number | null | undefined,
  currency: string | null | undefined,
  locale: string,
): string {
  if (value === null || value === undefined) {
    return EMPTY;
  }
  try {
    return new Intl.NumberFormat(locale, currency ? { style: 'currency', currency } : {}).format(
      value,
    );
  } catch {
    // An unknown currency code makes Intl throw; show the plain number instead.
    return currency ? `${value} ${currency}` : String(value);
  }
}

/** Formats a date-only value (`2026-03-01`) without shifting it by the browser's time zone. */
export function formatDate(value: string | null | undefined, locale: string): string {
  if (!value) {
    return EMPTY;
  }
  const date = new Date(value.length === 10 ? `${value}T00:00:00Z` : value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'medium',
    timeZone: value.length === 10 ? 'UTC' : undefined,
  }).format(date);
}

export function formatDateTime(value: string | null | undefined, locale: string): string {
  if (!value) {
    return EMPTY;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

export function formatBytes(bytes: number, locale: string): string {
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  const digits = unit === 0 ? 0 : 1;
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: digits }).format(value)} ${units[unit]}`;
}
