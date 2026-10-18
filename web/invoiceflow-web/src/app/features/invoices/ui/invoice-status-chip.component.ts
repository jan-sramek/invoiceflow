import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { DuplicateStatus, InvoiceStatus } from '@shared/api';

/** Status as a labelled pill. The dot repeats the colour, so the meaning never rests on colour alone. */
@Component({
  selector: 'if-invoice-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `<span class="chip" [attr.data-status]="status()"
    ><span class="dot" aria-hidden="true"></span>{{ 'invoices.status.' + status() | transloco }}</span
  >`,
  styleUrl: './chips.scss',
})
export class InvoiceStatusChipComponent {
  readonly status = input.required<InvoiceStatus>();
}

/** Flags that need attention next to the status: overdue and duplicate suspicion. */
@Component({
  selector: 'if-invoice-flags',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    @if (overdue()) {
      <span class="chip" data-flag="overdue"><span class="dot" aria-hidden="true"></span>{{ 'invoices.flags.overdue' | transloco }}</span>
    }
    @if (duplicate() === 'Suspected') {
      <span class="chip" data-flag="duplicate"><span class="dot" aria-hidden="true"></span>{{ 'invoices.duplicate.Suspected' | transloco }}</span>
    }
  `,
  styleUrl: './chips.scss',
  host: { class: 'flags' },
})
export class InvoiceFlagsComponent {
  readonly overdue = input(false);
  readonly duplicate = input<DuplicateStatus | null>(null);
}
