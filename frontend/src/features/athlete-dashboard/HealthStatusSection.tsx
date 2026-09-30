import { useTranslation } from 'react-i18next';
import { Text } from '@mantine/core';
import {
  HealthMetric,
  HealthMetricStatus,
  ReadinessFactor,
  type HealthMetricDto,
  type ReadinessComponentDto,
} from '../../api/generated/models';
import classes from './HealthStatusSection.module.css';

const num = (value: number, digits = 0) => value.toLocaleString('cs-CZ', { maximumFractionDigits: digits });
const hm = (minutes: number) => `${Math.floor(minutes / 60)}h ${Math.round(minutes % 60)}m`;

const UNIT: Partial<Record<HealthMetric, string>> = {
  [HealthMetric.Hrv]: 'ms',
  [HealthMetric.RestingHeartRate]: 'bpm',
  [HealthMetric.AvgSleepingHeartRate]: 'bpm',
  [HealthMetric.SpO2]: '%',
};

const digitsFor = (metric?: HealthMetric) => (metric === HealthMetric.SpO2 ? 1 : 0);

function formatValue(metric: HealthMetric | undefined, value: number): string {
  if (metric === HealthMetric.SleepDuration) return hm(value);
  const unit = metric ? UNIT[metric] : undefined;
  return unit ? `${num(value, digitsFor(metric))} ${unit}` : num(value);
}

function formatRange(metric: HealthMetric | undefined, low: number, high: number): string {
  if (metric === HealthMetric.SleepDuration) return `${hm(low)} – ${hm(high)}`;
  const unit = metric ? UNIT[metric] : undefined;
  const range = `${num(low, digitsFor(metric))}–${num(high, digitsFor(metric))}`;
  return unit ? `${range} ${unit}` : range;
}

/** Which readiness sub-score the health metric feeds, so its row can show that contribution. */
const FACTOR_FOR: Partial<Record<HealthMetric, ReadinessFactor>> = {
  [HealthMetric.Hrv]: ReadinessFactor.Hrv,
  [HealthMetric.RestingHeartRate]: ReadinessFactor.RestingHeartRate,
  [HealthMetric.SleepScore]: ReadinessFactor.Sleep,
};

/**
 * 270° gauge in the spirit of Garmin's health status: the highlighted arc is the athlete's normal
 * range, the dot is today's value. The scale extends 60 % of the range width past each end, so
 * an out-of-range value still lands visibly outside the highlighted arc (clamped at the ends).
 */
function RangeGauge({ value, low, high, tone }: { value: number; low: number; high: number; tone: 'info' | 'warning' }) {
  const size = 46;
  const r = 18;
  const c = size / 2;
  const span = Math.max(high - low, 0.0001);
  const min = low - span * 0.6;
  const max = high + span * 0.6;
  const toAngle = (v: number) => 135 + ((Math.min(Math.max(v, min), max) - min) / (max - min)) * 270;
  const point = (deg: number) => {
    const rad = (deg * Math.PI) / 180;
    return [c + r * Math.cos(rad), c + r * Math.sin(rad)] as const;
  };
  const arc = (from: number, to: number) => {
    const [x1, y1] = point(from);
    const [x2, y2] = point(to);
    return `M ${x1} ${y1} A ${r} ${r} 0 ${to - from > 180 ? 1 : 0} 1 ${x2} ${y2}`;
  };
  const [dx, dy] = point(toAngle(value));
  const color = tone === 'warning' ? 'var(--color-warning)' : 'var(--color-info)';

  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden className={classes.gauge}>
      <path d={arc(135, 405)} fill="none" stroke="var(--color-surface-3)" strokeWidth={4} strokeLinecap="round" />
      <path d={arc(toAngle(low), toAngle(high))} fill="none" stroke="var(--color-info)" strokeWidth={4} strokeLinecap="round" />
      <circle cx={dx} cy={dy} r={5} fill={color} stroke="var(--color-surface-2)" strokeWidth={2} />
    </svg>
  );
}

