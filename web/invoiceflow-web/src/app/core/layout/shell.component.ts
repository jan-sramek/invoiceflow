import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthUserComponent } from '../auth';
import { AppConfigService } from '../config';
import { DevUserSwitcherComponent } from './dev-user-switcher.component';
import { LanguageSwitcherComponent } from './language-switcher.component';

/** The app frame: brand, navigation, sign-in and language switchers, and the routed page. */
@Component({
  selector: 'if-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    RouterLinkActive,
    TranslocoPipe,
    AuthUserComponent,
    DevUserSwitcherComponent,
    LanguageSwitcherComponent,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly config = inject(AppConfigService);

  protected readonly usesOidc = computed(() => this.config.usesOidc);
  protected readonly developmentUsers = computed(() => this.config.developmentUsers);
}
