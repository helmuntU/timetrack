import { IsoDate } from '../core/api/api.models';

/**
 * Calendar-date helpers. Work dates are plain calendar days ("2026-11-09"), so they are built
 * from local date parts: converting through toISOString() would shift the day near midnight.
 */

export function toIsoDate(date: Date): IsoDate {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function parseIsoDate(value: IsoDate): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function todayIso(): IsoDate {
  return toIsoDate(new Date());
}

export function addDays(value: IsoDate, days: number): IsoDate {
  const date = parseIsoDate(value);
  date.setDate(date.getDate() + days);
  return toIsoDate(date);
}

/** Monday of the week that contains the date (weeks run Monday to Sunday, like the API). */
export function startOfWeek(value: IsoDate): IsoDate {
  const date = parseIsoDate(value);
  const daysSinceMonday = (date.getDay() + 6) % 7;
  return addDays(value, -daysSinceMonday);
}

export function isFuture(value: IsoDate): boolean {
  return value > todayIso();
}
