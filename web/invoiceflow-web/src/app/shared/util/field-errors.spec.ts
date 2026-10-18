import { FormControl, FormGroup } from '@angular/forms';
import { API_ERRORS_KEY, applyFieldErrors, mapFieldErrors, pointerToControlPath } from './field-errors';
import { parseJsonPointer, toJsonPointer } from './json-pointer';

describe('parseJsonPointer', () => {
  it('returns no tokens for the whole document', () => {
    expect(parseJsonPointer('')).toEqual([]);
    expect(parseJsonPointer(null)).toEqual([]);
    expect(parseJsonPointer(undefined)).toEqual([]);
  });

  it('splits tokens', () => {
    expect(parseJsonPointer('/lines/0/amount')).toEqual(['lines', '0', 'amount']);
  });

  it('decodes ~1 and ~0 in the right order', () => {
    expect(parseJsonPointer('/a~1b')).toEqual(['a/b']);
    expect(parseJsonPointer('/m~0n')).toEqual(['m~n']);
    expect(parseJsonPointer('/~01')).toEqual(['~1']);
  });

  it('tolerates a missing leading slash', () => {
    expect(parseJsonPointer('supplierName')).toEqual(['supplierName']);
  });

  it('round-trips through toJsonPointer', () => {
    const tokens = ['a/b', 'm~n', '0'];
    expect(parseJsonPointer(toJsonPointer(tokens))).toEqual(tokens);
  });
});

describe('field errors', () => {
  const required = { pointer: '/supplierName', code: 'validation.required', params: { field: 'supplierName' } };

  it('maps a pointer to a control path', () => {
    expect(pointerToControlPath('/supplierName')).toBe('supplierName');
    expect(pointerToControlPath('/lines/1/amount')).toBe('lines.1.amount');
    expect(pointerToControlPath('')).toBeNull();
  });

  it('groups errors and keeps the rest as unmapped', () => {
    const unknown = { pointer: '/nope', code: 'validation.invalid_format' };
    const whole = { pointer: '', code: 'invoice.not_ready_for_submission' };
    const result = mapFieldErrors([required, unknown, whole], (path) => path === 'supplierName');

    expect(result.byPath.get('supplierName')).toEqual([required]);
    expect(result.unmapped).toEqual([unknown, whole]);
  });

  it('sets errors on the matching controls and touches them', () => {
    const form = new FormGroup({ supplierName: new FormControl(''), total: new FormControl('') });
    const unmapped = applyFieldErrors(form, [required, { pointer: '/other', code: 'x' }]);

    const control = form.controls.supplierName;
    expect(control.errors?.[API_ERRORS_KEY]).toEqual([required]);
    expect(control.touched).toBeTrue();
    expect(form.controls.total.errors).toBeNull();
    expect(unmapped.map((e) => e.pointer)).toEqual(['/other']);
  });

  it('keeps client validation errors next to server errors', () => {
    const control = new FormControl('', { nonNullable: true });
    control.setErrors({ required: true });
    applyFieldErrors(new FormGroup({ a: control }), [{ pointer: '/a', code: 'x' }]);

    expect(Object.keys(control.errors ?? {}).sort()).toEqual(['api', 'required']);
  });

  it('drops server errors when the user edits the value', () => {
    const control = new FormControl('');
    applyFieldErrors(new FormGroup({ a: control }), [{ pointer: '/a', code: 'x' }]);
    control.setValue('changed');

    expect(control.errors).toBeNull();
  });
});
