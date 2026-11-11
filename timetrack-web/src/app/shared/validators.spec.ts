import { FormControl } from '@angular/forms';
import { addDays, startOfWeek, todayIso } from './dates';
import { notInFutureValidator, quarterHourValidator } from './validators';

describe('quarterHourValidator', () => {
  const validate = (value: unknown) => quarterHourValidator()(new FormControl(value));

  it.each([0.25, 1, 2.5, 7.75, 8])('accepts %s', (value) => {
    expect(validate(value)).toBeNull();
  });

  it.each([0.1, 1.1, 2.3, 7.8])('rejects %s', (value) => {
    expect(validate(value)).toEqual({ quarterHour: true });
  });

  it('leaves empty values to the required validator', () => {
    expect(validate('')).toBeNull();
    expect(validate(null)).toBeNull();
  });
});

describe('notInFutureValidator', () => {
  const validate = (value: string) => notInFutureValidator()(new FormControl(value));

  it('accepts today and past dates', () => {
    expect(validate(todayIso())).toBeNull();
    expect(validate(addDays(todayIso(), -30))).toBeNull();
  });

  it('rejects tomorrow', () => {
    expect(validate(addDays(todayIso(), 1))).toEqual({ futureDate: true });
  });
});

describe('dates', () => {
  it('finds the Monday of the week, including from a Sunday', () => {
    expect(startOfWeek('2026-11-09')).toBe('2026-11-09'); // Monday
    expect(startOfWeek('2026-11-12')).toBe('2026-11-09'); // Thursday
    expect(startOfWeek('2026-11-15')).toBe('2026-11-09'); // Sunday
  });

  it('adds days across month boundaries', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });
});
