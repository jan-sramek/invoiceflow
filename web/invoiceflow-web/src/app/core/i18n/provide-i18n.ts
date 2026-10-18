import { MatPaginatorIntl } from '@angular/material/paginator';
import { inject, isDevMode, provideAppInitializer, type Provider, type EnvironmentProviders } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { DEFAULT_LANGUAGE, LANGUAGES, LanguageService } from './language.service';
import { TranslatedPaginatorIntl } from './translated-paginator-intl';
import { TranslocoHttpLoader } from './transloco-http-loader';

/**
 * Transloco with English and Czech, English as fallback, and the start-up language loaded before the first render.
 * Missing keys are not logged: `npm run i18n:check` keeps the files in step, and the UI falls back to English.
 */
export function provideI18n(): (Provider | EnvironmentProviders | EnvironmentProviders[])[] {
  return [
    provideTransloco({
      config: {
        availableLangs: [...LANGUAGES],
        defaultLang: DEFAULT_LANGUAGE,
        fallbackLang: DEFAULT_LANGUAGE,
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
        missingHandler: { logMissingKey: false },
      },
      loader: TranslocoHttpLoader,
    }),
    { provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl },
    provideAppInitializer(() => inject(LanguageService).init()),
  ];
}
