import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { AuthResponse, LoginRequest, User } from '../api/api.models';

const STORAGE_KEY = 'timetrack.session';

interface Session {
  accessToken: string;
  expiresAtUtc: string;
  user: User;
}

/**
 * Holds the session as signals. Components read `user()` / `isManager()` and update
 * automatically; nothing outside this service can write the state (asReadonly).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  private readonly session = signal<Session | null>(readStoredSession());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isManager = computed(() => this.session()?.user.role === 'Manager');

  /** The token, or null when there is no session or it has expired. */
  token(): string | null {
    const session = this.session();

    if (!session) {
      return null;
    }

    if (Date.parse(session.expiresAtUtc) <= Date.now()) {
      this.clearSession();
      return null;
    }

    return session.accessToken;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiBaseUrl}/auth/login`, request).pipe(
      tap((response) => {
        const session: Session = {
          accessToken: response.accessToken,
          expiresAtUtc: response.expiresAtUtc,
          user: response.user,
        };

        this.session.set(session);
        writeStoredSession(session);
      }),
    );
  }

  logout(returnUrl?: string): void {
    this.clearSession();
    void this.router.navigate(['/login'], returnUrl ? { queryParams: { returnUrl } } : undefined);
  }

  private clearSession(): void {
    this.session.set(null);
    writeStoredSession(null);
  }
}

// Browser storage can be unavailable (private mode, blocked cookies): never let it break the app.
function readStoredSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const session = raw ? (JSON.parse(raw) as Session) : null;
    return session && Date.parse(session.expiresAtUtc) > Date.now() ? session : null;
  } catch {
    return null;
  }
}

function writeStoredSession(session: Session | null): void {
  try {
    if (session) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  } catch {
    // Storage not available: the session simply lasts until the tab is closed.
  }
}
