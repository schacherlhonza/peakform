import { useTranslation } from 'react-i18next';
import { Text } from '@mantine/core';
import { IconGauge } from '@tabler/icons-react';
import { Panel, CardHeader, MetricStrip, EmptyState, Skeleton, Button, Badge } from '../../design-system/components';
import classes from './ReadinessCard.module.css';

export interface ReadinessCardData {
  score: number | null;
  date: string | null;
  isToday: boolean;
  restingHeartRateBpm: number | null;
  hrvRmssdMs: number | null;
  sleepDurationMinutes: number | null;
  lastSyncedAtUtc: string | null;
  isLoading: boolean;
  isError: boolean;
  refetch: () => void;
}

function verbalState(score: number, t: (key: string) => string): { label: string; tone: 'positive' | 'warning' | 'danger' } {
  if (score >= 75) return { label: t('dashboard.readinessGood'), tone: 'positive' };
  if (score >= 50) return { label: t('dashboard.readinessModerate'), tone: 'warning' };
  return { label: t('dashboard.readinessLow'), tone: 'danger' };
}

function ringColor(score: number): string {
  if (score >= 75) return 'var(--color-accent)';
  if (score >= 50) return 'var(--color-warning)';
  return 'var(--color-danger)';
}

function formatMinutes(minutes: number | null): string {
  if (minutes == null) return '—';
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return `${h}h ${m}m`;
}

/**
 * Dominant readiness card — see docs/DESIGN_SYSTEM.md §7. Score is never fabricated: when no
 * recovery data exists at all, this renders an explained empty state instead of a fake 0.
 */
export function ReadinessCard({ data }: { data: ReadinessCardData }) {
  const { t } = useTranslation();

  if (data.isLoading) {
    return (
      <Panel className={classes.panel}>
        <Skeleton height={16} width={120} mb="md" />
        <Skeleton height={166} circle mx="auto" mb="md" />
        <Skeleton height={56} />
      </Panel>
    );
  }

  if (data.isError) {
    return (
      <Panel className={classes.panel}>
        <EmptyState
          icon={<IconGauge size={28} stroke={1.6} />}
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

  if (data.score == null) {
    return (
      <Panel className={classes.panel}>
        <CardHeader kicker={t('dashboard.readiness')} />
        <EmptyState
          icon={<IconGauge size={28} stroke={1.6} />}
          title={t('dashboard.readinessEmptyTitle')}
          description={t('dashboard.readinessEmptyDescription')}
        />
      </Panel>
    );
  }

  const state = verbalState(data.score, t);
  const color = ringColor(data.score);

  return (
    <Panel className={classes.panel}>
      <CardHeader
        kicker={t('dashboard.readiness')}
        right={
          <Badge tone={data.isToday ? 'positive' : 'neutral'}>
            {data.isToday ? t('dashboard.readinessFresh') : t('dashboard.readinessStale', { date: data.date })}
          </Badge>
        }
      />

      <div
        className={classes.ring}
        style={{ background: `conic-gradient(${color} ${data.score * 3.6}deg, var(--color-surface-inset) 0deg)` }}
        role="img"
        aria-label={t('dashboard.readinessScoreLabel', { score: data.score })}
      >
        <div className={classes.ringInner}>
          <Text className="ds-key-metric" fz={42}>
            {data.score}
          </Text>
          <Text className="ds-metadata">/ 100</Text>
        </div>
      </div>

      <Text ta="center" fw={700} fz={16} mt="sm" c={`var(--color-${state.tone === 'positive' ? 'accent' : state.tone})`}>
        {state.label}
      </Text>
      <Text ta="center" className="ds-body" mt={4}>
        {t('dashboard.readinessExplain')}
      </Text>

      <MetricStrip
        metrics={[
          { label: t('wellness.restingHr'), value: data.restingHeartRateBpm != null ? `${data.restingHeartRateBpm}` : '—' },
          { label: t('wellness.hrv'), value: data.hrvRmssdMs != null ? `${Math.round(data.hrvRmssdMs)}` : '—' },
          { label: t('wellness.sleepDuration'), value: formatMinutes(data.sleepDurationMinutes) },
          { label: t('integrations.lastSynced'), value: data.lastSyncedAtUtc ? new Date(data.lastSyncedAtUtc).toLocaleDateString('cs-CZ') : '—' },
        ]}
      />
    </Panel>
  );
}
