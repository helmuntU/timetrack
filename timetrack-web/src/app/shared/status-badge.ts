import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TimeEntryStatus } from '../core/api/api.models';

const LABELS: Record<TimeEntryStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Awaiting approval',
  Approved: 'Approved',
  Rejected: 'Needs changes',
};

/** Presentational component: receives a status, renders it. No services, no state. */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [class]="'status status--' + status().toLowerCase()">{{ label() }}</span>`,
})
export class StatusBadge {
  readonly status = input.required<TimeEntryStatus>();

  protected readonly label = computed(() => LABELS[this.status()]);
}
