import { Text } from '@mantine/core';
import { useTranslation } from 'react-i18next';

interface SparklinePoint {
  date: string;
  value: number;
}

/** Semantic color per docs/DESIGN_SYSTEM.md §8: HRV/recovery = accent, sleep = info, load = warning. */
export type SparklineTone = 'accent' | 'info' | 'warning';

interface SparklineProps {
  data: SparklinePoint[];
  tone?: SparklineTone;
  height?: number;
  unit?: string;
}

const WIDTH = 320;

const toneColor: Record<SparklineTone, string> = {
  accent: 'var(--color-accent)',
  info: 'var(--color-info)',
  warning: 'var(--color-warning)',
};

/**
 * Minimal dependency-free inline SVG line chart. No charting library is
 * installed in this project, so trend data is rendered as a simple sparkline
 * with a subtle filled area, a few gridlines, and value labels at the ends.
 * `tone` maps to the app's semantic chart palette — see docs/DESIGN_SYSTEM.md §8.
 */
export function Sparkline({ data, tone = 'accent', height = 80, unit }: SparklineProps) {
  const { t } = useTranslation();
  const points = data.filter((d) => Number.isFinite(d.value));
  const color = toneColor[tone];

  if (points.length === 0) {
    return (
      <Text size="sm" c="dimmed">
        {t('wellness.noData')}
      </Text>
    );
  }

  const values = points.map((p) => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;
  const padding = 6;
  const innerHeight = height - padding * 2;

  const stepX = points.length > 1 ? (WIDTH - padding * 2) / (points.length - 1) : 0;
  const coords = points.map((p, i) => {
    const x = padding + i * stepX;
    const y = padding + innerHeight - ((p.value - min) / range) * innerHeight;
    return { x, y, value: p.value, date: p.date };
  });

  const linePath = coords.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x.toFixed(1)} ${c.y.toFixed(1)}`).join(' ');
  const areaPath = `${linePath} L ${coords[coords.length - 1].x.toFixed(1)} ${height - padding} L ${coords[0].x.toFixed(1)} ${height - padding} Z`;

  const last = coords[coords.length - 1];
  const first = coords[0];

  // Textual fallback for the chart (docs/DESIGN_SYSTEM.md §8/§10): summarizes the same
  // latest/min/max values shown visually below, exposed via <title> and aria-label so screen
  // readers get it even though the SVG itself carries no accessible text nodes.
  const rangeSummary = first.date !== last.date ? ` · min ${min}${unit ?? ''} / max ${max}${unit ?? ''}` : '';
  const summary = `${t('wellness.latestValue', { value: last.value, unit: unit ?? '' })}${rangeSummary}`;

  return (
    <div>
      <svg viewBox={`0 0 ${WIDTH} ${height}`} width="100%" height={height} preserveAspectRatio="none" role="img" aria-label={summary}>
        <title>{summary}</title>
        <path d={areaPath} fill={color} opacity={0.12} />
        <path d={linePath} fill="none" stroke={color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
        {coords.map((c) => (
          <circle key={c.date} cx={c.x} cy={c.y} r={2} fill={color} />
        ))}
      </svg>
      <Text size="xs" c="dimmed" ta="right">
        {t('wellness.latestValue', { value: last.value, unit: unit ?? '' })}
        {rangeSummary}
      </Text>
    </div>
  );
}
