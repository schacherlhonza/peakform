import { describe, expect, it } from 'vitest';
import { IntensityTargetType, WorkoutSegmentType, type WorkoutSegmentDto } from '../../api/generated/models';
import i18n from '../../i18n';
import { garminStepCount, normalizeSegments, parseClock, segmentTotals, summarizeStructure, targetLabel } from './segmentFormat';

const block = (repeatCount: number, steps: WorkoutSegmentDto[]): WorkoutSegmentDto => ({
  type: WorkoutSegmentType.Repeat,
  repeatCount,
  intensityTargetType: IntensityTargetType.Free,
  steps,
});

describe('parseClock', () => {
  it('reads a bare number as minutes', () => {
    expect(parseClock('15')).toBe(900);
  });

  it('reads m:ss and h:mm:ss', () => {
    expect(parseClock('4:30')).toBe(270);
    expect(parseClock('1:05:00')).toBe(3900);
  });

  it('rejects malformed input and treats empty as no value', () => {
    expect(parseClock('4:75')).toBeNull();
    expect(parseClock('abc')).toBeNull();
    expect(parseClock('  ')).toBeNull();
  });
});

describe('segmentTotals', () => {
  it('multiplies by repeats and keeps missing dimensions null', () => {
    const totals = segmentTotals([
      { type: WorkoutSegmentType.WarmUp, durationSeconds: 900 },
      { type: WorkoutSegmentType.Interval, durationSeconds: 300, repeatCount: 6 },
    ]);
    expect(totals).toEqual({ durationSeconds: 2700, distanceMeters: null });
  });

  it('multiplies block steps by the block repeats', () => {
    const totals = segmentTotals([
      block(6, [
        { type: WorkoutSegmentType.Interval, distanceMeters: 1000 },
        { type: WorkoutSegmentType.Recovery, durationSeconds: 120 },
      ]),
    ]);
    expect(totals).toEqual({ durationSeconds: 720, distanceMeters: 6000 });
  });
});

describe('garminStepCount', () => {
  it('counts a repeat as one step plus its children, not unrolled', () => {
    const count = garminStepCount([
      { type: WorkoutSegmentType.WarmUp, durationSeconds: 900 },
      { type: WorkoutSegmentType.Interval, durationSeconds: 60, repeatCount: 10 },
      block(6, [{ type: WorkoutSegmentType.Interval }, { type: WorkoutSegmentType.Recovery }]),
    ]);
    expect(count).toBe(1 + 2 + 3);
  });
});

describe('normalizeSegments', () => {
  it('renumbers by position and drops targets of other target types', () => {
    const [a, b] = normalizeSegments([
      { order: 7, type: WorkoutSegmentType.Main, intensityTargetType: IntensityTargetType.HeartRateZone, targetHeartRateZoneNumber: 2, targetRpe: 5 },
      { order: 3, type: WorkoutSegmentType.Main, intensityTargetType: IntensityTargetType.Pace, targetPaceSecondsPerKmMin: 290, targetPaceSecondsPerKmMax: 270 },
    ]);
    expect(a.order).toBe(1);
    expect(a.targetHeartRateZoneNumber).toBe(2);
    expect(a.targetRpe).toBeNull();
    expect(b.order).toBe(2);
    expect([b.targetPaceSecondsPerKmMin, b.targetPaceSecondsPerKmMax]).toEqual([270, 290]);
  });
});

describe('normalizeSegments with blocks', () => {
  it('keeps one end condition, strips block fields and drops empty blocks', () => {
    const [step, b] = normalizeSegments([
      { type: WorkoutSegmentType.Main, durationSeconds: 600, distanceMeters: 2000 },
      { ...block(1, [{ type: WorkoutSegmentType.Interval, durationSeconds: 60, repeatCount: 3 }]), durationSeconds: 999 },
      block(4, []),
    ]);
    expect(step.distanceMeters).toBeNull();
    expect(b.repeatCount).toBe(2);
    expect(b.durationSeconds).toBeUndefined();
    expect(b.steps).toHaveLength(1);
    expect(b.steps![0]).toMatchObject({ order: 1, repeatCount: null, steps: null });
  });
});

describe('targetLabel', () => {
  it('labels zone and pace range targets', () => {
    expect(targetLabel({ intensityTargetType: IntensityTargetType.HeartRateZone, targetHeartRateZoneNumber: 4 })).toBe('Z4');
    expect(targetLabel({ intensityTargetType: IntensityTargetType.Pace, targetPaceSecondsPerKmMin: 270, targetPaceSecondsPerKmMax: 290 })).toBe('4:30–4:50 /km');
    expect(targetLabel({ intensityTargetType: IntensityTargetType.Free })).toBeNull();
  });
});

describe('summarizeStructure', () => {
  it('puts the whole structure on one line, blocks in parentheses', () => {
    const summary = summarizeStructure(
      [
        { order: 1, type: WorkoutSegmentType.WarmUp, durationSeconds: 600, intensityTargetType: IntensityTargetType.HeartRateZone, targetHeartRateZoneNumber: 2 },
        {
          ...block(3, [
            { order: 1, type: WorkoutSegmentType.Interval, durationSeconds: 180, intensityTargetType: IntensityTargetType.HeartRateZone, targetHeartRateZoneNumber: 4 },
            { order: 2, type: WorkoutSegmentType.Recovery, durationSeconds: 120 },
          ]),
          order: 2,
        },
        { order: 3, type: WorkoutSegmentType.Strides, distanceMeters: 100, repeatCount: 4 },
        { order: 4, type: WorkoutSegmentType.Main },
      ],
      i18n.t,
    );
    expect(summary).toBe('Rozklus 10:00 Z2 → 3× (Interval 3:00 Z4 + Aktivní pauza 2:00) → 4× Stupňované úseky 100 m → Hlavní část Lap');
  });
});
