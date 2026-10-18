import { ErrorHandler, Injectable, inject } from '@angular/core';
import { ClientErrorCode, isApiError, ApiError } from '../http';
import { ErrorNotifier } from './error-notifier.service';

/**
 * Last resort for exceptions nobody caught. API errors were already shown by the error interceptor,
 * so they are only logged; anything else is a bug and gets a generic message.
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly notifier = inject(ErrorNotifier);

  handleError(error: unknown): void {
    console.error(error);
    if (isApiError(error)) {
      return;
    }
    this.notifier.show(
      new ApiError({ kind: 'unknown', status: 0, code: ClientErrorCode.Unknown, cause: error }),
    );
  }
}
