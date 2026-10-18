import { Component, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { API_ERRORS_KEY, FieldErrorLike } from '@shared/util';

/**
 * The first error of a form control as text, for use inside `<mat-error>`. Understands the errors the API
 * returned for the field and the common built-in validators. Messages live under `errors.validation.*`.
 *
 * Deliberately not OnPush: control errors change without any signal, and the surrounding form field already
 * asks for a check when they do.
 */
@Component({
  selector: 'if-field-error',
  imports: [TranslocoPipe],
  template: `{{ key() | transloco: params() }}`,
})
export class FieldErrorComponent {
  readonly control = input.required<AbstractControl | null>();

  protected key(): string {
    const errors = this.control()?.errors;
    if (!errors) {
      return '';
    }
    const api = errors[API_ERRORS_KEY] as readonly FieldErrorLike[] | undefined;
    if (api?.length) {
      return `errors.${api[0].code}`;
    }
    if (errors['required']) return 'errors.validation.required';
    if (errors['maxlength']) return 'errors.validation.max_length';
    if (errors['min'] || errors['max']) return 'errors.validation.out_of_range';
    if (errors['pattern']) return 'errors.validation.invalid_format';
    return 'errors.validation.invalid_format';
  }

  protected params(): Record<string, unknown> {
    const errors = this.control()?.errors;
    const api = errors?.[API_ERRORS_KEY] as readonly FieldErrorLike[] | undefined;
    if (api?.length) {
      return { ...api[0].params };
    }
    return {
      max: errors?.['maxlength']?.requiredLength ?? errors?.['max']?.max,
      min: errors?.['min']?.min,
    };
  }
}
