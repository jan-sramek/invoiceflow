import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { DevUserSwitcherComponent } from './dev-user-switcher.component';
import { LanguageSwitcherComponent } from './language-switcher.component';

/** The app frame: brand, navigation, the development-user and language switchers, and the routed page. */
@Component({
  selector: 'if-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TranslocoPipe, DevUserSwitcherComponent, LanguageSwitcherComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {}
