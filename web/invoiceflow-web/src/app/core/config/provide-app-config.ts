import { inject, provideAppInitializer, type EnvironmentProviders } from '@angular/core';
import { AppConfigService } from './app-config.service';

/** Loads `config.json` before the app starts. */
export function provideAppConfig(): EnvironmentProviders {
  return provideAppInitializer(() => inject(AppConfigService).ensureLoaded());
}
