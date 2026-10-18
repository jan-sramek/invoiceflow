import { Injectable, signal } from '@angular/core';

/** Runtime settings served as `config.json` next to the app, so one build runs in every environment. */
export interface AppConfig {
  /** Base of every API call, without a trailing slash. Relative by default so the dev proxy and ingress both work. */
  readonly apiBaseUrl: string;
}

export const DEFAULT_APP_CONFIG: AppConfig = { apiBaseUrl: '/api' };

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly state = signal<AppConfig>(DEFAULT_APP_CONFIG);

  readonly config = this.state.asReadonly();

  get apiBaseUrl(): string {
    return this.state().apiBaseUrl;
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
  const value = (raw ?? {}) as Partial<AppConfig>;
  const apiBaseUrl =
    typeof value.apiBaseUrl === 'string' && value.apiBaseUrl.length > 0
      ? value.apiBaseUrl.replace(/\/+$/, '')
      : DEFAULT_APP_CONFIG.apiBaseUrl;
  return { apiBaseUrl };
}
