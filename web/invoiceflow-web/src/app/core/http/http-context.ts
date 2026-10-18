import { HttpContextToken, HttpRequest } from '@angular/common/http';
import { InjectionToken } from '@angular/core';
import type { ApiError } from './api-error';

/** Set on a request whose caller shows its own error, so no snackbar appears for it. */
export const SUPPRESS_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);

/** Set on a request that must not be retried even though it would qualify. */
export const DISABLE_RETRY = new HttpContextToken<boolean>(() => false);

/** Where the error interceptor reports errors that nobody handles locally; provided by core/error-handling. */
export const API_ERROR_NOTIFIER = new InjectionToken<(error: ApiError) => void>('API_ERROR_NOTIFIER');

export function isApiRequest(request: HttpRequest<unknown>, apiBaseUrl: string): boolean {
  return request.url === apiBaseUrl || request.url.startsWith(apiBaseUrl + '/');
}
