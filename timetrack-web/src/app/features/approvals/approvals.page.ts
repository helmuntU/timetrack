import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { TIME_RULES, TimeEntry } from '../../core/api/api.models';
import { problemMessage, toProblemDetails } from '../../core/api/problem-details';
import { TimeTrackApi } from '../../core/api/time-track.api';
import { ToastService } from '../../core/notifications/toast.service';
import { parseIsoDate } from '../../shared/dates';
import { HoursPipe } from '../../shared/hours.pipe';

interface PersonQueue {
  userId: string;
  fullName: string;
  totalHours: number;
  entries: TimeEntry[];
}

@Component({
  selector: 'app-approvals-page',
  imports: [ReactiveFormsModule, HoursPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './approvals.page.html',
  styleUrl: './approvals.page.scss',
})
export class ApprovalsPage {
  private readonly api = inject(TimeTrackApi);
  private readonly toasts = inject(ToastService);
  private readonly baseUrl = inject(API_BASE_URL);

  protected readonly maxReasonLength = TIME_RULES.maxRejectionReasonLength;
  protected readonly pending = httpResource<TimeEntry[]>(
    () => `${this.baseUrl}/approvals/pending`,
    {
      defaultValue: [],
    },
  );

  protected readonly busyIds = signal<ReadonlySet<string>>(new Set());
  protected readonly rejectingId = signal<string | null>(null);
  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(TIME_RULES.maxRejectionReasonLength)],
  });

  /** The queue grouped by person: a manager reviews one consultant's week at a time. */
  protected readonly queues = computed<PersonQueue[]>(() => {
    const byUser = new Map<string, PersonQueue>();

    for (const entry of this.pending.value()) {
      const queue = byUser.get(entry.userId) ?? {
        userId: entry.userId,
        fullName: entry.userFullName,
        totalHours: 0,
        entries: [],
      };
      queue.entries.push(entry);
      queue.totalHours += entry.hours;
      byUser.set(entry.userId, queue);
    }

    return [...byUser.values()].sort((a, b) => a.fullName.localeCompare(b.fullName));
  });

  protected readonly totalHours = computed(() =>
    this.pending.value().reduce((sum, e) => sum + e.hours, 0),
  );

  protected formatDate(date: string): string {
    return parseIsoDate(date).toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
  }

  protected isBusy(id: string): boolean {
    return this.busyIds().has(id);
  }

  protected approve(entry: TimeEntry): void {
    this.run(
      entry,
      this.api.approve(entry.id),
      `Approved ${entry.hours} h for ${entry.userFullName}.`,
    );
  }

  /** Approves a whole queue in parallel (forkJoin) and confirms once, not once per entry. */
  protected approveAll(queue: PersonQueue): void {
    const ids = queue.entries.map((entry) => entry.id);
    ids.forEach((id) => this.setBusy(id, true));

    forkJoin(ids.map((id) => this.api.approve(id)))
      .pipe(finalize(() => ids.forEach((id) => this.setBusy(id, false))))
      .subscribe({
        next: () => {
          this.toasts.success(`Approved ${queue.entries.length} entries for ${queue.fullName}.`);
          this.pending.update((items) => items.filter((item) => !ids.includes(item.id)));
        },
        error: (error) => {
          this.toasts.error(problemMessage(toProblemDetails(error)));
          this.pending.reload();
        },
      });
  }

  protected startReject(entry: TimeEntry): void {
    this.reason.reset();
    this.rejectingId.set(entry.id);
  }

  protected confirmReject(entry: TimeEntry): void {
    if (this.reason.invalid) {
      this.reason.markAsTouched();
      return;
    }

    this.run(
      entry,
      this.api.reject(entry.id, this.reason.value.trim()),
      `Sent back to ${entry.userFullName} with your note.`,
    );
    this.rejectingId.set(null);
  }

  private run(
    entry: TimeEntry,
    request: ReturnType<TimeTrackApi['approve']>,
    success: string,
  ): void {
    this.setBusy(entry.id, true);

    request.pipe(finalize(() => this.setBusy(entry.id, false))).subscribe({
      next: () => {
        this.toasts.success(success);
        // Optimistic local removal: the row disappears at once, without refetching the queue.
        this.pending.update((items) => items.filter((item) => item.id !== entry.id));
      },
      error: (error) => {
        this.toasts.error(problemMessage(toProblemDetails(error)));
        this.pending.reload();
      },
    });
  }

  private setBusy(id: string, busy: boolean): void {
    this.busyIds.update((current) => {
      const next = new Set(current);
      if (busy) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }
}
