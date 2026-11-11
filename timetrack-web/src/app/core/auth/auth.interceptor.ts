import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { AuthService } from './auth.service';

/**
 * Adds the bearer token to API calls and ends the session when the API answers 401.
 * inject() runs here at the top: inside catchError there is no injection context anymore.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const apiBaseUrl = inject(API_BASE_URL);

  // Only our API gets the token: never leak it to third-party URLs.
  const isApiCall = request.url.startsWith(apiBaseUrl);
  const isLoginCall = request.url.endsWith('/auth/login');
  const token = isApiCall ? auth.token() : null;

  // Requests are immutable, so we clone them to add the header.
  const authorized = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiCall && !isLoginCall) {
        auth.logout(router.url);
      }

      return throwError(() => error);
    }),
  );
};
