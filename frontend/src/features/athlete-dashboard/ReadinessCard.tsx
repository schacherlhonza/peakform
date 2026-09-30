import { useTranslation } from 'react-i18next';
import { Progress, Text } from '@mantine/core';
import { IconAlertTriangle, IconGauge } from '@tabler/icons-react';
import { Panel, CardHeader, EmptyState, Skeleton, Button, Badge } from '../../design-system/components';
import { ReadinessFactor, ReadinessScoreSource, type ReadinessComponentDto, type ReadinessDto } from '../../api/generated/models';
import { HealthStatusSection } from './HealthStatusSection';
import classes from './ReadinessCard.module.css';

export interface ReadinessCardData {
  readiness: ReadinessDto | null;
  lastSyncedAtUtc: string | null;
  isLoading: boolean;
  isError: boolean;
  refetch: () => void;
}

type Tone = 'positive' | 'warning' | 'danger';

function toneFor(score: number): Tone {
  if (score >= 75) return 'positive';
  if (score >= 50) return 'warning';
  return 'danger';
}

const TONE_COLOR: Record<Tone, string> = {
  positive: 'var(--color-accent)',
  warning: 'var(--color-warning)',
  danger: 'var(--color-danger)',
};

const TONE_LABEL_KEY: Record<Tone, string> = {
  positive: 'dashboard.readinessGood',
  warning: 'dashboard.readinessModerate',
  danger: 'dashboard.readinessLow',
};

/** Readiness factors that aren't overnight body metrics — HRV, resting HR and sleep are shown
 * (with their contribution) in the health-status section instead. */
const OTHER_FACTORS: ReadinessFactor[] = [ReadinessFactor.TrainingLoad, ReadinessFactor.Subjective];

const num = (value: number, digits = 0) => value.toLocaleString('cs-CZ', { maximumFractionDigits: digits });
const signed = (value: number, digits = 0) => `${value > 0 ? '+' : value < 0 ? '−' : '±'}${num(Math.abs(value), digits)}`;

function factorDetail(factor: ReadinessFactor, r: ReadinessDto, t: (key: string, options?: Record<string, unknown>) => string): string | null {
  if (factor === ReadinessFactor.TrainingLoad && r.ctl != null && r.atl != null) {
    return t('dashboard.readinessDetail.form', { form: signed(r.ctl - r.atl), ctl: num(r.ctl), atl: num(r.atl) });
  }
  return null;
}

function FactorRow({ factor, component, detail }: { factor: ReadinessFactor; component?: ReadinessComponentDto; detail: string | null }) {
  const { t } = useTranslation();
  const subScore = component?.subScore ?? null;
  const color = subScore != null ? TONE_COLOR[toneFor(subScore)] : 'var(--color-text-subtle)';

  return (
    <div className={classes.factor}>
      <div className={classes.factorHead}>
        <Text fz={13} fw={700} c="var(--color-text)">
          {t(`dashboard.readinessFactor.${factor}`)}
        </Text>
        <Text fz={13} fw={700} c={color} className={classes.factorScore}>
          {subScore ?? '—'}
        </Text>
      </div>
      <Progress value={subScore ?? 0} size={6} radius="xl" color={color} className={classes.factorBar} aria-hidden />
      <Text className="ds-metadata">{detail ?? t('dashboard.readinessDetail.checkIn')}</Text>
    </div>
  );
}

/**
 * Dominant readiness card — see docs/DESIGN_SYSTEM.md §7. Score is never fabricated: a provider's
 * own score is shown when it has one, otherwise PeakForm's estimate from the individual signals
 * (backend ReadinessCalculator), always with the per-factor breakdown so it stays explainable.
 */
export function ReadinessCard({ data }: { data: ReadinessCardData }) {
  const { t } = useTranslation();

  if (data.isLoading) {
    return (
      <Panel className={classes.panel}>
        <Skeleton height={16} width={120} mb="md" />
        <Skeleton height={150} circle mb="md" />
        <Skeleton height={40} mb={8} />
        <Skeleton height={40} mb={8} />
        <Skeleton height={40} />
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

  const r = data.readiness;
  if (!r?.date) {
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

  const score = r.score ?? null;
  const tone = score != null ? toneFor(score) : null;
  const color = tone ? TONE_COLOR[tone] : 'var(--color-text-subtle)';
  const components = r.components ?? [];
  const rows = OTHER_FACTORS.map((factor) => ({
    factor,
    component: components.find((c) => c.factor === factor),
    detail: factorDetail(factor, r, t),
  })).filter((row) => row.component || row.detail);

  return (
    <Panel className={classes.panel}>
      <CardHeader
        kicker={t('dashboard.readiness')}
        right={
          <Badge tone={r.isToday ? 'positive' : 'neutral'}>
            {r.isToday
              ? t('dashboard.readinessFresh')
              : t('dashboard.readinessStale', {
                  date: new Date(r.date).toLocaleDateString('cs-CZ'),
                })}
          </Badge>
        }
      />

      <div className={classes.hero}>
        <div
          className={classes.ring}
          style={{
            background: `conic-gradient(${color} ${(score ?? 0) * 3.6}deg, var(--color-surface-inset) 0deg)`,
          }}
          role="img"
          aria-label={score != null ? t('dashboard.readinessScoreLabel', { score }) : t('dashboard.readinessNoScore')}
        >
          <div className={classes.ringInner}>
            <Text className="ds-key-metric" fz={42}>
              {score ?? '—'}
            </Text>
            <Text className="ds-metadata">/ 100</Text>
          </div>
        </div>

        <div className={classes.heroText}>
          {tone ? (
            <Text fw={700} fz={20} c={color}>
              {t(TONE_LABEL_KEY[tone])}
            </Text>
          ) : (
            <Text fw={700} fz={16} c="var(--color-text)">
              {t('dashboard.readinessNoScore')}
            </Text>
          )}
          <Text className="ds-body">
            {score == null
              ? t('dashboard.readinessNotEnoughData')
              : r.scoreSource === ReadinessScoreSource.Vendor
                ? t('dashboard.readinessSourceVendor')
                : t('dashboard.readinessSourceComputed', {
                    count: components.length,
                  })}
          </Text>
          {r.hasPainOrIllness && (
            <Badge tone="danger" icon={<IconAlertTriangle size={12} />}>
              {t('dashboard.readinessPainFlag')}
            </Badge>
          )}
        </div>
      </div>

      <HealthStatusSection items={r.healthStatus ?? []} components={components} />

      {rows.length > 0 && (
        <div className={classes.factors}>
          <Text className="ds-eyebrow">{t('dashboard.readinessOtherFactorsTitle')}</Text>
          {rows.map((row) => (
            <FactorRow key={row.factor} {...row} />
          ))}
        </div>
      )}

      <Text className="ds-metadata" mt="auto" pt="sm">
        {t('dashboard.readinessExplain')}
        {data.lastSyncedAtUtc && ` ${t('integrations.lastSynced')}: ${new Date(data.lastSyncedAtUtc).toLocaleString('cs-CZ')}.`}
      </Text>
    </Panel>
  );
}
