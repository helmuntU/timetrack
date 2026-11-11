import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, debounceTime, distinctUntilChanged, finalize, map } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { TimeEntry, TimeEntryFilter, TimeEntryStatus } from '../../core/api/api.models';
import { problemMessage, toProblemDetails } from '../../core/api/problem-details';
import { TimeTrackApi, toParams } from '../../core/api/time-track.api';
import { ToastService } from '../../core/notifications/toast.service';
import { addDays, parseIsoDate, todayIso } from '../../shared/dates';
import { HoursPipe } from '../../shared/hours.pipe';
import { StatusBadge } from '../../shared/status-badge';

interface EntryGroup {
  date: string;
  label: string;
  total: number;
  entries: TimeEntry[];
}

@Component({
  selector: 'app-entries-page',
  imports: [ReactiveFormsModule, RouterLink, HoursPipe, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './entries.page.html',
  styleUrl: './entries.page.scss',
})
export class EntriesPage {
  private readonly api = inject(TimeTrackApi);
  private readonly toasts = inject(ToastService);
  private readonly baseUrl = inject(API_BASE_URL);

  protected readonly statuses: TimeEntryStatus[] = ['Draft', 'Submitted', 'Approved', 'Rejected'];
  protected readonly busyId = signal<string | null>(null);
  protected readonly confirmDeleteId = signal<string | null>(null);

  protected readonly filters = inject(NonNullableFormBuilder).group({
    from: addDays(todayIso(), -27),
    to: todayIso(),
    status: '' as TimeEntryStatus | '',
  });

  /**
   * RxJS for the event stream (debounce while the user changes dates), then a signal for
   * the template and the resource. distinctUntilChanged avoids identical requests.
   */
  private readonly filter = toSignal(
    this.filters.valueChanges.pipe(
      debounceTime(300),
      map(() => this.toFilter()),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
    ),
    { initialValue: this.toFilter() },
  );

  protected readonly entries = httpResource<TimeEntry[]>(
    () => {
      const filter = this.filter();

      // Returning undefined skips the request: an invalid range never reaches the API.
      if (filter.from && filter.to && filter.from > filter.to) {
        return undefined;
      }

      return { url: `${this.baseUrl}/time-entries`, params: toParams(filter) };
    },
    { defaultValue: [] },
  );

  protected readonly invalidRange = computed(() => {
    const { from, to } = this.filter();
    return !!from && !!to && from > to;
  });

  protected readonly groups = computed<EntryGroup[]>(() => {
    const byDate = new Map<string, TimeEntry[]>();

    for (const entry of this.entries.value()) {
      byDate.set(entry.workDate, [...(byDate.get(entry.workDate) ?? []), entry]);
    }

    return [...byDate.entries()].map(([date, entries]) => ({
      date,
      label: parseIsoDate(date).toLocaleDateString('en-US', {
        weekday: 'long',
        month: 'short',
        day: 'numeric',
      }),
      total: entries.reduce((sum, entry) => sum + entry.hours, 0),
      entries,
    }));
  });

  protected readonly totalHours = computed(() =>
    this.entries.value().reduce((sum, entry) => sum + entry.hours, 0),
  );

  protected clearFilters(): void {
    this.filters.reset();
  }

  protected submit(entry: TimeEntry): void {
    this.runAction(entry.id, this.api.submitTimeEntry(entry.id), 'Entry sent for approval.');
  }

  protected delete(entry: TimeEntry): void {
    this.confirmDeleteId.set(null);
    this.runAction(entry.id, this.api.deleteTimeEntry(entry.id), 'Entry deleted.');
  }

  private runAction(id: string, request: Observable<unknown>, success: string): void {
    this.busyId.set(id);

    request.pipe(finalize(() => this.busyId.set(null))).subscribe({
      next: () => {
        this.toasts.success(success);
        this.entries.reload();
      },
      error: (error) => this.toasts.error(problemMessage(toProblemDetails(error))),
    });
  }

  private toFilter(): TimeEntryFilter {
    const { from, to, status } = this.filters.getRawValue();
    return { from: from || undefined, to: to || undefined, status: status || undefined };
  }
}
