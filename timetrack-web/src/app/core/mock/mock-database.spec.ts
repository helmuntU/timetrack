import { addDays, todayIso } from '../../shared/dates';
import { MockApiError, MockDatabase } from './mock-database';

/** Demo mode must enforce the same rules as the .NET domain, or the demo would lie. */
describe('MockDatabase business rules', () => {
  let db: MockDatabase;
  const felipe = {
    id: 'u-felipe',
    email: 'consultant@timetrack.dev',
    fullName: 'Felipe Urueta',
    role: 'Consultant' as const,
  };
  const laura = {
    id: 'u-manager',
    email: 'manager@timetrack.dev',
    fullName: 'Laura Bennett',
    role: 'Manager' as const,
  };

  beforeEach(() => {
    sessionStorage.clear();
    db = new MockDatabase();
  });

  const create = (overrides: Record<string, unknown> = {}) =>
    db.createEntry(felipe, {
      projectId: 'p-cld',
      workDate: addDays(todayIso(), -20),
      hours: 2,
      description: 'Work',
      ...overrides,
    });

  function expectError(action: () => unknown, status: number, code?: string): void {
    try {
      action();
      expect.unreachable('Expected an error');
    } catch (error) {
      expect(error).toBeInstanceOf(MockApiError);
      expect((error as MockApiError).status).toBe(status);
      if (code) {
        expect((error as MockApiError).problem.code).toBe(code);
      }
    }
  }

  it('rejects more than 12 hours on the same day', () => {
    const date = addDays(todayIso(), -21);
    create({ workDate: date, hours: 8 });

    expectError(() => create({ workDate: date, hours: 4.25 }), 422, 'TimeEntry.DailyLimitExceeded');
  });

  it('rejects hours that are not 15-minute steps', () => {
    expectError(() => create({ hours: 1.1 }), 422, 'TimeEntry.InvalidIncrement');
  });

  it('returns field errors (400) for missing data', () => {
    expectError(() => create({ description: '  ' }), 400);
  });

  it('detects a stale concurrency stamp (409)', () => {
    const entry = create();
    const staleStamp = entry.concurrencyStamp;
    db.updateEntry(felipe, entry.id, { ...entry, hours: 3, concurrencyStamp: staleStamp });

    expectError(
      () => db.updateEntry(felipe, entry.id, { ...entry, hours: 4, concurrencyStamp: staleStamp }),
      409,
    );
  });

  it('follows the approval lifecycle and blocks self-review', () => {
    const entry = create();
    db.submitEntry(felipe, entry.id);

    expectError(() => db.review(felipe, entry.id, 'Approved'), 403);
    expect(db.review(laura, entry.id, 'Approved').status).toBe('Approved');
    expectError(
      () =>
        db.updateEntry(felipe, entry.id, { ...entry, concurrencyStamp: entry.concurrencyStamp }),
      422,
      'TimeEntry.NotEditable',
    );
  });
});
