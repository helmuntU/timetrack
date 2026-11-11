import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, finalize, switchMap } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { SaveTimeEntryRequest, TIME_RULES, TimeEntry } from '../../core/api/api.models';
import {
  applyServerErrors,
  problemMessage,
  toProblemDetails,
} from '../../core/api/problem-details';
import { TimeTrackApi } from '../../core/api/time-track.api';
import { ToastService } from '../../core/notifications/toast.service';
import { isFuture, todayIso } from '../../shared/dates';
import { HoursPipe } from '../../shared/hours.pipe';
import { StatusBadge } from '../../shared/status-badge';
import { notInFutureValidator, quarterHourValidator } from '../../shared/validators';
import { ProjectPicker } from './project-picker';

type SaveMode = 'save' | 'saveAndSubmit';

/** One page for "log hours" and "edit entry": the route's :id decides the mode. */
@Component({
  selector: 'app-entry-form-page',
  imports: [ReactiveFormsModule, RouterLink, ProjectPicker, HoursPipe, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './entry-form.page.html',
  styleUrl: './entry-form.page.scss',
})
export class EntryFormPage {
  private readonly api = inject(TimeTrackApi);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly baseUrl = inject(API_BASE_URL);

  /** Route param (/entries/:id/edit), bound by withComponentInputBinding. */
  readonly id = input<string>();
  /** Optional query param (?date=2026-11-09) to prefill the day. */
  readonly date = input<string>();

  protected readonly rules = TIME_RULES;
  protected readonly quickHours = [0.5, 1, 2, 4, 8];
  protected readonly today = todayIso();
  protected readonly isEdit = computed(() => !!this.id());
  protected readonly saving = signal<SaveMode | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly conflict = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    projectId: ['', Validators.required],
    workDate: [this.today, [Validators.required, notInFutureValidator()]],
    hours: [
      1,
      [
        Validators.required,
        Validators.min(TIME_RULES.hoursIncrement),
        Validators.max(TIME_RULES.maxHoursPerEntry),
        quarterHourValidator(),
      ],
    ],
    description: ['', [Validators.required, Validators.maxLength(TIME_RULES.maxDescriptionLength)]],
  });

  /** The entry being edited. Returns undefined (no request) when creating. */
  protected readonly entry = httpResource<TimeEntry>(() => {
    const id = this.id();
    return id ? `${this.baseUrl}/time-entries/${id}` : undefined;
  });

  protected readonly canEdit = computed(() => {
    const entry = this.entry.value();
    return !entry || entry.status === 'Draft' || entry.status === 'Rejected';
  });

  // Form values as signals, so the daily total below reacts while the user types.
  private readonly workDate = toSignal(this.form.controls.workDate.valueChanges, {
    initialValue: this.form.controls.workDate.value,
  });
  private readonly hours = toSignal(this.form.controls.hours.valueChanges, {
    initialValue: this.form.controls.hours.value,
  });
  private readonly descriptionLength = toSignal(this.form.controls.description.valueChanges, {
    initialValue: '',
  });

  protected readonly charactersLeft = computed(
    () => TIME_RULES.maxDescriptionLength - this.descriptionLength().length,
  );

  /** Everything already logged that day, to warn before the API rejects the 12-hour limit. */
  protected readonly sameDay = httpResource<TimeEntry[]>(
    () => {
      const day = this.workDate();
      return day && !isFuture(day)
        ? { url: `${this.baseUrl}/time-entries`, params: { from: day, to: day } }
        : undefined;
    },
    { defaultValue: [] },
  );

  protected readonly otherHoursThatDay = computed(() =>
    this.sameDay
      .value()
      .filter((entry) => entry.id !== this.id())
      .reduce((sum, entry) => sum + entry.hours, 0),
  );

  protected readonly dayTotal = computed(
    () => this.otherHoursThatDay() + (Number(this.hours()) || 0),
  );
  protected readonly overDailyLimit = computed(() => this.dayTotal() > TIME_RULES.maxHoursPerDay);

  protected readonly otherPercent = computed(() =>
    Math.min(100, (this.otherHoursThatDay() / TIME_RULES.maxHoursPerDay) * 100),
  );

  protected readonly thisEntryPercent = computed(() =>
    Math.max(
      0,
      Math.min(100, (this.dayTotal() / TIME_RULES.maxHoursPerDay) * 100) - this.otherPercent(),
    ),
  );

  constructor() {
    // Effects are for syncing signals with non-signal APIs, like a reactive form.
    effect(() => {
      const date = this.date();
      if (!this.id() && date && !isFuture(date)) {
        this.form.controls.workDate.setValue(date);
      }
    });

    effect(() => {
      const entry = this.entry.value();
      if (!entry) {
        return;
      }

      this.form.reset({
        projectId: entry.projectId,
        workDate: entry.workDate,
        hours: entry.hours,
        description: entry.description,
      });

      if (this.canEdit()) {
        this.form.enable();
      } else {
        this.form.disable();
      }
    });
  }

  protected setHours(hours: number): void {
    this.form.controls.hours.setValue(hours);
    this.form.controls.hours.markAsDirty();
  }

  protected stepHours(direction: 1 | -1): void {
    const current = Number(this.form.controls.hours.value) || 0;
    const next = Math.min(
      TIME_RULES.maxHoursPerEntry,
      Math.max(TIME_RULES.hoursIncrement, current + direction * TIME_RULES.hoursIncrement),
    );
    this.setHours(next);
  }

  protected showError(control: keyof typeof this.form.controls): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  protected save(mode: SaveMode): void {
    if (this.form.invalid || this.overDailyLimit()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(mode);
    this.errorMessage.set(null);
    this.conflict.set(false);

    const request: SaveTimeEntryRequest = {
      ...this.form.getRawValue(),
      hours: Number(this.form.controls.hours.value),
      concurrencyStamp: this.entry.value()?.concurrencyStamp,
    };

    const id = this.id();
    const saved$: Observable<TimeEntry> = id
      ? this.api.updateTimeEntry(id, request)
      : this.api.createTimeEntry(request);

    // "Save and send": chain the submit after the save with switchMap.
    const result$ =
      mode === 'saveAndSubmit'
        ? saved$.pipe(switchMap((entry) => this.api.submitTimeEntry(entry.id)))
        : saved$;

    result$.pipe(finalize(() => this.saving.set(null))).subscribe({
      next: (entry) => {
        this.toasts.success(
          mode === 'saveAndSubmit' ? 'Hours saved and sent for approval.' : 'Hours saved as draft.',
        );
        void this.router.navigate(['/week'], { queryParams: { focus: entry.workDate } });
      },
      error: (error) => this.handleError(error),
    });
  }

  protected reload(): void {
    this.conflict.set(false);
    this.errorMessage.set(null);
    this.entry.reload();
  }

  private handleError(error: unknown): void {
    const problem = toProblemDetails(error);

    if (problem.status === 400) {
      const unmatched = applyServerErrors(this.form, problem);
      this.errorMessage.set(
        unmatched.length ? unmatched.join(' ') : 'Check the highlighted fields.',
      );
      return;
    }

    if (problem.status === 409) {
      this.conflict.set(true);
      return;
    }

    this.errorMessage.set(problemMessage(problem));
  }
}
