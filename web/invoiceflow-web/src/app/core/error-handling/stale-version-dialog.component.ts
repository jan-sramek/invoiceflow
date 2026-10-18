import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { TranslocoPipe } from '@jsverse/transloco';

/** Shown on 412: someone else changed the invoice. Closes with `true` when the user wants the current version. */
@Component({
  selector: 'if-stale-version-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, TranslocoPipe],
  template: `
    <h2 mat-dialog-title>{{ 'errors.stale.title' | transloco }}</h2>
    <mat-dialog-content>{{ 'errors.stale.message' | transloco }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton [mat-dialog-close]="false">{{ 'errors.stale.keep' | transloco }}</button>
      <button matButton="filled" [mat-dialog-close]="true" cdkFocusInitial>
        {{ 'errors.stale.reload' | transloco }}
      </button>
    </mat-dialog-actions>
  `,
})
export class StaleVersionDialogComponent {}
