import { describe, it, expect } from 'vitest';
import { formatDistance, formatMinutesAgo, pluralise } from './format';

describe('formatDistance', () => {
  it('shows metres when under 1 km', () => {
    expect(formatDistance(500)).toBe('500 m');
  });

  it('rounds to nearest metre', () => {
    expect(formatDistance(123.7)).toBe('124 m');
  });

  it('shows km with one decimal at exactly 1000 m', () => {
    expect(formatDistance(1000)).toBe('1.0 km');
  });

  it('shows km with one decimal above 1 km', () => {
    expect(formatDistance(2500)).toBe('2.5 km');
  });

  it('shows km correctly for large distances', () => {
    expect(formatDistance(10500)).toBe('10.5 km');
  });
});

describe('formatMinutesAgo', () => {
  it('returns "No reports yet" for null', () => {
    expect(formatMinutesAgo(null)).toBe('No reports yet');
  });

  it('returns "Just now" for 0 minutes', () => {
    expect(formatMinutesAgo(0)).toBe('Just now');
  });

  it('returns minutes for values under 60', () => {
    expect(formatMinutesAgo(5)).toBe('5 min ago');
    expect(formatMinutesAgo(59)).toBe('59 min ago');
  });

  it('returns hours for values 60 and above', () => {
    expect(formatMinutesAgo(60)).toBe('1 hr ago');
    expect(formatMinutesAgo(120)).toBe('2 hrs ago');
    expect(formatMinutesAgo(90)).toBe('1 hr ago');
  });
});

describe('pluralise', () => {
  it('uses singular for count of 1', () => {
    expect(pluralise(1, 'report')).toBe('1 report');
  });

  it('uses plural for count of 0', () => {
    expect(pluralise(0, 'report')).toBe('0 reports');
  });

  it('uses plural for counts above 1', () => {
    expect(pluralise(42, 'station')).toBe('42 stations');
  });
});
