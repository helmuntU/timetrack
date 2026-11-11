/**
 * Contracts shared with the .NET API. They mirror the C# DTOs one to one,
 * so if the back end changes a field the TypeScript compiler points at every usage.
 */

export type UserRole = 'Consultant' | 'Manager';

export type TimeEntryStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';

/** Dates travel as ISO calendar dates ("2026-11-09"), never as Date objects with time zones. */
export type IsoDate = string;

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: User;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Project {
  id: string;
  code: string;
  name: string;
  clientName: string;
  budgetHours: number;
  isActive: boolean;
}

export interface TimeEntry {
  id: string;
  userId: string;
  userFullName: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  workDate: IsoDate;
  hours: number;
  description: string;
  status: TimeEntryStatus;
  rejectionReason: string | null;
  updatedAtUtc: string;
  concurrencyStamp: string;
}

export interface TimeEntryFilter {
  from?: IsoDate;
  to?: IsoDate;
  status?: TimeEntryStatus;
}

export interface SaveTimeEntryRequest {
  projectId: string;
  workDate: IsoDate;
  hours: number;
  description: string;
  /** Required for updates (optimistic concurrency), ignored for creates. */
  concurrencyStamp?: string;
}

export interface DailyHours {
  date: IsoDate;
  hours: number;
}

export interface ProjectHours {
  projectId: string;
  projectCode: string;
  projectName: string;
  hours: number;
}

export interface WeeklySummary {
  weekStart: IsoDate;
  weekEnd: IsoDate;
  totalHours: number;
  draftHours: number;
  submittedHours: number;
  approvedHours: number;
  rejectedHours: number;
  dailyHours: DailyHours[];
  hoursByProject: ProjectHours[];
}

/** RFC 7807 ProblemDetails, as returned by the API's global exception handler. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

/** Business limits enforced by the API domain; the UI uses them for instant feedback. */
export const TIME_RULES = {
  hoursIncrement: 0.25,
  maxHoursPerEntry: 8,
  maxHoursPerDay: 12,
  targetHoursPerDay: 8,
  maxDescriptionLength: 500,
  maxRejectionReasonLength: 300,
} as const;
