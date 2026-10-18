import { ErrorHandler, inject, type Provider } from '@angular/core';
import { API_ERROR_NOTIFIER } from '../http';
import { ErrorNotifier } from './error-notifier.service';
import { GlobalErrorHandler } from './global-error-handler';

/** Connects the error interceptor to the snackbar and installs the global error handler. */
export function provideErrorHandling(): Provider[] {
  return [
    {
      provide: API_ERROR_NOTIFIER,
      useFactory: () => {
        const notifier = inject(ErrorNotifier);
        return (error: unknown) => notifier.show(error);
      },
    },
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
  ];
}
