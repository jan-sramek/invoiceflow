import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiErrorMessages } from '@core/error-handling';
import { LanguageService } from '@core/i18n';
import { TranslocoPipe } from '@jsverse/transloco';
import { DUPLICATE_STATUSES, INVOICE_STATUSES, type DuplicateStatus, type InvoiceStatus } from '@shared/api';
import { DatePipe, MoneyPipe } from '@shared/ui';
import { debounceTime } from 'rxjs';
import { InvoiceListStore } from '../data-access';
import { InvoiceFlagsComponent, InvoiceStatusChipComponent } from '../ui';
import {
  DEFAULT_LIST_QUERY,
  InvoiceListQuery,
  PAGE_SIZE_OPTIONS,
  activeFilterCount,
  fromRouteParams,
  sortParts,
  toRouteParams,
  toSort,
} from '../util';

@Component({
  selector: 'if-invoice-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [InvoiceListStore],
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    TranslocoPipe,
    DatePipe,
    MoneyPipe,
    InvoiceFlagsComponent,
    InvoiceStatusChipComponent,
  ],
  templateUrl: './invoice-list-page.component.html',
  styleUrl: './invoice-list-page.component.scss',
})
export class InvoiceListPageComponent {
  protected readonly store = inject(InvoiceListStore);
  protected readonly language = inject(LanguageService);
  protected readonly errorMessages = inject(ApiErrorMessages);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly statuses = INVOICE_STATUSES;
  protected readonly duplicateStatuses = DUPLICATE_STATUSES;
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly columns = ['supplier', 'status', 'invoiceDate', 'dueDate', 'total', 'uploadedAt'];

  protected readonly form = inject(NonNullableFormBuilder).group({
    search: '',
    status: [[] as InvoiceStatus[]],
    overdue: false,
    duplicateStatus: '' as DuplicateStatus | '',
    invoiceDateFrom: '',
    invoiceDateTo: '',
    dueDateFrom: '',
    dueDateTo: '',
    totalAmountFrom: null as number | null,
    totalAmountTo: null as number | null,
    currency: '',
  });

  protected readonly showMore = signal(false);
  protected readonly sort = computed(() => sortParts(this.store.query().sort));
  protected readonly filterCount = computed(() => activeFilterCount(this.store.query()));
  protected readonly hasCriteria = computed(
    () => this.filterCount() > 0 || this.store.query().search.trim() !== '',
  );
  protected readonly errorText = computed(() => {
    const error = this.store.error();
    return error ? this.errorMessages.message(error) : '';
  });

  constructor() {
    // The URL is the source of truth, so reload, back button and shared links keep the same list.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const query = fromRouteParams(params);
      this.store.setQuery(query);
      if (!sameCriteria(this.queryFromForm(), query)) {
        this.form.patchValue(formValue(query), { emitEvent: false });
      }
      if (query.dueDateFrom || query.invoiceDateFrom || query.totalAmountFrom !== null) {
        this.showMore.set(true);
      }
    });

    this.form.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.navigate({ ...this.queryFromForm(), page: 1 }, true));
  }

  protected onSort(sort: Sort): void {
    this.navigate({ ...this.store.query(), sort: toSort(sort.active, sort.direction), page: 1 });
  }

  protected onPage(event: PageEvent): void {
    this.navigate({ ...this.store.query(), page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected clear(): void {
    this.form.reset(formValue(DEFAULT_LIST_QUERY), { emitEvent: false });
    this.navigate({ ...DEFAULT_LIST_QUERY, sort: this.store.query().sort, pageSize: this.store.query().pageSize });
  }

  protected open(id: string): void {
    void this.router.navigate(['/invoices', id]);
  }

  /** Filters and search from the form, with the current sort and page size. */
  private queryFromForm(): InvoiceListQuery {
    const value = this.form.getRawValue();
    const current = this.store.query();
    return {
      ...current,
      ...value,
      duplicateStatus: value.duplicateStatus === '' ? null : value.duplicateStatus,
      totalAmountFrom: numberOrNull(value.totalAmountFrom),
      totalAmountTo: numberOrNull(value.totalAmountTo),
    };
  }

  private navigate(query: InvoiceListQuery, replaceUrl = false): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toRouteParams(query),
      replaceUrl,
    });
  }
}

function formValue(query: InvoiceListQuery) {
  return {
    search: query.search,
    status: [...query.status],
    overdue: query.overdue,
    duplicateStatus: query.duplicateStatus ?? ('' as const),
    invoiceDateFrom: query.invoiceDateFrom,
    invoiceDateTo: query.invoiceDateTo,
    dueDateFrom: query.dueDateFrom,
    dueDateTo: query.dueDateTo,
    totalAmountFrom: query.totalAmountFrom,
    totalAmountTo: query.totalAmountTo,
    currency: query.currency,
  };
}

/** Number inputs hand over `null` or a number, but an emptied field can also arrive as an empty string. */
function numberOrNull(value: number | string | null): number | null {
  if (value === null || value === '') {
    return null;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

/** Whether two queries filter and search alike, ignoring how the URL spells them (spacing, case). */
function sameCriteria(a: InvoiceListQuery, b: InvoiceListQuery): boolean {
  const strip = (q: InvoiceListQuery) => JSON.stringify({ ...toRouteParams(q), sort: null, page: null, pageSize: null });
  return strip(a) === strip(b);
}
