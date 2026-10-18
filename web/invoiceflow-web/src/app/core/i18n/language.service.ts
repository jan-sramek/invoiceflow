import { DOCUMENT } from '@angular/common';
import { Injectable, Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';

export const LANGUAGES = ['en', 'cs'] as const;
export type Language = (typeof LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'en';

const STORAGE_KEY = 'invoiceflow.lang';
const LOCALES: Record<Language, string> = { en: 'en-GB', cs: 'cs-CZ' };

export function isLanguage(value: unknown): value is Language {
  return typeof value === 'string' && (LANGUAGES as readonly string[]).includes(value);
}

/** The saved choice, else the browser's language when supported, else English. */
export function pickInitialLanguage(stored: string | null, browser: readonly string[]): Language {
  if (isLanguage(stored)) {
    return stored;
  }
  for (const tag of browser) {
    const base = tag.toLowerCase().split('-')[0];
    if (isLanguage(base)) {
      return base;
    }
  }
  return DEFAULT_LANGUAGE;
}

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);

  readonly available = LANGUAGES;

  readonly lang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  }) as Signal<Language>;

  /** BCP 47 tag for `Intl` formatting of dates and money in the active language. */
  readonly locale = computed(() => LOCALES[this.lang()] ?? LOCALES[DEFAULT_LANGUAGE]);

  /** Picks the start-up language and loads its texts, so the first screen never shows keys. */
  async init(): Promise<void> {
    const lang = pickInitialLanguage(this.readStored(), globalThis.navigator?.languages ?? []);
    await firstValueFrom(this.transloco.load(lang));
    this.apply(lang);
  }

  use(lang: Language): void {
    this.apply(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Storage may be blocked; the choice then lasts until reload.
    }
  }

  private apply(lang: Language): void {
    this.transloco.setActiveLang(lang);
    this.document.documentElement.lang = lang;
  }

  private readStored(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
