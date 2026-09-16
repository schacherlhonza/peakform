import { Text } from '@mantine/core';
import classes from './MetricStrip.module.css';

export type MetricTone = 'positive' | 'info' | 'warning' | 'danger' | 'neutral';

export interface Metric {
  label: string;
  value: string;
  trend?: string;
  trendTone?: MetricTone;
}

export interface MetricStripProps {
  metrics: Metric[];
}

const trendColor: Record<MetricTone, string> = {
  positive: 'var(--color-success)',
  info: 'var(--color-info)',
  warning: 'var(--color-warning)',
  danger: 'var(--color-danger)',
  neutral: 'var(--color-text-muted)',
};

/** Grid of key metrics with hairline separators — see docs/DESIGN_SYSTEM.md §6. */
export function MetricStrip({ metrics }: MetricStripProps) {
  return (
    <div className={classes.strip} style={{ gridTemplateColumns: `repeat(${metrics.length}, 1fr)` }}>
      {metrics.map((metric) => (
        <div key={metric.label} className={classes.block}>
          <Text className="ds-eyebrow">{metric.label}</Text>
          <Text className="ds-key-metric" fz={21}>
            {metric.value}
          </Text>
          {metric.trend && (
            <Text className="ds-metadata" style={{ color: trendColor[metric.trendTone ?? 'neutral'] }}>
              {metric.trend}
            </Text>
          )}
        </div>
      ))}
    </div>
  );
}
