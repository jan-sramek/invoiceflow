import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AppConfigService } from '../config';
import { DEV_USER_HEADER, DevUserService } from './dev-user.service';
import { isApiRequest } from './http-context';

/** Adds `X-Development-User` to every API call. Other origins never see it. */
export const devUserInterceptor: HttpInterceptorFn = (request, next) => {
  const config = inject(AppConfigService);
  if (!isApiRequest(request, config.apiBaseUrl)) {
    return next(request);
  }
  const user = inject(DevUserService).user();
  return next(request.clone({ setHeaders: { [DEV_USER_HEADER]: user } }));
};
