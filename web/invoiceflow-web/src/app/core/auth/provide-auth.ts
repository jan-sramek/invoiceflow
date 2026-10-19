import { EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import {
  LogLevel,
  StsConfigLoader,
  StsConfigStaticLoader,
  provideAuth as provideOidcAuth,
  withAppInitializerAuthCheck,
} from 'angular-auth-oidc-client';
import { AppConfigService } from '../config';

/** Registers OIDC when `config.json` has an authority; otherwise a no-op loader. */
export function provideAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideOidcAuth(
      {
        loader: {
          provide: StsConfigLoader,
          useFactory: () => {
            const config = inject(AppConfigService);
            const oidc = config.oidc;
            if (!oidc) {
              return new StsConfigStaticLoader([]);
            }
            return new StsConfigStaticLoader({
              authority: oidc.authority,
              redirectUrl: `${window.location.origin}`,
              postLogoutRedirectUri: `${window.location.origin}`,
              clientId: oidc.clientId,
              scope: 'openid profile email',
              responseType: 'code',
              silentRenew: true,
              useRefreshToken: true,
              renewTimeBeforeTokenExpiresInSeconds: 30,
              logLevel: LogLevel.Warn,
              secureRoutes: [config.apiBaseUrl.startsWith('http') ? config.apiBaseUrl : `${window.location.origin}${config.apiBaseUrl}`],
            });
          },
          deps: [AppConfigService],
        },
      },
      withAppInitializerAuthCheck(),
    ),
  ]);
}
