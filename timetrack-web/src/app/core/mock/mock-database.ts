import {
  Project,
  ProblemDetails,
  TIME_RULES,
  TimeEntry,
  TimeEntryStatus,
  User,
  WeeklySummary,
} from '../api/api.models';
import { addDays, startOfWeek, todayIso } from '../../shared/dates';

/**
 * In-memory copy of the API for demo mode. It applies the same business rules as the
 * .NET domain and answers with the same ProblemDetails shapes, so the UI behaves identically.
 */

interface StoredUser extends User {
  password: string;
}

interface State {
  users: StoredUser[];
  projects: Project[];
  entries: TimeEntry[];
}

export class MockApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails,
  ) {
    super(problem.title);
  }
}

const STORAGE_KEY = 'timetrack.mock-db';

const USERS: StoredUser[] = [
  {
    id: 'u-manager',
    email: 'manager@timetrack.dev',
    fullName: 'Laura Bennett',
    role: 'Manager',
    password: 'Demo123!',
  },
  {
    id: 'u-felipe',
    email: 'consultant@timetrack.dev',
    fullName: 'Felipe Urueta',
    role: 'Consultant',
    password: 'Demo123!',
  },
  {
    id: 'u-priya',
    email: 'priya@timetrack.dev',
    fullName: 'Priya Sharma',
    role: 'Consultant',
    password: 'Demo123!',
  },
];

const PROJECTS: Project[] = [
  {
    id: 'p-aud',
    code: 'AUD-2026-014',
    name: 'Annual Financial Audit',
    clientName: 'Northwind Retail',
    budgetHours: 1200,
    isActive: true,
  },
  {
    id: 'p-cld',
    code: 'CLD-2026-021',
    name: 'Azure Cloud Migration',
    clientName: 'Fabrikam Bank',
    budgetHours: 900,
    isActive: true,
  },
  {
    id: 'p-dai',
    code: 'DAI-2026-003',
    name: 'Data & AI Strategy',
    clientName: 'Adventure Works',
    budgetHours: 650,
    isActive: true,
  },
  {
    id: 'p-tax',
    code: 'TAX-2026-007',
    name: 'Tax Advisory',
    clientName: 'Contoso Energy',
    budgetHours: 400,
    isActive: true,
  },
  {
    id: 'p-int',
    code: 'INT-2025-099',
    name: 'Internal Training',
    clientName: 'TimeTrack',
    budgetHours: 200,
    isActive: false,
  },
];

export class MockDatabase {
  private state: State = loadState() ?? seedState();

  login(email: string, password: string): User {
    const user = this.state.users.find((u) => u.email === email.trim().toLowerCase());

    if (!user || user.password !== password) {
      throw new MockApiError(401, {
        title: 'Authentication failed.',
        detail: 'Invalid email or password.',
      });
    }

    return publicUser(user);
  }

  getUser(id: string): User {
    const user = this.state.users.find((u) => u.id === id);

    if (!user) {
      throw new MockApiError(401, { title: 'Authentication required.' });
    }

    return publicUser(user);
  }

  searchProjects(search: string, page: number, pageSize: number) {
    const term = search.trim().toLowerCase();
    const matches = this.state.projects
      .filter((p) => p.isActive)
      .filter(
        (p) =>
          !term ||
          [p.code, p.name, p.clientName].some((value) => value.toLowerCase().includes(term)),
      )
      .sort((a, b) => a.code.localeCompare(b.code));

    return {
      items: matches.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      totalCount: matches.length,
      totalPages: Math.ceil(matches.length / pageSize),
    };
  }

  getProject(id: string): Project {
    const project = this.state.projects.find((p) => p.id === id);

    if (!project) {
      throw notFound('Project', id);
    }

    return project;
  }

  listEntries(userId: string, from?: string, to?: string, status?: string): TimeEntry[] {
    return this.state.entries
      .filter((e) => e.userId === userId)
      .filter((e) => !from || e.workDate >= from)
      .filter((e) => !to || e.workDate <= to)
      .filter((e) => !status || e.status === status)
      .sort(
        (a, b) =>
          b.workDate.localeCompare(a.workDate) || b.updatedAtUtc.localeCompare(a.updatedAtUtc),
      );
  }

