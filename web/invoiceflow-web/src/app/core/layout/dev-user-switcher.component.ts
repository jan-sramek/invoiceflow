import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoPipe } from '@jsverse/transloco';
import { DEV_USERS, DevUser, DevUserService } from '../http';

/**
 * Chooses which persona's `X-Development-User` header the app sends. Stands in for sign-in
 * until Keycloak is wired up, so it says plainly that it is a development tool.
 */
@Component({
  selector: 'if-dev-user-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatMenuModule, MatIconModule, TranslocoPipe],
  template: `
    <button
      type="button"
      class="trigger"
      [matMenuTriggerFor]="menu"
      [attr.title]="'app.devUser.hint' | transloco"
    >
      <mat-icon aria-hidden="true">account_circle</mat-icon>
      <span class="label">{{ 'app.devUser.label' | transloco }}</span>
      <strong>{{ ('app.devUser.' + devUser.user()) | transloco }}</strong>
      <mat-icon aria-hidden="true">arrow_drop_down</mat-icon>
    </button>
    <mat-menu #menu="matMenu" xPosition="before">
      @for (user of users; track user) {
        <button mat-menu-item type="button" (click)="select(user)" [attr.aria-current]="user === devUser.user()">
          <mat-icon>{{ user === devUser.user() ? 'radio_button_checked' : 'radio_button_unchecked' }}</mat-icon>
          <span>{{ 'app.devUser.' + user | transloco }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: `
    .trigger {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      border: 1px solid var(--if-header-border);
      border-radius: var(--if-radius-sm);
      background: transparent;
      color: var(--if-header-text);
      font: inherit;
      font-size: 13px;
      padding: 4px 6px 4px 10px;
      cursor: pointer;
    }
    .trigger:hover {
      background: var(--if-header-active-bg);
    }
    .trigger:focus-visible {
      outline: 2px solid var(--if-focus-ring);
      outline-offset: 2px;
    }
    .label {
      color: var(--if-header-text-muted);
    }
    mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    @media (max-width: 720px) {
      .label {
        display: none;
      }
    }
  `,
})
export class DevUserSwitcherComponent {
  protected readonly devUser = inject(DevUserService);
  protected readonly users = DEV_USERS;

  protected select(user: DevUser): void {
    this.devUser.select(user);
  }
}
