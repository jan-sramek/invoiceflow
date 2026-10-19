import { Injectable, signal } from '@angular/core';

/** OIDC settings when Keycloak is configured; omit for local DevelopmentUsers. */
export interface OidcConfig {
  readonly authority: string;
  readonly clientId: string;
}

/** Runtime settings served as `config.json` next to the app, so one build runs in every environment. */
export interface AppConfig {
  /** Base of every API call, without a trailing slash. Relative by default so the dev proxy and ingress both work. */
  readonly apiBaseUrl: string;
  /** When true (and OIDC is off), the Clara/Adam/Fiona switcher sends X-Development-User. */
  readonly developmentUsers: boolean;
  readonly oidc: OidcConfig | null;
}

export const DEFAULT_APP_CONFIG: AppConfig = {
  apiBaseUrl: '/api',
  developmentUsers: true,
  oidc: null,
};

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly state = signal<AppConfig>(DEFAULT_APP_CONFIG);

  readonly config = this.state.asReadonly();

  get apiBaseUrl(): string {
    return this.state().apiBaseUrl;
  }

  get developmentUsers(): boolean {
    return this.state().developmentUsers;
  }

  get oidc(): OidcConfig | null {
    return this.state().oidc;
  }

  get usesOidc(): boolean {
    return this.state().oidc !== null;
  }

  /** Loads `config.json`; falls back to the defaults so a missing file never blocks start-up. */
  async load(url = 'config.json'): Promise<void> {
    try {
      const response = await fetch(url, { cache: 'no-store' });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      this.state.set(normalize(await response.json()));
    } catch (error) {
      console.warn(`Could not load ${url}; using default configuration.`, error);
      this.state.set(DEFAULT_APP_CONFIG);
    }
  }
}

export function normalize(raw: unknown): AppConfig {
  const value = (raw ?? {}) as Partial<AppConfig> & { oidc?: Partial<OidcConfig> | null };
  const apiBaseUrl =
    typeof value.apiBaseUrl === 'string' && value.apiBaseUrl.length > 0
      ? value.apiBaseUrl.replace(/\/+$/, '')
      : DEFAULT_APP_CONFIG.apiBaseUrl;
  const developmentUsers = value.developmentUsers !== false;
  const oidc = normalizeOidc(value.oidc);
  return {
    apiBaseUrl,
    developmentUsers: oidc ? false : developmentUsers,
    oidc,
  };
}

function normalizeOidc(raw: Partial<OidcConfig> | null | undefined): OidcConfig | null {
  if (!raw || typeof raw.authority !== 'string' || raw.authority.length === 0) {
    return null;
  }
  if (typeof raw.clientId !== 'string' || raw.clientId.length === 0) {
    return null;
  }
  return {
    authority: raw.authority.replace(/\/+$/, ''),
    clientId: raw.clientId,
  };
}
