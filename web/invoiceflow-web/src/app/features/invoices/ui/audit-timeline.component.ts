import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LanguageService } from '@core/i18n';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { AuditChangeDto, InvoiceAuditEntryDto } from '@shared/api';
import { DateTimePipe } from '@shared/ui';

/** The audit trail, oldest first, with who did what, what changed and why. */
@Component({
  selector: 'if-audit-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatProgressSpinnerModule, TranslocoPipe, DateTimePipe],
  templateUrl: './audit-timeline.component.html',
  styleUrl: './audit-timeline.component.scss',
})
export class AuditTimelineComponent {
  readonly entries = input.required<readonly InvoiceAuditEntryDto[]>();
  readonly loading = input(false);
  readonly hasMore = input(false);
  readonly failed = input(false);
  readonly loadMore = output<void>();
  readonly retry = output<void>();

  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);

  protected actionLabel(code: string): string {
    return this.translateOr(`invoices.audit.action.${code}`, code);
  }

  protected fieldLabel(field: string): string {
    return this.translateOr(`invoices.fields.${field}`, field);
  }

  protected valueLabel(change: AuditChangeDto, value: string | null): string {
    if (value === null || value === '') {
      return '—';
    }
    return change.field === 'status' ? this.translateOr(`invoices.status.${value}`, value) : value;
  }

  private translateOr(key: string, fallback: string): string {
    // Read so the template re-renders when the language changes.
    this.language.lang();
    const text = this.transloco.translate(key);
    return text && text !== key ? text : fallback;
  }
}
