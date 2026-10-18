import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAppConfig } from '@core/config';
import { provideErrorHandling } from '@core/error-handling';
import { provideApiHttp } from '@core/http';
import { provideI18n } from '@core/i18n';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideAnimationsAsync(),
    // Route parameters arrive as component inputs (`id = input.required<string>()`).
    provideRouter(routes, withComponentInputBinding()),
    provideAppConfig(),
    provideApiHttp(),
    provideI18n(),
    provideErrorHandling(),
    // Icons come from the bundled "Material Icons Outlined" font, not a CDN.
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-icons-outlined');
    }),
  ],
};
