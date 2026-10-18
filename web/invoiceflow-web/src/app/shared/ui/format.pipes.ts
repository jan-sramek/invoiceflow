import { Pipe, PipeTransform } from '@angular/core';
import { formatBytes, formatDate, formatDateTime, formatMoney } from '@shared/util';

/** `{{ amount | ifMoney: currency : locale }}`; the locale comes from the language service so it updates on switch. */
@Pipe({ name: 'ifMoney' })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, currency: string | null | undefined, locale: string): string {
    return formatMoney(value, currency, locale);
  }
}

/** Date-only values such as `2026-03-01`, shown without a time-zone shift. */
@Pipe({ name: 'ifDate' })
export class DatePipe implements PipeTransform {
  transform(value: string | null | undefined, locale: string): string {
    return formatDate(value, locale);
  }
}

@Pipe({ name: 'ifDateTime' })
export class DateTimePipe implements PipeTransform {
  transform(value: string | null | undefined, locale: string): string {
    return formatDateTime(value, locale);
  }
}

@Pipe({ name: 'ifBytes' })
export class BytesPipe implements PipeTransform {
  transform(value: number, locale: string): string {
    return formatBytes(value, locale);
  }
}
