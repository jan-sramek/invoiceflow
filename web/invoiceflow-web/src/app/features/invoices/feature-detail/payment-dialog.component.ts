import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { FieldErrorComponent } from '@shared/ui';

export interface PaymentDialogResult {
  readonly paidOn: string;
  readonly paymentReference: string;
}

const MAX_REFERENCE_LENGTH = 100;

function today(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/** Collects the payment date and reference for "mark as paid". Closes with the values, or `undefined` when cancelled. */
@Component({
  selector: 'if-payment-dialog',
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
    <h2 mat-dialog-title>{{ 'invoices.dialogs.markPaid.title' | transloco }}</h2>
    <form [formGroup]="form" (ngSubmit)="confirm()">
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'invoices.fields.paidOn' | transloco }}</mat-label>
          <input matInput type="date" formControlName="paidOn" [max]="max" required />
          <mat-error><if-field-error [control]="form.controls.paidOn" /></mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'invoices.fields.paymentReference' | transloco }}</mat-label>
          <input matInput formControlName="paymentReference" [maxlength]="maxReference" required />
          <mat-hint>{{ 'invoices.dialogs.markPaid.referenceHint' | transloco }}</mat-hint>
          <mat-error><if-field-error [control]="form.controls.paymentReference" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton type="button" [mat-dialog-close]="undefined">{{ 'app.common.cancel' | transloco }}</button>
        <button matButton="filled" type="submit">{{ 'invoices.actions.markPaid' | transloco }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .full {
      width: 100%;
    }
  `,
})
export class PaymentDialogComponent {
  private readonly ref = inject<MatDialogRef<PaymentDialogComponent, PaymentDialogResult | undefined>>(MatDialogRef);

  protected readonly max = today();
  protected readonly maxReference = MAX_REFERENCE_LENGTH;
  protected readonly form = inject(NonNullableFormBuilder).group({
    paidOn: [today(), [Validators.required]],
    paymentReference: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(MAX_REFERENCE_LENGTH)]],
  });

  protected confirm(): void {
    this.form.markAllAsTouched();
    if (this.form.valid) {
      const { paidOn, paymentReference } = this.form.getRawValue();
      this.ref.close({ paidOn, paymentReference: paymentReference.trim() });
    }
  }
}
