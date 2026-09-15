import { Text } from '@mantine/core';
import { useTranslation } from 'react-i18next';

interface SparklinePoint {
  date: string;
  value: number;
}

interface SparklineProps {
  data: SparklinePoint[];
  color?: string;
  height?: number;
  unit?: string;
}

const WIDTH = 320;

/**
 * Minimal dependency-free inline SVG line chart. No charting library is
 * installed in this project, so trend data is rendered as a simple sparkline
 * with a subtle filled area, a few gridlines, and value labels at the ends.
 */
export function Sparkline({ data, color = 'var(--mantine-color-brand-6)', height = 80, unit }: SparklineProps) {
  const { t } = useTranslation();
  const points = data.filter((d) => Number.isFinite(d.value));

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

  return (
    <div>
      <svg viewBox={`0 0 ${WIDTH} ${height}`} width="100%" height={height} preserveAspectRatio="none" role="img">
        <path d={areaPath} fill={color} opacity={0.12} />
        <path d={linePath} fill="none" stroke={color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
        {coords.map((c) => (
          <circle key={c.date} cx={c.x} cy={c.y} r={2} fill={color} />
        ))}
      </svg>
      <Text size="xs" c="dimmed" ta="right">
        {t('wellness.latestValue', { value: last.value, unit: unit ?? '' })}
        {first.date !== last.date && ` · min ${min}${unit ?? ''} / max ${max}${unit ?? ''}`}
      </Text>
    </div>
  );
}