  getEntry(user: User, id: string): TimeEntry {
    const entry = this.state.entries.find((e) => e.id === id);

    if (!entry || (entry.userId !== user.id && user.role !== 'Manager')) {
      throw notFound('TimeEntry', id);
    }

    return entry;
  }

  createEntry(user: User, body: Record<string, unknown>): TimeEntry {
    const input = validateEntryInput(body, false);
    const project = this.activeProject(input.projectId);
    this.ensureDailyLimit(user.id, input.workDate, input.hours, null);

    const entry: TimeEntry = {
      id: newId(),
      userId: user.id,
      userFullName: user.fullName,
      projectId: project.id,
      projectCode: project.code,
      projectName: project.name,
      workDate: input.workDate,
      hours: input.hours,
      description: input.description,
      status: 'Draft',
      rejectionReason: null,
      updatedAtUtc: new Date().toISOString(),
      concurrencyStamp: newId(),
    };

    this.state.entries.push(entry);
    this.save();
    return entry;
  }

  updateEntry(user: User, id: string, body: Record<string, unknown>): TimeEntry {
    const input = validateEntryInput(body, true);
    const entry = this.ownedEntry(user, id);

    if (entry.concurrencyStamp !== input.concurrencyStamp) {
      throw new MockApiError(409, {
        title: 'Concurrency conflict.',
        detail: 'The entry was modified by someone else. Reload it and try again.',
      });
    }

    if (entry.status !== 'Draft' && entry.status !== 'Rejected') {
      throw businessRule(
        'TimeEntry.NotEditable',
        `An entry in status ${entry.status} can't be modified.`,
      );
    }

    const project = this.activeProject(input.projectId);
    this.ensureDailyLimit(user.id, input.workDate, input.hours, entry.id);

    Object.assign(entry, {
      projectId: project.id,
      projectCode: project.code,
      projectName: project.name,
      workDate: input.workDate,
      hours: input.hours,
      description: input.description,
      status: 'Draft',
      rejectionReason: null,
    });
    touch(entry);
    this.save();
    return entry;
  }

  deleteEntry(user: User, id: string): void {
    const entry = this.ownedEntry(user, id);

    if (entry.status !== 'Draft') {
      throw businessRule('TimeEntry.NotDeletable', 'Only draft entries can be deleted.');
    }

    this.state.entries = this.state.entries.filter((e) => e.id !== id);
    this.save();
  }

  submitEntry(user: User, id: string): TimeEntry {
    const entry = this.ownedEntry(user, id);

    if (entry.status !== 'Draft' && entry.status !== 'Rejected') {
      throw businessRule(
        'TimeEntry.InvalidTransition',
        `An entry in status ${entry.status} cannot be submitted.`,
      );
    }

    entry.status = 'Submitted';
    entry.rejectionReason = null;
    touch(entry);
    this.save();
    return entry;
  }

  pendingApprovals(manager: User): TimeEntry[] {
    ensureManager(manager);
    return this.state.entries
      .filter((e) => e.status === 'Submitted' && e.userId !== manager.id)
      .sort((a, b) => a.workDate.localeCompare(b.workDate));
  }

  review(manager: User, id: string, decision: 'Approved' | 'Rejected', reason?: string): TimeEntry {
    ensureManager(manager);

    if (decision === 'Rejected' && !reason?.trim()) {
      throw new MockApiError(400, {
        title: 'One or more validation errors occurred.',
        errors: { reason: ["'Reason' must not be empty."] },
      });
    }

    const entry = this.state.entries.find((e) => e.id === id);

    if (!entry) {
      throw notFound('TimeEntry', id);
    }

    if (entry.status !== 'Submitted') {
      throw businessRule('TimeEntry.NotSubmitted', 'Only submitted entries can be reviewed.');
    }

    if (entry.userId === manager.id) {
      throw businessRule('TimeEntry.SelfReview', "You can't review your own time entries.");
    }

    entry.status = decision;
    entry.rejectionReason = decision === 'Rejected' ? reason!.trim() : null;
    touch(entry);
    this.save();
    return entry;
  }

