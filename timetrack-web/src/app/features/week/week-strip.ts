import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { IsoDate, TIME_RULES, TimeEntryStatus } from '../../core/api/api.models';
import { HoursPipe } from '../../shared/hours.pipe';

export interface DaySegment {
  status: TimeEntryStatus;
  hours: number;
}

export interface DayColumn {
  date: IsoDate;
  weekday: string;
  dayOfMonth: number;
  total: number;
  segments: DaySegment[];
  isToday: boolean;
  isFuture: boolean;
}

/**
 * The week at a glance: one column per day, stacked by status, drawn against the
 * 8-hour target and the 12-hour daily limit. Presentational: data in, selection out.
 */
@Component({
  selector: 'app-week-strip',
  imports: [HoursPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './week-strip.html',
  styleUrl: './week-strip.scss',
})
export class WeekStrip {
  readonly days = input.required<DayColumn[]>();
  readonly selected = input<IsoDate | null>(null);
  readonly daySelected = output<IsoDate>();

  protected readonly scale = TIME_RULES.maxHoursPerDay;
  protected readonly targetPercent =
    (TIME_RULES.targetHoursPerDay / TIME_RULES.maxHoursPerDay) * 100;

  protected readonly description = computed(() =>
    this.days()
      .map((day) => `${day.weekday} ${day.dayOfMonth}: ${day.total} hours`)
      .join(', '),
  );

  protected percent(hours: number): number {
    return Math.min(100, (hours / this.scale) * 100);
  }
}
