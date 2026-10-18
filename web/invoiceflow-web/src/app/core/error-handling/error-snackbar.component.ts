import { Clipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  MAT_SNACK_BAR_DATA,
  MatSnackBarAction,
  MatSnackBarActions,
  MatSnackBarLabel,
  MatSnackBarRef,
} from '@angular/material/snack-bar';
import { TranslocoPipe } from '@jsverse/transloco';

export interface ErrorSnackbarData {
  readonly message: string;
  readonly traceId?: string;
}

/** Error toast with the trace ID in a form that is easy to copy into a bug report. */
@Component({
  selector: 'if-error-snackbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatSnackBarLabel, MatSnackBarActions, MatSnackBarAction, TranslocoPipe],
  template: `
    <div matSnackBarLabel class="message" role="alert">
      <span class="text">{{ data.message }}</span>
      @if (data.traceId) {
        <span class="trace">
          {{ 'errors.traceId' | transloco }}: <code>{{ data.traceId }}</code>
        </span>
      }
    </div>
    <div matSnackBarActions>
      @if (data.traceId) {
        <button matButton matSnackBarAction type="button" (click)="copy()">
          <mat-icon>{{ copied() ? 'check' : 'content_copy' }}</mat-icon>
          {{ (copied() ? 'errors.traceCopied' : 'errors.copyTraceId') | transloco }}
        </button>
      }
      <button
        matIconButton
        matSnackBarAction
        type="button"
        [attr.aria-label]="'app.common.close' | transloco"
        (click)="ref.dismiss()"
      >
        <mat-icon>close</mat-icon>
      </button>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .message {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .trace {
      font-size: 12px;
      opacity: 0.85;
    }
    code {
      font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace;
      user-select: all;
    }
  `,
})
export class ErrorSnackbarComponent {
  protected readonly data = inject<ErrorSnackbarData>(MAT_SNACK_BAR_DATA);
  protected readonly ref = inject(MatSnackBarRef);
  private readonly clipboard = inject(Clipboard);

  protected readonly copied = signal(false);

  protected copy(): void {
    if (this.data.traceId && this.clipboard.copy(this.data.traceId)) {
      this.copied.set(true);
    }
  }
}
