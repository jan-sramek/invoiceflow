import { pickInitialLanguage } from './language.service';

describe('pickInitialLanguage', () => {
  it('prefers the saved choice', () => {
    expect(pickInitialLanguage('cs', ['en-US'])).toBe('cs');
  });

  it('ignores a saved value that is not a supported language', () => {
    expect(pickInitialLanguage('fr', ['cs-CZ'])).toBe('cs');
  });

  it('uses the first supported browser language, by its base tag', () => {
    expect(pickInitialLanguage(null, ['de-DE', 'cs-CZ', 'en'])).toBe('de');
    expect(pickInitialLanguage(null, ['EN-gb'])).toBe('en');
  });

  it('falls back to English', () => {
    expect(pickInitialLanguage(null, ['fr', 'it'])).toBe('en');
    expect(pickInitialLanguage(null, [])).toBe('en');
  });
});
