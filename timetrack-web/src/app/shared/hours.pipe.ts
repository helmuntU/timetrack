import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formats decimal hours the way people read them: 7.5 -> "7h 30m", 8 -> "8h", 0.25 -> "15m".
 * Pure pipe: Angular only re-runs it when the input value changes.
 */
@Pipe({ name: 'hours' })
export class HoursPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }

    const totalMinutes = Math.round(value * 60);
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;

    if (hours === 0 && minutes === 0) {
      return '0h';
    }

    if (minutes === 0) {
      return `${hours}h`;
    }

    return hours === 0 ? `${minutes}m` : `${hours}h ${minutes}m`;
  }
}
