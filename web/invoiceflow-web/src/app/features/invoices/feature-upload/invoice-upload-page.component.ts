import { ChangeDetectionStrategy, Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ApiErrorMessages } from '@core/error-handling';
import { LanguageService } from '@core/i18n';
import { TranslocoPipe } from '@jsverse/transloco';
import { BytesPipe } from '@shared/ui';
import {
  InvoiceUploadStore,
  MAX_PARALLEL_UPLOADS,
  MAX_UPLOAD_BYTES,
  type UploadItem,
} from '../data-access';
import { InvoiceFlagsComponent, InvoiceStatusChipComponent } from '../ui';

@Component({
  selector: 'if-invoice-upload-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [InvoiceUploadStore],
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    TranslocoPipe,
    BytesPipe,
    InvoiceFlagsComponent,
    InvoiceStatusChipComponent,
  ],
  templateUrl: './invoice-upload-page.component.html',
  styleUrl: './invoice-upload-page.component.scss',
})
export class InvoiceUploadPageComponent {
  protected readonly store = inject(InvoiceUploadStore);
  protected readonly language = inject(LanguageService);
  private readonly errorMessages = inject(ApiErrorMessages);

  protected readonly maxParallel = MAX_PARALLEL_UPLOADS;
  protected readonly maxBytes = MAX_UPLOAD_BYTES;
  protected readonly dragging = signal(false);

  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  protected choose(): void {
    this.fileInput().nativeElement.click();
  }

  protected onFilesChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.store.addFiles(Array.from(input.files ?? []));
    // Lets the same file be chosen again after it was removed.
    input.value = '';
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    this.store.addFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  protected errorText(item: UploadItem): string {
    return item.error ? this.errorMessages.message(item.error) : '';
  }
}
