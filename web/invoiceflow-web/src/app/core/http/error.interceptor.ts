import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, mergeMap, throwError } from 'rxjs';
import { AppConfigService } from '../config';
import { ApiError, ServerErrorCode } from './api-error';
import { API_ERROR_NOTIFIER, SUPPRESS_ERROR_NOTIFICATION, isApiRequest } from './http-context';
import { readBlobProblem, toApiError } from './to-api-error';

/**
 * Whether the global snackbar should report this error. Left to the caller: errors it asked to handle
 * itself, field errors (shown at the fields), a stale version (offered as a dialog) and cancelled calls.
 */
export function shouldNotify(error: ApiError, suppressed: boolean): boolean {
  return (
    !suppressed &&
    error.kind !== 'canceled' &&
    !error.hasFieldErrors &&
    error.code !== ServerErrorCode.StaleVersion
  );
}

/** Converts every failed API call into an {@link ApiError} and reports unhandled ones to the snackbar. */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const config = inject(AppConfigService);
  if (!isApiRequest(request, config.apiBaseUrl)) {
    return next(request);
  }
  const notify = inject(API_ERROR_NOTIFIER, { optional: true });
  const suppressed = request.context.get(SUPPRESS_ERROR_NOTIFICATION);

  return next(request).pipe(
    catchError((failure: unknown) =>
      from(readBlobProblem(failure)).pipe(
        mergeMap((readable) => {
          const error = toApiError(readable);
          if (notify && shouldNotify(error, suppressed)) {
            notify(error);
          }
          return throwError(() => error);
        }),
      ),
    ),
  );
};
