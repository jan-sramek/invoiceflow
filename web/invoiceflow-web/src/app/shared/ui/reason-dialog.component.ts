import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { FieldErrorComponent } from './field-error.component';

export interface ReasonDialogData {
  /** Translation keys, so the dialog works for reject, cancel and "not a duplicate" alike. */
  readonly titleKey: string;
  readonly labelKey: string;
  readonly confirmKey: string;
  readonly hintKey?: string;
  readonly maxLength?: number;
}

/** Asks for a mandatory reason. Closes with the trimmed reason, or `undefined` when cancelled. */
@Component({
  selector: 'if-reason-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
    FieldErrorComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | transloco }}</h2>
    <mat-dialog-content>
      @if (data.hintKey) {
        <p class="hint">{{ data.hintKey | transloco }}</p>
      }
      <mat-form-field appearance="outline" class="full">
        <mat-label>{{ data.labelKey | transloco }}</mat-label>
        <textarea
          matInput
          rows="3"
          [formControl]="reason"
          [maxlength]="maxLength"
          (keydown.control.enter)="confirm()"
        ></textarea>
        <mat-hint align="end">{{ reason.value.length }} / {{ maxLength }}</mat-hint>
        <mat-error><if-field-error [control]="reason" /></mat-error>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" [mat-dialog-close]="undefined">
        {{ 'app.common.cancel' | transloco }}
      </button>
      <button matButton="filled" type="button" (click)="confirm()">
        {{ data.confirmKey | transloco }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full {
      width: 100%;
    }
    .hint {
      margin: 0 0 12px;
      color: var(--if-text-muted);
    }
  `,
})
export class ReasonDialogComponent {
  protected readonly data = inject<ReasonDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<ReasonDialogComponent, string | undefined>>(MatDialogRef);

  protected readonly maxLength = this.data.maxLength ?? 1000;
  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(this.maxLength)],
  });

  protected confirm(): void {
    this.reason.markAsTouched();
    if (this.reason.valid) {
      this.ref.close(this.reason.value.trim());
    }
  }
}
