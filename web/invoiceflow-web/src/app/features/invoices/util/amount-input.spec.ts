import { formatAmountInput, parseAmountInput, roundMoney } from './amount-input';

describe('amount-input', () => {
  it('parses US, EU, and currency spellings', () => {
    expect(parseAmountInput('$93.50')).toBe(93.5);
    expect(parseAmountInput('93,50')).toBe(93.5);
    expect(parseAmountInput('8,5')).toBe(8.5);
    expect(parseAmountInput('1.234,56')).toBe(1234.56);
    expect(parseAmountInput('1,234.56')).toBe(1234.56);
    expect(parseAmountInput('USD 85.00')).toBe(85);
    expect(parseAmountInput('1,234')).toBe(1234);
  });

  it('returns null for blank or junk', () => {
    expect(parseAmountInput('')).toBeNull();
    expect(parseAmountInput('   ')).toBeNull();
    expect(parseAmountInput(null)).toBeNull();
    expect(parseAmountInput('abc')).toBeNull();
  });

  it('rounds to cents', () => {
    expect(roundMoney(93.505)).toBe(93.51);
    expect(parseAmountInput(93.5000000001)).toBe(93.5);
  });

  it('formats edit-box text with two decimals', () => {
    expect(formatAmountInput(121)).toBe('121.00');
    expect(formatAmountInput(93.5)).toBe('93.50');
    expect(formatAmountInput(null)).toBe('');
  });
});
