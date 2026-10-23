/** Money scale used by the API (`numeric(18,2)` / domain `Money.MaxDecimals`). */
export const AMOUNT_DECIMALS = 2;

/** Round to cents so float noise from `type=number` does not trip `validation.amount_precision`. */
export function roundMoney(value: number): number {
  const factor = 10 ** AMOUNT_DECIMALS;
  return Math.round(value * factor) / factor;
}

/**
 * Parse an amount the user typed or pasted. Accepts `93.50`, `93,50`, `1.234,56`, `1,234.56`, `$93.50`.
 * Returns `null` for blank or unparseable input.
 */
export function parseAmountInput(raw: string | number | null | undefined): number | null {
  if (raw === null || raw === undefined) {
    return null;
  }
  if (typeof raw === 'number') {
    return Number.isFinite(raw) ? roundMoney(raw) : null;
  }

  const trimmed = raw.trim();
  if (trimmed === '') {
    return null;
  }

  let cleaned = '';
  for (const c of trimmed) {
    if (cleaned.length === 0 && c === '-') {
      cleaned += c;
      continue;
    }
    if ((c >= '0' && c <= '9') || c === '.' || c === ',') {
      cleaned += c;
    }
  }

  if (cleaned === '' || cleaned === '-') {
    return null;
  }

  const lastDot = cleaned.lastIndexOf('.');
  const lastComma = cleaned.lastIndexOf(',');

  if (lastDot >= 0 && lastComma >= 0) {
    const decimalSep = lastDot > lastComma ? '.' : ',';
    const thousandsSep = decimalSep === '.' ? ',' : '.';
    cleaned = cleaned.split(thousandsSep).join('').replace(decimalSep, '.');
  } else if (lastComma >= 0) {
    const digitsAfter = cleaned.length - lastComma - 1;
    const head = cleaned.slice(0, lastComma).replace(/^-/, '');
    const thousands = digitsAfter === 3 && lastComma > 0 && /^[0-9]+$/.test(head);
    cleaned = thousands ? cleaned.split(',').join('') : cleaned.replace(',', '.');
  }

  const parsed = Number(cleaned);
  return Number.isFinite(parsed) ? roundMoney(parsed) : null;
}

/** Stable edit-box text (always dot decimal) so `cs` / `de` locales do not break `type="number"`. */
export function formatAmountInput(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '';
  }
  return roundMoney(value).toFixed(AMOUNT_DECIMALS);
}