  weeklySummary(userId: string, weekOf?: string): WeeklySummary {
    const weekStart = startOfWeek(weekOf || todayIso());
    const weekEnd = addDays(weekStart, 6);
    const entries = this.listEntries(userId, weekStart, weekEnd);
    const sum = (items: TimeEntry[]) => items.reduce((total, e) => total + e.hours, 0);
    const byStatus = (status: TimeEntryStatus) => sum(entries.filter((e) => e.status === status));

    const projects = new Map<
      string,
      { projectId: string; projectCode: string; projectName: string; hours: number }
    >();
    for (const e of entries) {
      const current = projects.get(e.projectId) ?? {
        projectId: e.projectId,
        projectCode: e.projectCode,
        projectName: e.projectName,
        hours: 0,
      };
      current.hours += e.hours;
      projects.set(e.projectId, current);
    }

    return {
      weekStart,
      weekEnd,
      totalHours: sum(entries),
      draftHours: byStatus('Draft'),
      submittedHours: byStatus('Submitted'),
      approvedHours: byStatus('Approved'),
      rejectedHours: byStatus('Rejected'),
      dailyHours: Array.from({ length: 7 }, (_, i) => {
        const date = addDays(weekStart, i);
        return { date, hours: sum(entries.filter((e) => e.workDate === date)) };
      }),
      hoursByProject: [...projects.values()].sort((a, b) => b.hours - a.hours),
    };
  }

  private ownedEntry(user: User, id: string): TimeEntry {
    const entry = this.state.entries.find((e) => e.id === id);

    if (!entry || entry.userId !== user.id) {
      throw notFound('TimeEntry', id);
    }

    return entry;
  }

  private activeProject(id: string): Project {
    const project = this.getProject(id);

    if (!project.isActive) {
      throw businessRule('TimeEntry.InactiveProject', `Project ${project.code} is not active.`);
    }

    return project;
  }

  private ensureDailyLimit(
    userId: string,
    workDate: string,
    hours: number,
    excludeId: string | null,
  ): void {
    const logged = this.state.entries
      .filter((e) => e.userId === userId && e.workDate === workDate && e.id !== excludeId)
      .reduce((total, e) => total + e.hours, 0);

    if (logged + hours > TIME_RULES.maxHoursPerDay) {
      throw businessRule(
        'TimeEntry.DailyLimitExceeded',
        `You already logged ${logged} hours on ${workDate}. The daily limit is ${TIME_RULES.maxHoursPerDay} hours.`,
      );
    }
  }

  private save(): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(this.state));
    } catch {
      // Demo data just won't survive a reload.
    }
  }
}

function validateEntryInput(body: Record<string, unknown>, isUpdate: boolean) {
  const errors: Record<string, string[]> = {};
  const projectId = String(body['projectId'] ?? '');
  const workDate = String(body['workDate'] ?? '');
  const hours = Number(body['hours']);
  const description = String(body['description'] ?? '').trim();
  const concurrencyStamp = String(body['concurrencyStamp'] ?? '');

  if (!projectId) errors['projectId'] = ["'Project Id' must not be empty."];
  if (!/^\d{4}-\d{2}-\d{2}$/.test(workDate))
    errors['workDate'] = ["'Work Date' must not be empty."];
  if (!(hours > 0 && hours <= TIME_RULES.maxHoursPerEntry)) {
    errors['hours'] = [
      `'Hours' must be greater than 0 and at most ${TIME_RULES.maxHoursPerEntry}.`,
    ];
  }
  if (!description) errors['description'] = ["'Description' must not be empty."];
  if (description.length > TIME_RULES.maxDescriptionLength) {
    errors['description'] = [
      `'Description' must be ${TIME_RULES.maxDescriptionLength} characters or fewer.`,
    ];
  }
  if (isUpdate && !concurrencyStamp)
    errors['concurrencyStamp'] = ["'Concurrency Stamp' must not be empty."];

  if (Object.keys(errors).length > 0) {
    throw new MockApiError(400, { title: 'One or more validation errors occurred.', errors });
  }

  // Same rules as TimeEntry in the .NET domain.
  if ((hours * 100) % (TIME_RULES.hoursIncrement * 100) !== 0) {
    throw businessRule(
      'TimeEntry.InvalidIncrement',
      'Hours must be logged in increments of 15 minutes (0.25).',
    );
  }
  if (workDate > todayIso()) {
    throw businessRule('TimeEntry.FutureDate', "You can't log hours for a future date.");
  }

  return { projectId, workDate, hours, description, concurrencyStamp };
}

