import { BestEffortType } from '../api/generated/models';
import { formatClock, formatPace } from './activityFormat';

const DISTANCE_METERS: Partial<Record<BestEffortType, number>> = {
  [BestEffortType.Distance1Km]: 1000,
  [BestEffortType.Distance5Km]: 5000,
  [BestEffortType.Distance10Km]: 10000,
  [BestEffortType.DistanceHalfMarathon]: 21097.5,
  [BestEffortType.DistanceMarathon]: 42195,
};

/** Distance efforts are times (lower is better); power efforts are watts (higher is better). */
export function isDistanceEffort(type: BestEffortType): boolean {
  return type in DISTANCE_METERS;
}

export function formatEffortValue(type: BestEffortType, value: number): string {
  return isDistanceEffort(type) ? formatClock(Math.round(value)) : `${Math.round(value)} W`;
}

/** Secondary line: pace for a distance effort. */
export function formatEffortDetail(type: BestEffortType, value: number): string | null {
  const meters = DISTANCE_METERS[type];
  return meters ? formatPace((value / meters) * 1000) : null;
}

export const EFFORT_ORDER: BestEffortType[] = [
  BestEffortType.Distance1Km,
  BestEffortType.Distance5Km,
  BestEffortType.Distance10Km,
  BestEffortType.DistanceHalfMarathon,
  BestEffortType.DistanceMarathon,
  BestEffortType.Power1Min,
  BestEffortType.Power5Min,
  BestEffortType.Power20Min,
];
