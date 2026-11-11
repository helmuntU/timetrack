import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  ElementRef,
  forwardRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { Project } from '../../core/api/api.models';
import { TimeTrackApi } from '../../core/api/time-track.api';

let nextId = 0;

/**
 * Searchable project selector that works with formControlName like a native input.
 *
 * - ControlValueAccessor: the form only sees the project id; this component handles the UI.
 * - RxJS: debounceTime waits for the user to stop typing, distinctUntilChanged skips repeats,
 *   switchMap cancels the previous request so an old response never overwrites a newer one,
 *   and catchError lives inside switchMap so one failed request doesn't kill the stream.
 * - Accessible combobox: arrow keys, Enter, Escape, and the ARIA roles screen readers expect.
 */
@Component({
  selector: 'app-project-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => ProjectPicker), multi: true },
  ],
  templateUrl: './project-picker.html',
  styleUrl: './project-picker.scss',
})
export class ProjectPicker implements ControlValueAccessor {
  private readonly api = inject(TimeTrackApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchInput = viewChild.required<ElementRef<HTMLInputElement>>('searchInput');

  readonly inputId = input(`project-picker-${nextId++}`);
  readonly invalid = input(false);

  protected readonly listboxId = computed(() => `${this.inputId()}-listbox`);
  protected readonly selected = signal<Project | null>(null);
  protected readonly results = signal<Project[]>([]);
  protected readonly open = signal(false);
  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly activeIndex = signal(-1);
  protected readonly disabled = signal(false);
  protected readonly query = signal('');

  private readonly searchTerms = new Subject<string>();
  private onChange: (projectId: string) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    this.searchTerms
      .pipe(
        map((term) => term.trim()),
        debounceTime(250),
        distinctUntilChanged(),
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap((term) =>
          this.api.searchProjects(term).pipe(
            map((page) => page.items),
            catchError(() => {
              this.failed.set(true);
              return of<Project[]>([]);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((projects) => {
        this.loading.set(false);
        this.results.set(projects);
        this.activeIndex.set(projects.length > 0 ? 0 : -1);
      });
  }

  // ---- ControlValueAccessor: how Angular forms talk to this component ----

  writeValue(projectId: string | null): void {
    if (!projectId) {
      this.selected.set(null);
      this.query.set('');
      return;
    }

    if (this.selected()?.id === projectId) {
      return;
    }

    // Editing an existing entry: the form only knows the id, so load the label to display.
    this.api
      .getProject(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => this.select(project, false),
        error: () => this.selected.set(null),
      });
  }

  registerOnChange(fn: (projectId: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  // ---- UI events ----

  protected onInput(value: string): void {
    this.query.set(value);
    this.open.set(true);

    // Typing again clears the previous choice: the value must always be a real project.
    if (this.selected()) {
      this.selected.set(null);
      this.onChange('');
    }

    this.searchTerms.next(value);
  }

  protected onFocus(): void {
    if (!this.selected()) {
      this.open.set(true);
      this.searchTerms.next(this.query());
    }
  }

  protected onBlur(): void {
    this.onTouched();
    // Delay so a click on an option registers before the list closes.
    setTimeout(() => this.open.set(false), 150);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.results().length;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.open.set(true);
        this.activeIndex.set(count ? (this.activeIndex() + 1) % count : -1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.activeIndex.set(count ? (this.activeIndex() - 1 + count) % count : -1);
        break;
      case 'Enter':
        if (this.open() && this.activeIndex() >= 0) {
          event.preventDefault();
          this.select(this.results()[this.activeIndex()]);
        }
        break;
      case 'Escape':
        this.open.set(false);
        break;
    }
  }

  protected select(project: Project, notify = true): void {
    this.selected.set(project);
    this.query.set(`${project.code} · ${project.name}`);
    this.open.set(false);

    if (notify) {
      this.onChange(project.id);
      this.onTouched();
    }
  }

  protected clear(): void {
    this.selected.set(null);
    this.query.set('');
    this.onChange('');
    this.searchInput().nativeElement.focus();
  }

  protected optionId(index: number): string {
    return `${this.listboxId()}-option-${index}`;
  }
}