function businessRule(code: string, detail: string): MockApiError {
  return new MockApiError(422, { title: 'A business rule was violated.', detail, code });
}

function notFound(resource: string, id: string): MockApiError {
  return new MockApiError(404, {
    title: 'Resource not found.',
    detail: `${resource} '${id}' was not found.`,
  });
}

function ensureManager(user: User): void {
  if (user.role !== 'Manager') {
    throw new MockApiError(403, {
      title: 'Forbidden.',
      detail: 'Only managers can review time entries.',
    });
  }
}

function publicUser({ password: _password, ...user }: StoredUser): User {
  return user;
}

function touch(entry: TimeEntry): void {
  entry.updatedAtUtc = new Date().toISOString();
  entry.concurrencyStamp = newId();
}

function newId(): string {
  return (
    globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
  );
}

function loadState(): State | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as State) : null;
  } catch {
    return null;
  }
}

function seedState(): State {
  const today = todayIso();
  const lastMonday = addDays(startOfWeek(today), -7);
  const lastWeek = (weekday: number) => addDays(lastMonday, weekday);
  const project = (id: string) => PROJECTS.find((p) => p.id === id)!;
  const entry = (
    userId: string,
    projectId: string,
    workDate: string,
    hours: number,
    description: string,
    status: TimeEntryStatus,
    rejectionReason: string | null = null,
  ): TimeEntry => {
    const p = project(projectId);
    return {
      id: newId(),
      userId,
      userFullName: USERS.find((u) => u.id === userId)!.fullName,
      projectId,
      projectCode: p.code,
      projectName: p.name,
      workDate,
      hours,
      description,
      status,
      rejectionReason,
      updatedAtUtc: new Date().toISOString(),
      concurrencyStamp: newId(),
    };
  };

  // Same demo data as the .NET DatabaseInitializer: a full previous week plus today's drafts.
  const felipe = 'u-felipe';
  const priya = 'u-priya';

  return {
    users: USERS,
    projects: PROJECTS,
    entries: [
      entry(
        felipe,
        'p-cld',
        lastWeek(0),
        8,
        'Sprint planning and landing zone backlog grooming',
        'Approved',
      ),
      entry(
        felipe,
        'p-cld',
        lastWeek(1),
        6,
        'Designed the landing zone and network topology',
        'Approved',
      ),
      entry(
        felipe,
        'p-dai',
        lastWeek(1),
        2,
        'Stakeholder interviews for the AI use-case inventory',
        'Approved',
      ),
      entry(
        felipe,
        'p-aud',
        lastWeek(2),
        7.5,
        'Walkthrough of the revenue recognition controls',
        'Approved',
      ),
      entry(
        felipe,
        'p-cld',
        lastWeek(3),
        4,
        'Bicep modules for the hub-and-spoke network',
        'Approved',
      ),
      entry(
        felipe,
        'p-aud',
        lastWeek(3),
        4,
        'Client meeting',
        'Rejected',
        'Please describe which audit area the meeting covered.',
      ),
      entry(
        felipe,
        'p-cld',
        lastWeek(4),
        7.5,
        'Migrated the reporting API to Azure App Service',
        'Submitted',
      ),
      entry(
        felipe,
        'p-dai',
        lastWeek(4),
        1,
        'Prepared the data governance workshop agenda',
        'Submitted',
      ),
      entry(
        felipe,
        'p-dai',
        today,
        3,
        'Workshop prep: data governance maturity assessment',
        'Draft',
      ),
      entry(felipe, 'p-cld', today, 2.5, 'Code review of the Angular time-entry module', 'Draft'),
      entry(
        priya,
        'p-tax',
        lastWeek(4),
        8,
        'Prepared the transfer pricing documentation',
        'Submitted',
      ),
      entry(priya, 'p-aud', today, 5.25, 'Tested revenue recognition controls', 'Submitted'),
    ],
  };
}
