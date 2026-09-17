import { useMemo, useState, type ReactNode } from 'react';
import { AreaChart, LineChart } from '@mantine/charts';
import { Group, Text } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { Panel, CardHeader, IconButton } from '../design-system/components';
import { formatClock } from './activityFormat';

const MAX_POINTS = 300;

interface StreamPoint {
  t: number;
  v: number;
}

export interface StreamChartProps {
  title: string;
  explanation: string;
  unit: string;
  color: string;
  kind: 'line' | 'area';
  times: readonly number[];
  values: readonly (number | null | undefined)[];
  formatValue?: (v: number) => string;
  /** Flips the y-axis so a lower value reads higher — for metrics like pace where "better" means smaller. */
  reverseYAxis?: boolean;
}

/** Zooms the y-axis to the data's own min/max (+10% padding) instead of always starting at 0 —
 * a workout's heart rate or elevation usually moves within a narrow band, and anchoring at 0
 * flattens that band into a barely-visible line. Values here can't go negative, so the padded
 * floor is clamped at 0. */
function computeYDomain(values: number[]): [number, number] {
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min;
  const padding = range > 0 ? range * 0.1 : Math.max(max * 0.1, 1);
  return [Math.max(0, Math.floor(min - padding)), Math.ceil(max + padding)];
}

/** Simple stride-based downsampling — a hard cap on point count keeps a ~1h activity's
 * ~3600 raw samples from turning into an unreadably dense/slow SVG line. */
function buildPoints(times: readonly number[], values: readonly (number | null | undefined)[]): StreamPoint[] {
  const points: StreamPoint[] = [];
  const len = Math.min(times.length, values.length);
  for (let i = 0; i < len; i++) {
    const v = values[i];
    if (v == null || !Number.isFinite(v)) continue;
    points.push({ t: times[i], v });
  }
  if (points.length <= MAX_POINTS) return points;
  const stride = Math.ceil(points.length / MAX_POINTS);
  return points.filter((_, i) => i % stride === 0);
}

/**
 * One metric's time-series chart — single series only (never overlaid with other metrics,
 * which have incompatible units/scales; see docs/DESIGN_SYSTEM.md §8). Ships with a visible
 * min/avg/max summary line (textual fallback) and a toggleable one-line explanation of what
 * the metric means, per the user's request for "vysvětlivky" alongside every chart.
 */
export function StreamChart({ title, explanation, unit, color, kind, times, values, formatValue, reverseYAxis }: StreamChartProps) {
  const [showInfo, setShowInfo] = useState(false);
  const points = useMemo(() => buildPoints(times, values), [times, values]);

  if (points.length === 0) return null;

  const raw = points.map((p) => p.v);
  const min = Math.min(...raw);
  const max = Math.max(...raw);
  const avg = raw.reduce((sum, v) => sum + v, 0) / raw.length;
  const fmt = formatValue ?? ((v: number) => `${Math.round(v)} ${unit}`);
  const yDomain = computeYDomain(raw);

  const data = points.map((p) => ({ t: p.t, v: p.v }));
  const series = [{ name: 'v', color, label: title }];
  const chartProps = {
    h: 200,
    data,
    dataKey: 't',
    series,
    withLegend: false,
    withDots: false,
    gridColor: 'rgba(255,255,255,.07)',
    textColor: 'var(--color-text-muted)',
    strokeWidth: 2,
    xAxisProps: { tickFormatter: formatClock },
    yAxisProps: { domain: yDomain, reversed: reverseYAxis, tickFormatter: fmt, width: 64 },
    tooltipProps: { labelFormatter: (label: ReactNode) => formatClock(Number(label)) },
    valueFormatter: fmt,
  };

  return (
    <Panel>
      <CardHeader
        kicker={title}
        right={
          <IconButton
            icon={<IconInfoCircle size={16} />}
            label={explanation}
            onClick={() => setShowInfo((v) => !v)}
          />
        }
      />
      {showInfo && (
        <Text className="ds-body" mb="sm">
          {explanation}
        </Text>
      )}
      {kind === 'area' ? (
        <AreaChart {...chartProps} curveType="natural" fillOpacity={0.16} />
      ) : (
        <LineChart {...chartProps} curveType="monotone" />
      )}
      <Group gap="md" mt="xs">
        <Text className="ds-metadata">min {fmt(min)}</Text>
        <Text className="ds-metadata">průměr {fmt(avg)}</Text>
        <Text className="ds-metadata">max {fmt(max)}</Text>
      </Group>
    </Panel>
  );
}
