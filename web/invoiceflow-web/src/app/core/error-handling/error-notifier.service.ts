import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarRef } from '@angular/material/snack-bar';
import { Observable, map } from 'rxjs';
import { toApiError } from '../http';
import { ApiErrorMessages } from './api-error-messages';
import { ErrorSnackbarComponent, ErrorSnackbarData } from './error-snackbar.component';
import { StaleVersionDialogComponent } from './stale-version-dialog.component';

const SNACKBAR_DURATION_MS = 15_000;

@Injectable({ providedIn: 'root' })
export class ErrorNotifier {
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly messages = inject(ApiErrorMessages);

  private current: { ref: MatSnackBarRef<ErrorSnackbarComponent>; key: string } | null = null;

  /** Shows the error in a snackbar with its trace ID. The same error twice in a row shows once. */
  show(error: unknown): void {
    const apiError = toApiError(error);
    const data: ErrorSnackbarData = { message: this.messages.message(apiError), traceId: apiError.traceId };
    const key = `${data.message}|${data.traceId ?? ''}`;

    if (this.current?.key === key) {
      return;
    }
    const ref = this.snackBar.openFromComponent(ErrorSnackbarComponent, {
      data,
      duration: SNACKBAR_DURATION_MS,
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
      panelClass: 'if-error-snackbar',
    });
    this.current = { ref, key };
    ref.afterDismissed().subscribe(() => {
      if (this.current?.ref === ref) {
        this.current = null;
      }
    });
  }

  /** Asks whether to reload an invoice someone else changed. Emits `true` for reload, `false` to keep editing. */
  confirmReloadAfterStaleVersion(): Observable<boolean> {
    return this.dialog
      .open<StaleVersionDialogComponent, void, boolean>(StaleVersionDialogComponent, {
        width: '440px',
        maxWidth: '92vw',
      })
      .afterClosed()
      .pipe(map((reload) => reload === true));
  }
}