function MetricRow({ item, component }: { item: HealthMetricDto; component?: ReadinessComponentDto }) {
  const { t } = useTranslation();
  const hasRange = item.rangeLow != null && item.rangeHigh != null;
  const outOfRange = item.status === HealthMetricStatus.AboveRange || item.status === HealthMetricStatus.BelowRange;

  let subtitle: string;
  if (item.status === HealthMetricStatus.NoData) subtitle = t('dashboard.health.noDataHint');
  else if (!hasRange) subtitle = t('dashboard.health.learningRange');
  else subtitle = t('dashboard.health.range', { range: formatRange(item.metric, item.rangeLow!, item.rangeHigh!) });

  return (
    <div className={`${classes.row} ${item.isConcerning ? classes.rowConcerning : ''}`}>
      <div className={classes.rowText}>
        <Text fz={14} fw={700} c="var(--color-text)">
          {t(`dashboard.health.metric.${item.metric}`)}
        </Text>
        <Text className="ds-metadata">{subtitle}</Text>
        {outOfRange && (
          <Text fz={12} c={item.isConcerning ? 'var(--color-warning)' : 'var(--color-text-muted)'} mt={2}>
            {t(`dashboard.health.explain.${item.metric}.${item.status}`)}
          </Text>
        )}
        {component && (
          <Text className="ds-metadata" mt={2}>
            {t('dashboard.health.contribution', { score: component.subScore })}
          </Text>
        )}
      </div>
      <Text fz={16} fw={700} c="var(--color-text)" className={classes.value}>
        {item.value != null ? formatValue(item.metric, item.value) : '––'}
      </Text>
      <div className={classes.gaugeSlot}>
        {item.value != null && hasRange && (
          <RangeGauge value={item.value} low={item.rangeLow!} high={item.rangeHigh!} tone={item.isConcerning ? 'warning' : 'info'} />
        )}
      </div>
    </div>
  );
}

const GROUPS: { key: string; statuses: HealthMetricStatus[] }[] = [
  { key: 'outOfRange', statuses: [HealthMetricStatus.BelowRange, HealthMetricStatus.AboveRange] },
  { key: 'inRange', statuses: [HealthMetricStatus.InRange] },
  { key: 'noRange', statuses: [HealthMetricStatus.NoRange] },
  { key: 'noData', statuses: [HealthMetricStatus.NoData] },
];

/** Garmin-style health status: each overnight metric against the athlete's own 28-day range
 * (backend HealthStatusCalculator), grouped out-of-range first. */
export function HealthStatusSection({ items, components }: { items: HealthMetricDto[]; components: ReadinessComponentDto[] }) {
  const { t } = useTranslation();
  const outCount = items.filter((i) => i.status === HealthMetricStatus.AboveRange || i.status === HealthMetricStatus.BelowRange).length;
  const rangedCount = items.filter((i) => i.rangeLow != null && i.value != null).length;

  const headline =
    outCount > 0
      ? t('dashboard.health.outOfRangeHeadline', { count: outCount })
      : rangedCount > 0
        ? t('dashboard.health.allInRange')
        : t('dashboard.health.learningHeadline');

  const componentFor = (metric?: HealthMetric) => {
    const factor = metric ? FACTOR_FOR[metric] : undefined;
    // Sleep contributes via score when present, otherwise via duration.
    if (metric === HealthMetric.SleepDuration && !items.some((i) => i.metric === HealthMetric.SleepScore && i.value != null)) {
      return components.find((c) => c.factor === ReadinessFactor.Sleep);
    }
    return factor ? components.find((c) => c.factor === factor) : undefined;
  };

  return (
    <div className={classes.section}>
      <Text className="ds-eyebrow">{t('dashboard.health.title')}</Text>
      <Text fz={18} fw={700} c="var(--color-text)" mt={4}>
        {headline}
      </Text>
      <Text className="ds-body" mb="xs">
        {t('dashboard.health.subtitle')}
      </Text>

      {GROUPS.map((group) => {
        const groupItems = items.filter((i) => i.status && group.statuses.includes(i.status));
        if (groupItems.length === 0) return null;
        return (
          <div key={group.key} className={classes.group}>
            <Text className="ds-eyebrow">{t(`dashboard.health.group.${group.key}`)}</Text>
            {groupItems.map((item) => (
              <MetricRow key={item.metric} item={item} component={componentFor(item.metric)} />
            ))}
          </div>
        );
      })}
    </div>
  );
}
