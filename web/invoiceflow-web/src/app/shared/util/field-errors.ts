import { AbstractControl } from '@angular/forms';
import { parseJsonPointer } from './json-pointer';

/** The shape of one `errors[]` item of a problem response; structurally matches the API's. */
export interface FieldErrorLike {
  readonly pointer: string;
  readonly code: string;
  readonly params?: Readonly<Record<string, unknown>> | null;
}

/** Key under which server field errors are stored in `AbstractControl.errors`. */
export const API_ERRORS_KEY = 'api';

/** Dotted path usable with `AbstractControl.get`, or `null` when the pointer names no single field. */
export function pointerToControlPath(pointer: string): string | null {
  const tokens = parseJsonPointer(pointer);
  return tokens.length === 0 ? null : tokens.join('.');
}

export interface MappedFieldErrors {
  /** Errors grouped by control path. */
  readonly byPath: ReadonlyMap<string, readonly FieldErrorLike[]>;
  /** Errors whose pointer names no field, or a field the form does not have. */
  readonly unmapped: readonly FieldErrorLike[];
}

/**
 * Groups errors by the control their pointer names. `hasControl` decides whether the form has that
 * control, so nested paths into groups and arrays work with any form shape.
 */
export function mapFieldErrors(
  errors: readonly FieldErrorLike[],
  hasControl: (path: string) => boolean,
): MappedFieldErrors {
  const byPath = new Map<string, FieldErrorLike[]>();
  const unmapped: FieldErrorLike[] = [];

  for (const error of errors) {
    const path = pointerToControlPath(error.pointer);
    if (path !== null && hasControl(path)) {
      byPath.set(path, [...(byPath.get(path) ?? []), error]);
    } else {
      unmapped.push(error);
    }
  }
  return { byPath, unmapped };
}

/**
 * Puts server field errors on the matching controls and marks them touched so they show up.
 * Angular drops them again as soon as the user edits the control. Returns the errors that
 * found no control, for the caller to show at form level.
 */
export function applyFieldErrors(
  form: AbstractControl,
  errors: readonly FieldErrorLike[],
): readonly FieldErrorLike[] {
  const { byPath, unmapped } = mapFieldErrors(errors, (path) => form.get(path) !== null);

  for (const [path, list] of byPath) {
    const control = form.get(path);
    if (control) {
      control.setErrors({ ...control.errors, [API_ERRORS_KEY]: list });
      control.markAsTouched();
    }
  }
  return unmapped;
}
