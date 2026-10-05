import { Group, Stack, Text, Tooltip } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import { BELOW_COLOR, UNSPECIFIED_FILL, ZONE_COLORS } from '../activities/hrZones';
import { formatClock } from '../activities/activityFormat';

interface Segment {
  key: string;
  label: string;
  seconds: number;
  fill: string;
}

/** One 100 % stacked bar (part-to-whole, ≤ 9 segments), 2 px surface gaps between segments. */
function StackedRow({ label, segments, height }: { label: string; segments: Segment[]; height: number }) {
  const total = segments.reduce((a, s) => a + s.seconds, 0);
  const tooltip = segments
    .filter((s) => s.seconds > 0)
    .map((s) => `${s.label}: ${formatClock(s.seconds)} (${Math.round((s.seconds / total) * 100)} %)`)
    .join(' · ');
  return (
    <Group gap={6} wrap="nowrap">
      <Text className="ds-metadata" w={64} style={{ flexShrink: 0 }}>
        {label}
      </Text>
      <Tooltip label={total > 0 ? tooltip : '—'} multiline maw={360} openDelay={80}>
        <div style={{ display: 'flex', gap: 2, flex: 1, height, borderRadius: 3, overflow: 'hidden', minWidth: 0 }}>
          {total === 0 ? (
            <div style={{ flex: 1, background: 'rgba(255,255,255,0.06)' }} />
          ) : (
            segments
              .filter((s) => s.seconds > 0)
              .map((s) => <div key={s.key} style={{ flexGrow: s.seconds, flexBasis: 0, minWidth: 2, background: s.fill }} />)
          )}
        </div>
      </Tooltip>
    </Group>
  );
}

/**
 * Planned vs actual time in heart rate zones as two aligned 100 % bars — same zone colors as
 * everywhere else; planned time without a zone target is hatched gray, actual time below zone 1
 * solid gray. Exact times on hover.
 */
export function ZoneCompareBars({
  plannedZones,
  plannedUnspecified,
  actualZones,
  actualBelow,
  height = 6,
  showPlan = true,
}: {
  plannedZones: readonly number[];
  plannedUnspecified: number;
  actualZones: readonly number[] | null;
  actualBelow: number;
  height?: number;
  /** False when the plan has no zone targets — an empty "plan" bar would only add noise. */
  showPlan?: boolean;
}) {
  const { t } = useTranslation();
  const zone = (i: number) => `Z${i + 1}`;
  const planned: Segment[] = [
    ...plannedZones.map((s, i) => ({ key: `z${i}`, label: zone(i), seconds: s, fill: ZONE_COLORS[i] })),
    { key: 'none', label: t('calendar.compare.noTarget'), seconds: plannedUnspecified, fill: UNSPECIFIED_FILL },
  ];
  const actual: Segment[] = actualZones
    ? [
        { key: 'below', label: t('activity.hrZones.below'), seconds: actualBelow, fill: BELOW_COLOR },
        ...actualZones.map((s, i) => ({ key: `z${i}`, label: zone(i), seconds: s, fill: ZONE_COLORS[i] })),
      ]
    : [];
  return (
    <Stack gap={3}>
      {showPlan && <StackedRow label={t('calendar.compare.plan')} segments={planned} height={height} />}
      {actualZones && <StackedRow label={t('calendar.compare.actual')} segments={actual} height={height} />}
    </Stack>
  );
}
