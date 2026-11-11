import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withHashLocation,
  withInMemoryScrolling,
} from '@angular/router';
import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { mockApiInterceptor } from './core/mock/mock-api.interceptor';

// Order matters: the auth interceptor runs first so the mock backend also receives the token.
const interceptors = environment.useMockApi
  ? [authInterceptor, mockApiInterceptor]
  : [authInterceptor];

/**
 * Zoneless application (Angular's default since v21): change detection is driven by
 * signals and events, with no zone.js patching every async browser API.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
      // Hash routes (#/week) for static hosting where the server can't fall back to index.html.
      ...(environment.useHashRouting ? [withHashLocation()] : []),
    ),
    provideHttpClient(withFetch(), withInterceptors(interceptors)),
  ],
};
