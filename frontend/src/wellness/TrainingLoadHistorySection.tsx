import { useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Text } from '@mantine/core';
import { LineChart } from '@mantine/charts';
import { IconTrendingUp } from '@tabler/icons-react';
import { useGetApiAthletesAthleteUserIdTrainingLoad } from '../api/generated/training-load/training-load';
import { DataSource } from '../api/generated/models';
import { Panel, CardHeader, EmptyState, SegmentedControl, Skeleton } from '../design-system/components';

type Range = '90' | '365' | '1825' | 'all';

// Categorical slots 1 and 2 of the dataviz reference palette, dark steps — validated on the app's
// dark surface (#111f1b): all checks pass (normal-vision ΔE 31.8, CVD ΔE 26.8, contrast ≥ 3:1).
const FITNESS_COLOR = '#3987e5';
const FATIGUE_COLOR = '#d95926';
// Form is a separate chart (a signed value around zero), so it gets its own hue, slot 3.
const FORM_COLOR = '#199e70';

const MAX_POINTS = 400;

function isoDaysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

const formatDate = (value: number | string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString('cs-CZ', { day: 'numeric', month: 'numeric', year: 'numeric' });
};

/**
 * Fitness (CTL), fatigue (ATL) and form (TSB = CTL − ATL) over the athlete's whole history —
 * PeakForm's own heart-rate-based series, the only one that covers years back (intervals.icu's
 * starts when it was connected). One source for the whole range, so the line has no seams.
 */
export function TrainingLoadHistorySection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const [range, setRange] = useState<Range>('365');
  const query = useGetApiAthletesAthleteUserIdTrainingLoad(athleteUserId, {
    from: range === 'all' ? '2000-01-01' : isoDaysAgo(Number(range)),
    to: isoDaysAgo(0),
    source: DataSource.PeakForm,
  });

  const data = useMemo(() => {
    const rows = [...(query.data ?? [])]
      .filter((r) => r.ctl != null && r.atl != null)
      .sort((a, b) => (a.date ?? '').localeCompare(b.date ?? ''));
    // Long ranges: every Nth day keeps the chart light; CTL/ATL are smooth by construction.
    const stride = Math.max(1, Math.ceil(rows.length / MAX_POINTS));
    return rows
      .filter((_, i) => i % stride === 0 || i === rows.length - 1)
      .map((r) => ({
        t: new Date(r.date!).getTime(),
        ctl: Number(r.ctl),
        atl: Number(r.atl),
        tsb: Math.round((Number(r.ctl) - Number(r.atl)) * 10) / 10,
      }));
  }, [query.data]);

  const latest = data[data.length - 1];
  const tickFormatter = (ms: number) =>
    range === '90'
      ? new Date(ms).toLocaleDateString('cs-CZ', { day: 'numeric', month: 'numeric' })
      : new Date(ms).toLocaleDateString('cs-CZ', { month: 'numeric', year: '2-digit' });
  const common = {
    dataKey: 't',
    withDots: false,
    strokeWidth: 2,
    gridColor: 'rgba(255,255,255,.07)',
    textColor: 'var(--color-text-muted)',
    xAxisProps: { type: 'number' as const, scale: 'time' as const, domain: ['dataMin', 'dataMax'], tickFormatter },
    tooltipProps: { labelFormatter: (label: ReactNode) => formatDate(Number(label)) },
    valueFormatter: (v: number) => v.toFixed(1),
  };

  return (
    <Panel>
      <CardHeader
        kicker={t('wellness.loadHistory.kicker')}
        title={t('wellness.loadHistory.title')}
        right={
          <SegmentedControl
            size="xs"
            value={range}
            onChange={(v) => setRange(v as Range)}
            data={(['90', '365', '1825', 'all'] as Range[]).map((r) => ({ value: r, label: t(`wellness.loadHistory.range.${r}`) }))}
          />
        }
      />
      {query.isLoading ? (
        <Skeleton height={300} />
      ) : data.length === 0 ? (
        <EmptyState icon={<IconTrendingUp size={28} stroke={1.6} />} title={t('wellness.loadHistory.emptyTitle')} description={t('wellness.loadHistory.emptyDescription')} />
      ) : (
        <Stack gap="md">
          {latest && (
            <Group gap="lg">
              <Text className="ds-metadata">
                <span style={{ color: FITNESS_COLOR }}>●</span> {t('wellness.loadHistory.fitness')}: <b>{latest.ctl.toFixed(0)}</b>
              </Text>
              <Text className="ds-metadata">
                <span style={{ color: FATIGUE_COLOR }}>●</span> {t('wellness.loadHistory.fatigue')}: <b>{latest.atl.toFixed(0)}</b>
              </Text>
              <Text className="ds-metadata">
                <span style={{ color: FORM_COLOR }}>●</span> {t('wellness.loadHistory.form')}: <b>{latest.tsb > 0 ? '+' : ''}{latest.tsb.toFixed(0)}</b>
              </Text>
            </Group>
          )}
          <LineChart
            {...common}
            h={240}
            data={data}
            withLegend
            legendProps={{ verticalAlign: 'top', height: 28 }}
            series={[
              { name: 'ctl', color: FITNESS_COLOR, label: t('wellness.loadHistory.fitness') },
              { name: 'atl', color: FATIGUE_COLOR, label: t('wellness.loadHistory.fatigue') },
            ]}
            yAxisProps={{ width: 40 }}
          />
          <Stack gap={2}>
            <Text className="ds-eyebrow">{t('wellness.loadHistory.formTitle')}</Text>
            <LineChart
              {...common}
              h={140}
              data={data}
              withLegend={false}
              series={[{ name: 'tsb', color: FORM_COLOR, label: t('wellness.loadHistory.form') }]}
              referenceLines={[{ y: 0, color: 'var(--color-text-muted)' }]}
              yAxisProps={{ width: 40 }}
            />
          </Stack>
          <Text className="ds-metadata">{t('wellness.loadHistory.explain')}</Text>
        </Stack>
      )}
    </Panel>
  );
}
