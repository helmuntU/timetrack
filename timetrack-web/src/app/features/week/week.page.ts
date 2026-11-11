import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { IsoDate, TimeEntry, TimeEntryStatus, WeeklySummary } from '../../core/api/api.models';
import { problemMessage, toProblemDetails } from '../../core/api/problem-details';
import { TimeTrackApi } from '../../core/api/time-track.api';
import { ToastService } from '../../core/notifications/toast.service';
import { addDays, parseIsoDate, startOfWeek, todayIso } from '../../shared/dates';
import { HoursPipe } from '../../shared/hours.pipe';
import { StatusBadge } from '../../shared/status-badge';
import { ProjectBreakdown } from './project-breakdown';
import { DayColumn, WeekStrip } from './week-strip';

// Bottom-to-top order of the stacked bars: settled hours first, open work on top.
const STACK_ORDER: TimeEntryStatus[] = ['Approved', 'Submitted', 'Rejected', 'Draft'];

@Component({
  selector: 'app-week-page',
  imports: [RouterLink, HoursPipe, StatusBadge, WeekStrip, ProjectBreakdown],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './week.page.html',
  styleUrl: './week.page.scss',
})
export class WeekPage {
  private readonly api = inject(TimeTrackApi);
  private readonly toasts = inject(ToastService);
  private readonly baseUrl = inject(API_BASE_URL);

  /** ?focus=2026-11-06 opens the week of that day with the day selected (used after saving). */
  readonly focus = input<IsoDate>();

  protected readonly today = todayIso();
  protected readonly weekStart = signal<IsoDate>(startOfWeek(this.today));
  protected readonly weekEnd = computed(() => addDays(this.weekStart(), 6));
  protected readonly isCurrentWeek = computed(() => this.weekStart() === startOfWeek(this.today));
  protected readonly submitting = signal(false);
  private readonly preferredDay = signal<IsoDate | null>(null);

  /**
   * httpResource re-requests automatically whenever weekStart() changes, cancels the
   * previous request, and exposes value / isLoading / error as signals. No subscriptions.
   */
  protected readonly summary = httpResource<WeeklySummary>(() => ({
    url: `${this.baseUrl}/dashboard/weekly-summary`,
    params: { weekOf: this.weekStart() },
  }));

  protected readonly entries = httpResource<TimeEntry[]>(
    () => ({
      url: `${this.baseUrl}/time-entries`,
      params: { from: this.weekStart(), to: this.weekEnd() },
    }),
    { defaultValue: [] },
  );

  /**
   * The selected day resets whenever the week changes (today if it's in the week, else Monday),
   * but the user can still pick another day: that is exactly what linkedSignal is for.
   */
  protected readonly selectedDay = linkedSignal<IsoDate>(() => {
    const start = this.weekStart();
    const end = addDays(start, 6);
    const preferred = this.preferredDay();

    if (preferred && preferred >= start && preferred <= end) {
      return preferred;
    }

    return this.today >= start && this.today <= end ? this.today : start;
  });

  protected readonly days = computed<DayColumn[]>(() => {
    const entries = this.entries.value();

    return Array.from({ length: 7 }, (_, index) => {
      const date = addDays(this.weekStart(), index);
      const dayEntries = entries.filter((entry) => entry.workDate === date);
      const parsed = parseIsoDate(date);

      return {
        date,
        weekday: parsed.toLocaleDateString('en-US', { weekday: 'short' }),
        dayOfMonth: parsed.getDate(),
        total: sumHours(dayEntries),
        segments: STACK_ORDER.map((status) => ({
          status,
          hours: sumHours(dayEntries.filter((entry) => entry.status === status)),
        })).filter((segment) => segment.hours > 0),
        isToday: date === this.today,
        isFuture: date > this.today,
      };
    });
  });

  protected readonly selectedEntries = computed(() =>
    this.entries.value().filter((entry) => entry.workDate === this.selectedDay()),
  );

  protected readonly draftCount = computed(
    () => this.entries.value().filter((entry) => entry.status === 'Draft').length,
  );

  protected readonly weekLabel = computed(() => formatRange(this.weekStart(), this.weekEnd()));

  protected readonly selectedDayLabel = computed(() =>
    parseIsoDate(this.selectedDay()).toLocaleDateString('en-US', {
      weekday: 'long',
      month: 'long',
      day: 'numeric',
    }),
  );

  protected readonly legend = computed(() => {
    const summary = this.summary.value();
    return [
      { status: 'Approved', label: 'Approved', hours: summary?.approvedHours ?? 0 },
      { status: 'Submitted', label: 'Awaiting approval', hours: summary?.submittedHours ?? 0 },
      { status: 'Rejected', label: 'Needs changes', hours: summary?.rejectedHours ?? 0 },
      { status: 'Draft', label: 'Draft', hours: summary?.draftHours ?? 0 },
    ];
  });

  constructor() {
    effect(() => {
      const focus = this.focus();
      if (focus && /^\d{4}-\d{2}-\d{2}$/.test(focus)) {
        this.preferredDay.set(focus);
        this.weekStart.set(startOfWeek(focus));
      }
    });
  }

  protected moveWeek(weeks: number): void {
    this.weekStart.update((start) => addDays(start, weeks * 7));
  }

  protected goToCurrentWeek(): void {
    this.weekStart.set(startOfWeek(this.today));
  }

  /** Submits every draft of the week in parallel, then refreshes both resources once. */
  protected submitWeek(): void {
    const drafts = this.entries.value().filter((entry) => entry.status === 'Draft');

    if (drafts.length === 0) {
      return;
    }

    this.submitting.set(true);

    forkJoin(drafts.map((entry) => this.api.submitTimeEntry(entry.id))).subscribe({
      next: () => {
        this.toasts.success(
          `${drafts.length} ${drafts.length === 1 ? 'entry' : 'entries'} sent for approval.`,
        );
        this.refresh();
      },
      error: (error) => {
        this.toasts.error(problemMessage(toProblemDetails(error)));
        this.refresh();
      },
    });
  }

  private refresh(): void {
    this.submitting.set(false);
    this.entries.reload();
    this.summary.reload();
  }
}

function sumHours(entries: TimeEntry[]): number {
  return entries.reduce((total, entry) => total + entry.hours, 0);
}

function formatRange(start: IsoDate, end: IsoDate): string {
  const from = parseIsoDate(start);
  const to = parseIsoDate(end);
  const sameMonth = from.getMonth() === to.getMonth();
  const startText = from.toLocaleDateString('en-US', { month: 'long', day: 'numeric' });
  const endText = sameMonth
    ? `${to.getDate()}, ${to.getFullYear()}`
    : to.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' });

  return `${startText} – ${endText}`;
}
