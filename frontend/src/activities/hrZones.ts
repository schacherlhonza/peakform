import { ActivityMetricType, type ActivityMetricDto } from '../api/generated/models';

// Zones are an ordered scale, so one hue light→dark (here: dim→bright on the dark surface, Z1 → Z5),
// never categorical hues. Validated with the dataviz validator: `--ordinal --mode dark --surface #111f1b`
// (monotone lightness, visible step gaps, dimmest step 2.10:1 against the surface).
export const ZONE_COLORS = ['#184f95', '#256abf', '#3987e5', '#6da7ec', '#9ec5f4', '#b7d3f6', '#cde2fb'];

// "Below zones" sits outside the ordered scale — neutral gray, not a ramp step. Validated against
// the zone 1 blue (normal-vision ΔE 22.6, CVD ΔE 20.9); every row carries a text label and value,
// which is the relief for the dim zone 1 step's < 3:1 contrast.
export const BELOW_COLOR = '#7d8984';

// Planned time without a zone target: the same gray, hatched — texture, not another hue, so it
// reads as "not specified" rather than as a seventh category.
export const UNSPECIFIED_FILL = `repeating-linear-gradient(45deg, ${BELOW_COLOR} 0 3px, transparent 3px 6px)`;

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
