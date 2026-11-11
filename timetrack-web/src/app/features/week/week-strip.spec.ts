import { TestBed } from '@angular/core/testing';
import { DayColumn, WeekStrip } from './week-strip';

const day = (date: string, total: number, extra: Partial<DayColumn> = {}): DayColumn => ({
  date,
  weekday: 'Mon',
  dayOfMonth: Number(date.slice(-2)),
  total,
  segments: total ? [{ status: 'Approved', hours: total }] : [],
  isToday: false,
  isFuture: false,
  ...extra,
});

describe('WeekStrip', () => {
  function render(days: DayColumn[], selected: string | null = null) {
    const fixture = TestBed.createComponent(WeekStrip);
    fixture.componentRef.setInput('days', days);
    fixture.componentRef.setInput('selected', selected);
    fixture.detectChanges();
    return fixture;
  }

  const week = Array.from({ length: 7 }, (_, i) =>
    day(`2026-11-${String(9 + i).padStart(2, '0')}`, i < 5 ? 6 + i : 0),
  );

  it('renders one column per day with its total', () => {
    const fixture = render(week);
    const columns = fixture.nativeElement.querySelectorAll('.day');

    expect(columns.length).toBe(7);
    expect(columns[0].querySelector('.day__total').textContent.trim()).toBe('6h');
    expect(columns[6].querySelector('.day__total').textContent.trim()).toBe('–');
  });

  it('scales bar height against the 12-hour daily limit', () => {
    const fixture = render([day('2026-11-09', 6)]);
    const segment: HTMLElement = fixture.nativeElement.querySelector('.day__segment');

    expect(segment.style.height).toBe('50%');
  });

  it('emits the clicked day and marks the selected one', () => {
    const fixture = render(week, '2026-11-10');
    const emitted: string[] = [];
    fixture.componentInstance.daySelected.subscribe((date) => emitted.push(date));

    const columns = fixture.nativeElement.querySelectorAll('.day');
    expect(columns[1].getAttribute('aria-pressed')).toBe('true');

    columns[3].click();
    expect(emitted).toEqual(['2026-11-12']);
  });
});
