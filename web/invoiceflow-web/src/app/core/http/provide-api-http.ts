import { provideHttpClient, withInterceptors } from '@angular/common/http';
import type { EnvironmentProviders } from '@angular/core';
import { bearerInterceptor } from '../auth';
import { devUserInterceptor } from './dev-user.interceptor';
import { errorInterceptor } from './error.interceptor';
import { retryInterceptor } from './retry.interceptor';

/**
 * HttpClient with the API interceptors. The first one listed is the outermost: the error interceptor
 * wraps the retry interceptor, so it only sees an error once all retries are used up.
 */
export function provideApiHttp(): EnvironmentProviders {
  return provideHttpClient(
    withInterceptors([bearerInterceptor, devUserInterceptor, errorInterceptor, retryInterceptor]),
  );
}
