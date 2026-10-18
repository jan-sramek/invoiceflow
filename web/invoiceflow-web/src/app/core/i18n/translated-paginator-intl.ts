import { Injectable, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';

/** Paginator texts from the translation files, refreshed when the language changes. */
@Injectable()
export class TranslatedPaginatorIntl extends MatPaginatorIntl {
  private readonly transloco = inject(TranslocoService);

  constructor() {
    super();
    this.transloco.langChanges$.subscribe(() => this.refresh());
    this.refresh();
  }

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return this.transloco.translate('app.paginator.range', { start: 0, end: 0, total: 0 });
    }
    const start = page * pageSize;
    const end = Math.min(start + pageSize, length);
    return this.transloco.translate('app.paginator.range', { start: start + 1, end, total: length });
  };

  private refresh(): void {
    const t = (key: string) => this.transloco.translate(`app.paginator.${key}`);
    this.itemsPerPageLabel = t('itemsPerPage');
    this.nextPageLabel = t('next');
    this.previousPageLabel = t('previous');
    this.firstPageLabel = t('first');
    this.lastPageLabel = t('last');
    this.changes.next();
  }
}
