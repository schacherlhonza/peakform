import { describe, expect, it } from 'vitest';
import { addDays, mondayOf, toIsoDate } from './dateUtils';

describe('toIsoDate', () => {
  it('formats a date as YYYY-MM-DD', () => {
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('pads single-digit month and day', () => {
    expect(toIsoDate(new Date(2026, 2, 3))).toBe('2026-03-03');
  });
});

describe('mondayOf', () => {
  it('returns the same date when already Monday', () => {
    const monday = new Date(2026, 0, 5); // a Monday
    expect(toIsoDate(mondayOf(monday))).toBe('2026-01-05');
  });

  it('rolls back a Wednesday to the preceding Monday', () => {
    const wednesday = new Date(2026, 0, 7);
    expect(toIsoDate(mondayOf(wednesday))).toBe('2026-01-05');
  });

  it('rolls back a Sunday to the preceding Monday, not forward', () => {
    const sunday = new Date(2026, 0, 11);
    expect(toIsoDate(mondayOf(sunday))).toBe('2026-01-05');
  });
});

describe('addDays', () => {
  it('adds positive days across a month boundary', () => {
    expect(toIsoDate(addDays(new Date(2026, 0, 30), 3))).toBe('2026-02-02');
  });

  it('subtracts days with a negative argument', () => {
    expect(toIsoDate(addDays(new Date(2026, 1, 2), -3))).toBe('2026-01-30');
  });
});
