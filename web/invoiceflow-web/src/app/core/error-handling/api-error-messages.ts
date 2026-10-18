import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { formatBytes } from '@shared/util';
import { ClientErrorCode, toApiError } from '../http';
import { LanguageService } from '../i18n';

/** Turns errors into text in the active language, always from the error's `code` and `params`. */
@Injectable({ providedIn: 'root' })
export class ApiErrorMessages {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  /**
   * Looks for `errors.<code>`, then the status (`errors.http.502`), then its class (`errors.http.5xx`),
   * and ends at a generic message, so every error has some text.
   */
  message(error: unknown): string {
    const apiError = toApiError(error);
    const params = this.withDerivedParams(apiError.params);
    const candidates = [`errors.${apiError.code}`];
    if (apiError.status > 0) {
      candidates.push(`errors.http.${apiError.status}`, `errors.http.${Math.floor(apiError.status / 100)}xx`);
    }
    candidates.push(`errors.${ClientErrorCode.Unknown}`);

    for (const key of candidates) {
      const text = this.transloco.translate(key, params);
      if (text && text !== key) {
        return text;
      }
    }
    return apiError.title ?? apiError.code;
  }

  /** Message for one field error of a problem response. */
  fieldMessage(code: string, params: Readonly<Record<string, unknown>> = {}): string {
    const key = `errors.${code}`;
    const text = this.transloco.translate(key, this.withDerivedParams(params));
    return text && text !== key ? text : code;
  }

  /** Adds readable forms of raw params: `maxBytes` becomes `maxSize` ("10 MB"). */
  private withDerivedParams(params: Readonly<Record<string, unknown>>): Record<string, unknown> {
    const derived: Record<string, unknown> = { ...params };
    const maxBytes = params['maxBytes'];
    if (typeof maxBytes === 'number') {
      derived['maxSize'] = formatBytes(maxBytes, this.language.locale());
    }
    return derived;
  }
}
