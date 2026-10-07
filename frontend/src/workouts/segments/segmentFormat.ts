import type { TFunction } from 'i18next';
import { IntensityTargetType, WorkoutSegmentType, type WorkoutSegmentDto } from '../../api/generated/models';
import { formatClock, formatPace } from '../../activities/activityFormat';

/** Garmin Connect rejects workouts with more steps (newer devices take 100; we stay with the common limit). */
export const GARMIN_MAX_STEPS = 50;

/** "5" → 300 s (minutes), "5:30" → 330 s, "1:05:00" → 3900 s. Null for empty or unparseable input. */
export function parseClock(input: string): number | null {
  const text = input.trim();
  if (!text) return null;
  const parts = text.split(':');
  if (parts.length > 3 || parts.some((p) => !/^\d+$/.test(p))) return null;
  const nums = parts.map(Number);
  if (nums.length === 1) return nums[0] * 60;
  if (nums.slice(1).some((n) => n >= 60)) return null;
  return nums.reduce((total, n) => total * 60 + n, 0);
}

export const isBlock = (s: WorkoutSegmentDto) => s.type === WorkoutSegmentType.Repeat;

function repeats(s: WorkoutSegmentDto): number {
  return Math.max(1, s.repeatCount ?? 1);
}

/** How a step ends — Garmin steps have exactly one end condition; no length means "until lap press". */
export type EndCondition = 'time' | 'distance' | 'lap';

export function endCondition(s: WorkoutSegmentDto): EndCondition {
  if (s.durationSeconds) return 'time';
  if (s.distanceMeters) return 'distance';
  return 'lap';
}

/** Planned totals of a whole structure (length × repeats, block steps × block repeats). Null when nothing sets that dimension. */
export function segmentTotals(segments: readonly WorkoutSegmentDto[]): { durationSeconds: number | null; distanceMeters: number | null } {
  let duration: number | null = null;
  let distance: number | null = null;
  for (const s of segments) {
    const inner = isBlock(s) ? segmentTotals(s.steps ?? []) : { durationSeconds: s.durationSeconds ?? null, distanceMeters: s.distanceMeters ?? null };
    if (inner.durationSeconds) duration = (duration ?? 0) + inner.durationSeconds * repeats(s);
    if (inner.distanceMeters) distance = (distance ?? 0) + inner.distanceMeters * repeats(s);
  }
  return { durationSeconds: duration, distanceMeters: distance };
}

/** Steps as Garmin counts them: a repeat is one step plus its children, not unrolled. */
export function garminStepCount(segments: readonly WorkoutSegmentDto[]): number {
  return segments.reduce((n, s) => n + (isBlock(s) ? 1 + (s.steps?.length ?? 0) : repeats(s) > 1 ? 2 : 1), 0);
}

export function formatSegmentDistance(meters: number): string {
  return meters >= 1000 ? `${(meters / 1000).toLocaleString('cs-CZ', { maximumFractionDigits: 2 })} km` : `${meters} m`;
}

/** "Z2", "4:30–4:50 /km", "RPE 6", "250 W" — null for a free segment or a target without a value. */
export function targetLabel(s: WorkoutSegmentDto): string | null {
  switch (s.intensityTargetType) {
    case IntensityTargetType.HeartRateZone:
      return s.targetHeartRateZoneNumber ? `Z${s.targetHeartRateZoneNumber}` : null;
    case IntensityTargetType.Pace: {
      const { targetPaceSecondsPerKmMin: min, targetPaceSecondsPerKmMax: max } = s;
      if (min && max && min !== max) return `${formatClock(min)}–${formatPace(max)}`;
      return min || max ? formatPace((min || max)!) : null;
    }
    case IntensityTargetType.Rpe:
      return s.targetRpe ? `RPE ${s.targetRpe}` : null;
    case IntensityTargetType.Power:
      return s.targetPowerWatts ? `${s.targetPowerWatts} W` : null;
    default:
      return null;
  }
}

/** One-line description: "6× 5:00 Interval · Z4", a block as "6× blok". */
export function describeSegment(s: WorkoutSegmentDto, t: TFunction): string {
  if (isBlock(s)) return t('workout.blockLabel', { count: repeats(s) });
  const length = { time: () => formatClock(s.durationSeconds!), distance: () => formatSegmentDistance(s.distanceMeters!), lap: () => t('workout.untilLap') }[endCondition(s)]();
  const head = [repeats(s) > 1 ? `${repeats(s)}×` : null, length, t(`segmentType.${s.type}`)].filter(Boolean).join(' ');
  const target = targetLabel(s);
  return target ? `${head} · ${target}` : head;
}

function normalizeStep(s: WorkoutSegmentDto, order: number, inBlock: boolean): WorkoutSegmentDto {
  const type = s.intensityTargetType ?? IntensityTargetType.Free;
  const paces = [s.targetPaceSecondsPerKmMin, s.targetPaceSecondsPerKmMax].filter((p): p is number => !!p).sort((a, b) => a - b);
  return {
    ...s,
    order,
    // A plain step can't contain steps; a stray "Repeat" type without steps becomes a main step.
    type: s.type === WorkoutSegmentType.Repeat ? WorkoutSegmentType.Main : s.type,
    steps: null,
    repeatCount: !inBlock && s.repeatCount && s.repeatCount > 1 ? s.repeatCount : null,
    distanceMeters: s.durationSeconds ? null : (s.distanceMeters ?? null),
    intensityTargetType: type,
    targetHeartRateZoneId: type === IntensityTargetType.HeartRateZone ? s.targetHeartRateZoneId : null,
    targetHeartRateZoneNumber: type === IntensityTargetType.HeartRateZone ? s.targetHeartRateZoneNumber : null,
    targetPaceSecondsPerKmMin: type === IntensityTargetType.Pace ? (paces[0] ?? null) : null,
    targetPaceSecondsPerKmMax: type === IntensityTargetType.Pace ? (paces.at(-1) ?? null) : null,
    targetRpe: type === IntensityTargetType.Rpe ? s.targetRpe : null,
    targetPowerWatts: type === IntensityTargetType.Power ? s.targetPowerWatts : null,
  };
}

/**
 * Shapes the editor's list for saving: orders follow position (per level), a step ends by time or distance
 * (time wins), only fields of the chosen target type are kept, and a block carries just its repeat count
 * and steps. Empty blocks are dropped.
 */
export function normalizeSegments(segments: readonly WorkoutSegmentDto[]): WorkoutSegmentDto[] {
  return segments
    .filter((s) => !isBlock(s) || (s.steps?.length ?? 0) > 0)
    .map((s, index) =>
      isBlock(s)
        ? {
            id: s.id,
            order: index + 1,
            type: WorkoutSegmentType.Repeat,
            repeatCount: Math.max(2, s.repeatCount ?? 2),
            intensityTargetType: IntensityTargetType.Free,
            notes: s.notes ?? null,
            steps: (s.steps ?? []).map((step, i) => normalizeStep(step, i + 1, true)),
          }
        : normalizeStep(s, index + 1, false),
    );
}
