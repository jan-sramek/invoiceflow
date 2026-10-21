import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { ApiErrorMessages } from '@core/error-handling';
import { LanguageService } from '@core/i18n';
import { TranslocoPipe } from '@jsverse/transloco';
import type { FieldStatus, InvoiceFieldsDto, InvoiceFieldsInput } from '@shared/api';
import {
  BytesPipe,
  DatePipe,
  DateTimePipe,
  FieldErrorComponent,
  MoneyPipe,
  ReasonDialogComponent,
  type ReasonDialogData,
} from '@shared/ui';
import { FieldErrorLike, applyFieldErrors } from '@shared/util';
import { filter } from 'rxjs';
import { InvoiceDetailStore } from '../data-access';
import { AuditTimelineComponent, InvoiceFlagsComponent, InvoiceStatusChipComponent } from '../ui';
import { PaymentDialogComponent, PaymentDialogResult } from './payment-dialog.component';

type FieldsFormValue = ReturnType<typeof toFormValue>;

const EMPTY_FORM_VALUE = toFormValue(null);

@Component({
  selector: 'if-invoice-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [InvoiceDetailStore],
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    TranslocoPipe,
    BytesPipe,
    DatePipe,
    DateTimePipe,
    MoneyPipe,
    FieldErrorComponent,
    AuditTimelineComponent,
    InvoiceFlagsComponent,
    InvoiceStatusChipComponent,
  ],
  templateUrl: './invoice-detail-page.component.html',
  styleUrl: './invoice-detail-page.component.scss',
})
export class InvoiceDetailPageComponent {
  /** Route parameter, bound by `withComponentInputBinding`. */
  readonly id = input.required<string>();

  protected readonly store = inject(InvoiceDetailStore);
  protected readonly language = inject(LanguageService);
  protected readonly errorMessages = inject(ApiErrorMessages);
  private readonly dialog = inject(MatDialog);

  protected readonly form = inject(NonNullableFormBuilder).group({
    supplierName: ['', [Validators.maxLength(200)]],
    invoiceNumber: [''],
    invoiceDate: [''],
    dueDate: [''],
    currency: ['', [Validators.maxLength(3)]],
    subtotal: [null as number | null],
    taxAmount: [null as number | null],
    totalAmount: [null as number | null],
    purchaseOrderReference: [''],
  });

  /** Field errors that name no field on the form (for example a rule about the whole invoice). */
  protected readonly unmapped = signal<readonly FieldErrorLike[]>([]);

  protected readonly editable = computed(() => this.store.capabilities()?.canEditFields ?? false);
  protected readonly title = computed(() => {
    const invoice = this.store.invoice();
    return invoice?.fields.supplierName || invoice?.document.originalFileName || '';
  });
  protected readonly loadErrorText = computed(() => {
    const error = this.store.loadError();
    return error ? this.errorMessages.message(error) : '';
  });
  protected readonly notFound = computed(() => this.store.loadError()?.status === 404);

  protected fieldStatus(field: keyof InvoiceFieldsDto): FieldStatus | null {
    const statuses = this.store.invoice()?.fieldStatuses;
    return statuses ? statuses[field] : null;
  }

  protected showFieldStatus(status: FieldStatus | null): boolean {
    return status === 'Invalid' || status === 'Unverified';
  }

  constructor() {
    this.store.load(this.id);
    this.store.poll(this.store.extracting);

    // Show the invoice that was loaded, saved or changed by someone else.
    effect(() => {
      const invoice = this.store.invoice();
      untracked(() => {
        this.form.reset(invoice ? toFormValue(invoice.fields) : EMPTY_FORM_VALUE);
      });
    });

    // Put the server's field errors on the matching controls; the rest is listed above the form.
    effect(() => {
      const errors = this.store.fieldErrors();
      untracked(() => {
        this.unmapped.set(errors.length > 0 ? applyFieldErrors(this.form, errors) : []);
      });
    });
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.valid) {
      this.store.saveFields(toInput(this.form.getRawValue()));
    }
  }

  protected discard(): void {
    const invoice = this.store.invoice();
    this.form.reset(invoice ? toFormValue(invoice.fields) : EMPTY_FORM_VALUE);
  }

  protected reject(): void {
    this.askReason({
      titleKey: 'invoices.dialogs.reject.title',
      hintKey: 'invoices.dialogs.reject.hint',
      labelKey: 'invoices.dialogs.reason',
      confirmKey: 'invoices.actions.reject',
    }).subscribe((reason) => this.store.reject(reason));
  }

  protected cancel(): void {
    this.askReason({
      titleKey: 'invoices.dialogs.cancel.title',
      hintKey: 'invoices.dialogs.cancel.hint',
      labelKey: 'invoices.dialogs.reason',
      confirmKey: 'invoices.actions.cancel',
    }).subscribe((reason) => this.store.cancel(reason));
  }

  protected acknowledgeNotDuplicate(): void {
    this.askReason({
      titleKey: 'invoices.dialogs.notDuplicate.title',
      hintKey: 'invoices.dialogs.notDuplicate.hint',
      labelKey: 'invoices.dialogs.reason',
      confirmKey: 'invoices.actions.notDuplicate',
    }).subscribe((reason) => this.store.acknowledgeNotDuplicate(reason));
  }

  protected markPaid(): void {
    this.dialog
      .open<PaymentDialogComponent, void, PaymentDialogResult | undefined>(PaymentDialogComponent, {
        width: '440px',
        maxWidth: '92vw',
      })
      .afterClosed()
      .pipe(filter((result): result is PaymentDialogResult => !!result))
      .subscribe((result) => this.store.markPaid(result.paidOn, result.paymentReference));
  }

  protected unmappedMessage(error: FieldErrorLike): string {
    return this.errorMessages.fieldMessage(error.code, error.params ?? {});
  }

  private askReason(data: ReasonDialogData) {
    return this.dialog
      .open<ReasonDialogComponent, ReasonDialogData, string | undefined>(ReasonDialogComponent, {
        data,
        width: '480px',
        maxWidth: '92vw',
      })
      .afterClosed()
      .pipe(filter((reason): reason is string => !!reason));
  }
}

function toFormValue(fields: InvoiceFieldsDto | null) {
  return {
    supplierName: fields?.supplierName ?? '',
    invoiceNumber: fields?.invoiceNumber ?? '',
    invoiceDate: fields?.invoiceDate ?? '',
    dueDate: fields?.dueDate ?? '',
    currency: fields?.currency ?? '',
    subtotal: fields?.subtotal ?? null,
    taxAmount: fields?.taxAmount ?? null,
    totalAmount: fields?.totalAmount ?? null,
    purchaseOrderReference: fields?.purchaseOrderReference ?? '',
  };
}

/** The API replaces all fields at once; a blank value clears the field. */
function toInput(value: FieldsFormValue): InvoiceFieldsInput {
  const text = (v: string) => (v.trim() === '' ? null : v.trim());
  return {
    supplierName: text(value.supplierName),
    invoiceNumber: text(value.invoiceNumber),
    invoiceDate: value.invoiceDate || null,
    dueDate: value.dueDate || null,
    currency: text(value.currency)?.toUpperCase() ?? null,
    subtotal: value.subtotal,
    taxAmount: value.taxAmount,
    totalAmount: value.totalAmount,
    purchaseOrderReference: text(value.purchaseOrderReference),
  };
}
