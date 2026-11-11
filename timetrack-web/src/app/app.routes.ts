import { Routes } from '@angular/router';
import { authGuard, guestGuard, managerGuard } from './core/auth/auth.guards';

/**
 * Every feature is lazy loaded (loadComponent), so the first download only contains
 * the shell and the page the user opens.
 */
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · TimeTrack',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'week' },
      {
        path: 'week',
        title: 'My week · TimeTrack',
        loadComponent: () => import('./features/week/week.page').then((m) => m.WeekPage),
      },
      {
        path: 'entries',
        title: 'Time entries · TimeTrack',
        loadComponent: () => import('./features/entries/entries.page').then((m) => m.EntriesPage),
      },
      {
        path: 'entries/new',
        title: 'Log hours · TimeTrack',
        loadComponent: () =>
          import('./features/entries/entry-form.page').then((m) => m.EntryFormPage),
      },
      {
        path: 'entries/:id/edit',
        title: 'Edit entry · TimeTrack',
        loadComponent: () =>
          import('./features/entries/entry-form.page').then((m) => m.EntryFormPage),
      },
      {
        path: 'approvals',
        title: 'Approvals · TimeTrack',
        canActivate: [managerGuard],
        loadComponent: () =>
          import('./features/approvals/approvals.page').then((m) => m.ApprovalsPage),
      },
    ],
  },
  {
    path: '**',
    title: 'Page not found · TimeTrack',
    loadComponent: () => import('./features/not-found/not-found.page').then((m) => m.NotFoundPage),
  },
];
