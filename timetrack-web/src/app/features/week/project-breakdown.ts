import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProjectHours } from '../../core/api/api.models';
import { HoursPipe } from '../../shared/hours.pipe';

@Component({
  selector: 'app-project-breakdown',
  imports: [HoursPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (projects().length === 0) {
      <p class="muted mb-0">No hours on any project this week.</p>
    } @else {
      <ul class="breakdown">
        @for (project of projects(); track project.projectId) {
          <li class="breakdown__row">
            <div class="breakdown__text">
              <span class="breakdown__name">{{ project.projectName }}</span>
              <span class="breakdown__code">{{ project.projectCode }}</span>
            </div>
            <div class="breakdown__bar" aria-hidden="true">
              <span [style.width.%]="share(project.hours)"></span>
            </div>
            <span class="breakdown__hours">{{ project.hours | hours }}</span>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    @use 'tokens' as *;

    .breakdown {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.85rem;
    }

    .breakdown__row {
      display: grid;
      grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr) 4.5rem;
      align-items: center;
      gap: 1rem;
    }

    .breakdown__text {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .breakdown__name {
      font-weight: 550;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .breakdown__code {
      font-size: 0.8rem;
      color: $ink-muted;
    }

    .breakdown__bar {
      height: 0.5rem;
      border-radius: 999px;
      background: $paper;
      overflow: hidden;

      span {
        display: block;
        height: 100%;
        background: $tide-500;
        border-radius: inherit;
      }
    }

    .breakdown__hours {
      text-align: right;
      font-weight: 600;
    }

    @media (max-width: 560px) {
      .breakdown__row {
        grid-template-columns: 1fr 4rem;
      }

      .breakdown__bar {
        grid-column: 1 / -1;
        order: 3;
      }
    }
  `,
})
export class ProjectBreakdown {
  readonly projects = input.required<ProjectHours[]>();

  private readonly max = computed(() => Math.max(1, ...this.projects().map((p) => p.hours)));

  protected share(hours: number): number {
    return (hours / this.max()) * 100;
  }
}
