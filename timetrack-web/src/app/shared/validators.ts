import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { TIME_RULES } from '../core/api/api.models';
import { isFuture } from './dates';

/** Hours must be a multiple of 15 minutes (0.25), the same rule as the API domain. */
export function quarterHourValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;

    if (value === null || value === undefined || value === '') {
      return null; // "required" is a separate validator: each validator checks one thing.
    }

    // Work in hundredths to avoid floating point surprises (0.1 + 0.2 !== 0.3).
    const hundredths = Math.round(Number(value) * 100);
    return hundredths % (TIME_RULES.hoursIncrement * 100) === 0 ? null : { quarterHour: true };
  };
}

/** A work date can't be in the future. */
export function notInFutureValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null =>
    control.value && isFuture(control.value) ? { futureDate: true } : null;
}
