import { ActivityMetricType, type ActivityMetricDto } from '../api/generated/models';

export const ZONE_METRICS: ActivityMetricType[] = [
  ActivityMetricType.TimeInHrZone1,
  ActivityMetricType.TimeInHrZone2,
  ActivityMetricType.TimeInHrZone3,
  ActivityMetricType.TimeInHrZone4,
  ActivityMetricType.TimeInHrZone5,
  ActivityMetricType.TimeInHrZone6,
  ActivityMetricType.TimeInHrZone7,
];

export interface ZoneSeconds {
  /** Index 0 = zone 1. */
  zones: number[];
  /** Heart rate below the lowest zone — in no zone (the backend never folds it into zone 1). */
  below: number;
  total: number;
}

/** Seconds per zone summed over the given activities' metrics. */
export function sumZoneSeconds(metricLists: readonly (readonly ActivityMetricDto[] | null | undefined)[]): ZoneSeconds {
  const zones = ZONE_METRICS.map(() => 0);
  let below = 0;
  for (const metrics of metricLists) {
    for (const m of metrics ?? []) {
      const index = ZONE_METRICS.indexOf(m.type as ActivityMetricType);
      if (index >= 0) zones[index] += m.value ?? 0;
      else if (m.type === ActivityMetricType.TimeBelowHrZones) below += m.value ?? 0;
    }
  }
  return { zones, below, total: zones.reduce((a, b) => a + b, 0) + below };
}
