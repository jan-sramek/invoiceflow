import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoPipe } from '@jsverse/transloco';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { map } from 'rxjs';

/** Signed-in user menu and Sign in / Sign out for Keycloak OIDC. */
@Component({
  selector: 'if-auth-user',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatMenuModule, MatIconModule, TranslocoPipe],
  template: `
    @if (authenticated()) {
      <button type="button" class="trigger" [matMenuTriggerFor]="menu">
        <mat-icon aria-hidden="true">account_circle</mat-icon>
        <strong>{{ displayName() }}</strong>
        <mat-icon aria-hidden="true">arrow_drop_down</mat-icon>
      </button>
      <mat-menu #menu="matMenu" xPosition="before">
        <button mat-menu-item type="button" (click)="logOff()">
          <mat-icon>logout</mat-icon>
          <span>{{ 'app.auth.signOut' | transloco }}</span>
        </button>
      </mat-menu>
    } @else {
      <button type="button" class="trigger" (click)="logIn()">
        <mat-icon aria-hidden="true">login</mat-icon>
        <span>{{ 'app.auth.signIn' | transloco }}</span>
      </button>
    }
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
      padding: 4px 10px;
      cursor: pointer;
    }
    .trigger:hover {
      background: var(--if-header-active-bg);
    }
    .trigger:focus-visible {
      outline: 2px solid var(--if-focus-ring);
      outline-offset: 2px;
    }
    mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
  `,
})
export class AuthUserComponent {
  private readonly oidc = inject(OidcSecurityService);

  protected readonly authenticated = toSignal(
    this.oidc.isAuthenticated$.pipe(map((state) => state.isAuthenticated)),
    { initialValue: false },
  );

  private readonly userData = toSignal(this.oidc.userData$, {
    initialValue: { userData: null, allUserData: [] },
  });

  protected readonly displayName = computed(() => {
    const data = this.userData().userData as { name?: string; preferred_username?: string } | null;
    return data?.name || data?.preferred_username || '';
  });

  protected logIn(): void {
    this.oidc.authorize();
  }

  protected logOff(): void {
    this.oidc.logoff().subscribe();
  }
}
