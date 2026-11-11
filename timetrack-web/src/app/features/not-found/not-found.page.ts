import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="not-found">
      <h1>This page doesn't exist</h1>
      <p class="muted">The link may be old, or the address has a typo.</p>
      <a class="btn btn-primary" routerLink="/week">Go to my week</a>
    </main>
  `,
  styles: `
    .not-found {
      max-width: 32rem;
      margin: 18vh auto 0;
      padding: 0 1rem;
    }
  `,
})
export class NotFoundPage {}
