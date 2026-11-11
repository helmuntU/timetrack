import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';
import { ProblemDetails } from './api.models';

/** Reads a ProblemDetails body from any HTTP error, with a safe fallback for network errors. */
export function toProblemDetails(error: unknown): ProblemDetails {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return {
        status: 0,
        title: 'The server is not reachable. Check your connection and try again.',
      };
    }

    if (error.error && typeof error.error === 'object') {
      return { status: error.status, ...(error.error as ProblemDetails) };
    }

    return { status: error.status, title: error.statusText || 'Request failed.' };
  }

  return { title: 'Something went wrong.' };
}

/** The most useful single sentence to show the user. */
export function problemMessage(problem: ProblemDetails): string {
  return problem.detail ?? problem.title ?? 'Something went wrong.';
}

/**
 * Copies 400 validation errors from the API onto the matching form controls, so the
 * server's messages appear under the right field. Returns the errors that matched no control.
 */
export function applyServerErrors(form: FormGroup, problem: ProblemDetails): string[] {
  const unmatched: string[] = [];

  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    const control: AbstractControl | null = form.get(field);

    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages[0] });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }

  return unmatched;
}
