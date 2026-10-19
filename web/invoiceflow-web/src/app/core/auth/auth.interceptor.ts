import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { authInterceptor } from 'angular-auth-oidc-client';
import { AppConfigService } from '../config';

const oidcBearer = authInterceptor();

function isApiRequest(request: HttpRequest<unknown>, apiBaseUrl: string): boolean {
  return request.url === apiBaseUrl || request.url.startsWith(`${apiBaseUrl}/`);
}

/** Adds the Bearer token on API calls when OIDC is configured; otherwise a no-op. */
export const bearerInterceptor: HttpInterceptorFn = (request, next) => {
  const config = inject(AppConfigService);
  if (!config.usesOidc || !isApiRequest(request, config.apiBaseUrl)) {
    return next(request);
  }
  return oidcBearer(request, next);
};
