import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import {
  PagedResult,
  Project,
  SaveTimeEntryRequest,
  TimeEntry,
  TimeEntryFilter,
} from './api.models';

/**
 * Typed gateway to the API for commands and one-off reads.
 * (Screens that just display data use httpResource directly, see the pages.)
 */
@Injectable({ providedIn: 'root' })
export class TimeTrackApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  searchProjects(search: string, pageSize = 8): Observable<PagedResult<Project>> {
    const params = new HttpParams().set('search', search).set('pageSize', pageSize);
    return this.http.get<PagedResult<Project>>(`${this.baseUrl}/projects`, { params });
  }

  getProject(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/projects/${id}`);
  }

  getTimeEntry(id: string): Observable<TimeEntry> {
    return this.http.get<TimeEntry>(`${this.baseUrl}/time-entries/${id}`);
  }

  listTimeEntries(filter: TimeEntryFilter): Observable<TimeEntry[]> {
    return this.http.get<TimeEntry[]>(`${this.baseUrl}/time-entries`, { params: toParams(filter) });
  }

  createTimeEntry(request: SaveTimeEntryRequest): Observable<TimeEntry> {
    return this.http.post<TimeEntry>(`${this.baseUrl}/time-entries`, request);
  }

  updateTimeEntry(id: string, request: SaveTimeEntryRequest): Observable<TimeEntry> {
    return this.http.put<TimeEntry>(`${this.baseUrl}/time-entries/${id}`, request);
  }

  deleteTimeEntry(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/time-entries/${id}`);
  }

  submitTimeEntry(id: string): Observable<TimeEntry> {
    return this.http.post<TimeEntry>(`${this.baseUrl}/time-entries/${id}/submit`, null);
  }

  approve(id: string): Observable<TimeEntry> {
    return this.http.post<TimeEntry>(`${this.baseUrl}/approvals/${id}/approve`, null);
  }

  reject(id: string, reason: string): Observable<TimeEntry> {
    return this.http.post<TimeEntry>(`${this.baseUrl}/approvals/${id}/reject`, { reason });
  }
}

/** Drops empty values so the URL only carries the filters the user actually set. */
export function toParams(filter: object): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(filter)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
