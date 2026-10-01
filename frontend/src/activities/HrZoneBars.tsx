import { Group, Stack, Text, Tooltip } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import type { HeartRateZoneDto } from '../api/generated/models';
import type { ZoneSeconds } from './hrZones';
import { formatClock } from './activityFormat';

// Zones are an ordered scale, so one hue light→dark (here: dim→bright on the dark surface, Z1 → Z5),
// never categorical hues. Validated with the dataviz validator: `--ordinal --mode dark --surface #111f1b`
// (monotone lightness, visible step gaps, dimmest step 2.10:1 against the surface).
const ZONE_COLORS = ['#184f95', '#256abf', '#3987e5', '#6da7ec', '#9ec5f4', '#b7d3f6', '#cde2fb'];

/** The athlete's zone count: the highest zone that has a name set, at least 5. */
function zoneCount(seconds: number[], zones: readonly HeartRateZoneDto[] | undefined): number {
  const lastWithTime = seconds.reduce((last, s, i) => (s > 0 ? i + 1 : last), 0);
  const configured = Math.max(0, ...(zones ?? []).map((z) => z.zoneNumber ?? 0));
  return Math.max(5, lastWithTime, configured);
}

// "Below zones" sits outside the ordered scale — neutral gray, not a ramp step. Validated against
// the zone 1 blue (normal-vision ΔE 22.6, CVD ΔE 20.9); every row carries a text label and value,
// which is the relief for the dim zone 1 step's < 3:1 contrast.
const BELOW_COLOR = '#7d8984';

function ZoneRow({ label, seconds, total, color }: { label: string; seconds: number; total: number; color: string }) {
  const share = total > 0 ? seconds / total : 0;
  const value = `${formatClock(Math.round(seconds))} · ${Math.round(share * 100)} %`;
  return (
    <Tooltip label={`${label}: ${value}`} position="top-start" openDelay={80}>
      <Group gap="sm" wrap="nowrap" role="row" style={{ cursor: 'default' }}>
        <Text role="cell" className="ds-metadata" w={120} truncate>
          {label}
        </Text>
        <div style={{ flex: 1, height: 22, display: 'flex', alignItems: 'center' }}>
          <div
            style={{
              width: `${Math.max(share * 100, seconds > 0 ? 0.8 : 0)}%`,
              height: 14,
              background: color,
              borderRadius: '0 4px 4px 0',
            }}
          />
        </div>
        <Text role="cell" className="ds-metadata" w={92} ta="right" style={{ fontVariantNumeric: 'tabular-nums' }}>
          {value}
        </Text>
      </Group>
    </Tooltip>
  );
}

/**
 * Time in heart rate zones as one horizontal bar per zone (part-to-whole over an ordered scale):
 * bar length = share of the total, value + share at the bar's tip in text ink, exact time on hover.
 * Time below the lowest zone is its own first row, so the rows add up to the activity's moving time.
 */
export function HrZoneBars({ data, zones }: { data: ZoneSeconds; zones?: readonly HeartRateZoneDto[] }) {
  const { t } = useTranslation();
  if (data.total === 0) return null;

  const count = zoneCount(data.zones, zones);
  // Names from the most recent zone set (the API returns all sets; the newest wins per zone number).
  const nameByZone = new Map<number, string>();
  [...(zones ?? [])]
    .sort((a, b) => (a.effectiveFromDate ?? '').localeCompare(b.effectiveFromDate ?? ''))
    .forEach((z) => z.zoneNumber && z.name && nameByZone.set(z.zoneNumber, z.name));

  return (
    <Stack gap={6} role="table" aria-label={t('activity.hrZones.title')}>
      {data.below > 0 && <ZoneRow label={t('activity.hrZones.below')} seconds={data.below} total={data.total} color={BELOW_COLOR} />}
      {data.zones.slice(0, count).map((s, i) => (
        <ZoneRow key={i} label={nameByZone.get(i + 1) ?? `Z${i + 1}`} seconds={s} total={data.total} color={ZONE_COLORS[i]} />
      ))}
    </Stack>
  );
}
