import { HoursPipe } from './hours.pipe';

describe('HoursPipe', () => {
  const pipe = new HoursPipe();

  it.each([
    [8, '8h'],
    [7.5, '7h 30m'],
    [0.25, '15m'],
    [5.25, '5h 15m'],
    [0, '0h'],
  ])('formats %s as %s', (value, expected) => {
    expect(pipe.transform(value)).toBe(expected);
  });

  it('shows a dash for missing values', () => {
    expect(pipe.transform(null)).toBe('—');
    expect(pipe.transform(undefined)).toBe('—');
  });
});
