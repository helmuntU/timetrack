import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, delay, of } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { AuthResponse, User } from '../api/api.models';
import { MockApiError, MockDatabase } from './mock-database';

const db = new MockDatabase();
const TOKEN_PREFIX = 'mock-token.';
const LATENCY_MS = 250;

/**
 * Demo mode only (environment.useMockApi). Answers API calls from MockDatabase with a small
 * delay, so loading states, debounce and request cancellation behave like against the real API.
 */
export const mockApiInterceptor: HttpInterceptorFn = (request, next) => {
  const baseUrl = inject(API_BASE_URL);

  if (!request.url.startsWith(baseUrl)) {
    return next(request);
  }

  const path = request.url.slice(baseUrl.length).split('?')[0];

  try {
    const body = route(request, path);
    return of(
      new HttpResponse({
        status: request.method === 'POST' && body?.created ? 201 : 200,
        body: body?.value ?? null,
      }),
    ).pipe(delay(LATENCY_MS));
  } catch (error) {
    if (error instanceof MockApiError) {
      return failAfterDelay(request, error.status, error.problem);
    }

    return failAfterDelay(request, 500, { title: 'An unexpected error occurred.' });
  }
};

function route(
  request: HttpRequest<unknown>,
  path: string,
): { value: unknown; created?: boolean } | null {
  const method = request.method;
  const body = (request.body ?? {}) as Record<string, unknown>;
  const param = (name: string) => request.params.get(name) ?? undefined;
  const segments = path.split('/').filter(Boolean);

  if (method === 'POST' && path === '/auth/login') {
    const user = db.login(String(body['email'] ?? ''), String(body['password'] ?? ''));
    const response: AuthResponse = {
      accessToken: TOKEN_PREFIX + user.id,
      expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user,
    };
    return { value: response };
  }

  const user = currentUser(request);

  if (method === 'GET' && path === '/auth/me') return { value: user };

  if (segments[0] === 'projects') {
    if (method === 'GET' && segments.length === 1) {
      return {
        value: db.searchProjects(
          param('search') ?? '',
          Number(param('page') ?? 1),
          Number(param('pageSize') ?? 10),
        ),
      };
    }
    if (method === 'GET' && segments.length === 2) return { value: db.getProject(segments[1]) };
  }

  if (segments[0] === 'time-entries') {
    const id = segments[1];
    if (method === 'GET' && !id) {
      return { value: db.listEntries(user.id, param('from'), param('to'), param('status')) };
    }
    if (method === 'GET' && id) return { value: db.getEntry(user, id) };
    if (method === 'POST' && !id) return { value: db.createEntry(user, body), created: true };
    if (method === 'PUT' && id) return { value: db.updateEntry(user, id, body) };
    if (method === 'DELETE' && id) {
      db.deleteEntry(user, id);
      return null;
    }
    if (method === 'POST' && segments[2] === 'submit') return { value: db.submitEntry(user, id) };
  }

  if (segments[0] === 'approvals') {
    if (method === 'GET' && segments[1] === 'pending') return { value: db.pendingApprovals(user) };
    if (method === 'POST' && segments[2] === 'approve')
      return { value: db.review(user, segments[1], 'Approved') };
    if (method === 'POST' && segments[2] === 'reject') {
      return { value: db.review(user, segments[1], 'Rejected', String(body['reason'] ?? '')) };
    }
  }

  if (method === 'GET' && path === '/dashboard/weekly-summary') {
    return { value: db.weeklySummary(user.id, param('weekOf')) };
  }

  throw new MockApiError(404, {
    title: 'Resource not found.',
    detail: `No mock route for ${method} ${path}.`,
  });
}

function currentUser(request: HttpRequest<unknown>): User {
  const header = request.headers.get('Authorization') ?? '';
  const token = header.replace(/^Bearer\s+/i, '');

  if (!token.startsWith(TOKEN_PREFIX)) {
    throw new MockApiError(401, { title: 'Authentication required.' });
  }

  return db.getUser(token.slice(TOKEN_PREFIX.length));
}

function failAfterDelay(
  request: HttpRequest<unknown>,
  status: number,
  error: unknown,
): Observable<never> {
  return new Observable<never>((subscriber) => {
    const timer = setTimeout(
      () => subscriber.error(new HttpErrorResponse({ status, error, url: request.url })),
      LATENCY_MS,
    );
    return () => clearTimeout(timer);
  });
}
