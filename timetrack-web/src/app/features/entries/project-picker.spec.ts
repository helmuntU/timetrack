import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/api/api-config';
import { PagedResult, Project } from '../../core/api/api.models';
import { ProjectPicker } from './project-picker';

const azure: Project = {
  id: 'p-cld',
  code: 'CLD-2026-021',
  name: 'Azure Cloud Migration',
  clientName: 'Fabrikam Bank',
  budgetHours: 900,
  isActive: true,
};

const page = (items: Project[]): PagedResult<Project> => ({
  items,
  page: 1,
  pageSize: 8,
  totalCount: items.length,
  totalPages: 1,
});

describe('ProjectPicker', () => {
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();

    TestBed.configureTestingModule({
      imports: [ProjectPicker],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api/v1' },
      ],
    });

    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    vi.useRealTimers();
  });

  function setup() {
    const fixture = TestBed.createComponent(ProjectPicker);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');

    const type = (text: string) => {
      input.value = text;
      input.dispatchEvent(new Event('input'));
    };

    return { fixture, input, type };
  }

  it('waits for the user to stop typing before searching (debounce)', () => {
    const { type } = setup();

    type('a');
    type('az');
    type('azu');
    vi.advanceTimersByTime(249);
    httpTesting.expectNone((req) => req.url === '/api/v1/projects');

    vi.advanceTimersByTime(1);
    const request = httpTesting.expectOne((req) => req.url === '/api/v1/projects');
    expect(request.request.params.get('search')).toBe('azu');
    request.flush(page([azure]));
  });

  it('cancels the previous request when a new search starts (switchMap)', () => {
    const { type } = setup();

    type('fab');
    vi.advanceTimersByTime(250);
    const first = httpTesting.expectOne((req) => req.params.get('search') === 'fab');

    type('fabrikam');
    vi.advanceTimersByTime(250);
    const second = httpTesting.expectOne((req) => req.params.get('search') === 'fabrikam');

    expect(first.cancelled).toBe(true);
    second.flush(page([azure]));
  });

  it('reports the selected project id to the form', () => {
    const { fixture, type } = setup();
    const onChange = vi.fn();
    fixture.componentInstance.registerOnChange(onChange);

    type('azure');
    vi.advanceTimersByTime(250);
    httpTesting.expectOne((req) => req.url === '/api/v1/projects').flush(page([azure]));
    fixture.detectChanges();

    const option: HTMLElement = fixture.nativeElement.querySelector('[role="option"]');
    option.click();

    expect(onChange).toHaveBeenCalledWith('p-cld');
  });
});
