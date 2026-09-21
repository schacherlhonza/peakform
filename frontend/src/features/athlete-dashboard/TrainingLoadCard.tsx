import { useTranslation } from 'react-i18next';
import { Text } from '@mantine/core';
import { IconTrendingUp } from '@tabler/icons-react';
import { Panel, CardHeader, MetricStrip, EmptyState, Skeleton, Button, Badge } from '../../design-system/components';
import type { BadgeTone } from '../../design-system/components';

export interface TrainingLoadCardData {
  ctl: number | null;
  atl: number | null;
  rampRate: number | null;
  date: string | null;
  isToday: boolean;
  isLoading: boolean;
  isError: boolean;
  refetch: () => void;
}

function verbalState(rampRate: number | null, t: (key: string) => string): { label: string; tone: BadgeTone } {
  if (rampRate == null) return { label: t('dashboard.trainingLoadUnknown'), tone: 'neutral' };
  if (rampRate > 5) return { label: t('dashboard.trainingLoadRising'), tone: 'warning' };
  if (rampRate < -5) return { label: t('dashboard.trainingLoadFalling'), tone: 'info' };
  return { label: t('dashboard.trainingLoadStable'), tone: 'positive' };
}

function formatNumber(value: number | null): string {
  return value != null ? value.toFixed(1) : '—';
}

/**
 * Secondary card for provider-synced training load (CTL/ATL/ramp rate) — same "never fabricate a
 * number" discipline as ReadinessCard: no data at all renders an explained empty state, not a
 * fake 0. Not every athlete/provider will have this (only intervals.icu supplies it today).
 */
export function TrainingLoadCard({ data }: { data: TrainingLoadCardData }) {
  const { t } = useTranslation();

  if (data.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={160} mb="md" />
        <Skeleton height={56} />
      </Panel>
    );
  }

  if (data.isError) {
    return (
      <Panel>
        <EmptyState
          icon={<IconTrendingUp size={28} stroke={1.6} />}
          title={t('common.error')}
          description={t('common.unknownError')}
          action={
            <Button variant="default" onClick={data.refetch}>
              {t('common.back')}
            </Button>
          }
        />
      </Panel>
    );
  }

  if (data.ctl == null && data.atl == null) {
    return (
      <Panel>
        <CardHeader kicker={t('wellness.trainingLoad')} />
        <EmptyState
          icon={<IconTrendingUp size={28} stroke={1.6} />}
          title={t('dashboard.trainingLoadEmptyTitle')}
          description={t('dashboard.trainingLoadEmptyDescription')}
        />
      </Panel>
    );
  }

  const state = verbalState(data.rampRate, t);

  return (
    <Panel>
      <CardHeader
        kicker={t('wellness.trainingLoad')}
        right={<Badge tone={data.isToday ? 'positive' : 'neutral'}>{data.isToday ? t('dashboard.readinessFresh') : t('dashboard.readinessStale', { date: data.date })}</Badge>}
      />

      <Text ta="center" fw={700} fz={16} c={`var(--color-${state.tone === 'positive' ? 'accent' : state.tone})`}>
        {state.label}
      </Text>
      <Text ta="center" className="ds-body" mt={4}>
        {t('wellness.trainingLoadExplain')}
      </Text>

      <MetricStrip
        metrics={[
          { label: 'CTL', value: formatNumber(data.ctl) },
          { label: 'ATL', value: formatNumber(data.atl) },
          { label: t('wellness.rampRate'), value: data.rampRate != null ? `${data.rampRate > 0 ? '+' : ''}${formatNumber(data.rampRate)}` : '—' },
        ]}
      />
    </Panel>
  );
}
