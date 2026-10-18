import { formatBytes, formatDate, formatMoney } from './format';

describe('format', () => {
  it('shows a dash for missing values', () => {
    expect(formatMoney(null, 'EUR', 'en')).toBe('—');
    expect(formatDate(null, 'en')).toBe('—');
  });

  it('formats money and survives an unknown currency', () => {
    expect(formatMoney(12.5, 'EUR', 'en')).toContain('12.50');
    expect(formatMoney(12.5, 'not-a-code', 'en')).toBe('12.5 not-a-code');
    expect(formatMoney(12.5, null, 'en')).toBe('12.5');
  });

  it('does not shift date-only values by the time zone', () => {
    expect(formatDate('2026-03-01', 'en')).toContain('1');
    expect(formatDate('2026-03-01', 'en')).toContain('2026');
    expect(formatDate('2026-03-01', 'en')).toContain('Mar');
  });

  it('formats byte sizes', () => {
    expect(formatBytes(512, 'en')).toBe('512 B');
    expect(formatBytes(1536, 'en')).toBe('1.5 KB');
    expect(formatBytes(10 * 1024 * 1024, 'en')).toBe('10 MB');
  });
});
