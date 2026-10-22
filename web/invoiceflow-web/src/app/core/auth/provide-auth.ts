import { EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import {
  LogLevel,
  type OpenIdConfiguration,
  StsConfigLoader,
  provideAuth as provideOidcAuth,
  withAppInitializerAuthCheck,
} from 'angular-auth-oidc-client';
import { from, type Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AppConfigService } from '../config';

/**
 * Loads OIDC options from `config.json` at `loadConfigs()` time.
 * Must not read config in the factory: `withAppInitializerAuthCheck` resolves
 * `OidcSecurityService` (and thus this loader) while registering APP_INITIALIZER
 * deps, before `config.json` has been fetched.
 */
class AppConfigOidcLoader implements StsConfigLoader {
  private readonly appConfig = inject(AppConfigService);

  loadConfigs(): Observable<OpenIdConfiguration[]> {
    return from(this.appConfig.ensureLoaded()).pipe(map(() => this.configs()));
  }

  private configs(): OpenIdConfiguration[] {
    const oidc = this.appConfig.oidc;
    if (!oidc) {
      return [];
    }

    return [
      {
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
        // HttpClient calls use relative `/api/...`; match that. Also keep the absolute
        // origin form in case a caller builds a full URL.
        secureRoutes: apiSecureRoutes(this.appConfig.apiBaseUrl),
      },
    ];
  }
}

function apiSecureRoutes(apiBaseUrl: string): string[] {
  if (apiBaseUrl.startsWith('http')) {
    return [apiBaseUrl];
  }
  const relative = apiBaseUrl.startsWith('/') ? apiBaseUrl : `/${apiBaseUrl}`;
  return [relative, `${window.location.origin}${relative}`];
}

/** Registers OIDC when `config.json` has an authority; otherwise a no-op loader. */
export function provideAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideOidcAuth(
      {
        loader: {
          provide: StsConfigLoader,
          useClass: AppConfigOidcLoader,
        },
      },
      withAppInitializerAuthCheck(),
    ),
  ]);
}
