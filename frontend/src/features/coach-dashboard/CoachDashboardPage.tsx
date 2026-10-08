import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, Stack, Text, Title } from '@mantine/core';
import { IconUserPlus, IconUsers } from '@tabler/icons-react';
import { Button, EmptyState, MetricStrip, Panel, Skeleton } from '../../design-system/components';
import { useGetApiCoachToday } from '../../api/generated/coach/coach';
import type { CoachTodayAthleteDto } from '../../api/generated/models';
import { toIsoDate } from '../../calendar/dateUtils';
import { AthleteTodayCard } from './AthleteTodayCard';
import { attentionLevel } from './coachToday';
import classes from './CoachDashboardPage.module.css';

/** Team numbers for the strip above the cards — only over athletes who share the respective data. */
function teamMetrics(athletes: CoachTodayAthleteDto[]) {
  const withWorkout = athletes.filter((a) => a.activitiesShared && (a.todayWorkouts ?? []).some((w) => !w.isRestDay));
  const trained = withWorkout.filter((a) => (a.todayWorkouts ?? []).some((w) => !w.isRestDay && w.actual));
  const scores = athletes.map((a) => a.readiness?.score).filter((s): s is number => s != null);
  return {
    trained: `${trained.length} / ${withWorkout.length}`,
    readiness: scores.length ? `${Math.round(scores.reduce((sum, s) => sum + s, 0) / scores.length)}` : '—',
    lowReadiness: scores.filter((s) => s < 50).length,
    flagged: athletes.filter((a) => (a.activeHealthFlags?.length ?? 0) > 0).length,
  };
}

/**
 * Coach dashboard: one card per athlete with today's readiness, plan and what was actually done, from a
 * single summary request (GET /api/coach/today). Athletes needing a look (health flag, low readiness) go first.
 */
export function CoachDashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const today = new Date();
  const query = useGetApiCoachToday({ date: toIsoDate(today) }, { query: { refetchInterval: 120_000 } });
  const athletes = [...(query.data?.athletes ?? [])].sort((a, b) => {
    const rank = (x: CoachTodayAthleteDto) => ({ danger: 0, warning: 1, none: 2 })[attentionLevel(x) ?? 'none'];
    return rank(a) - rank(b);
  });
  const metrics = teamMetrics(athletes);

  return (
    <Stack gap={17}>
      <Group justify="space-between" align="flex-end">
        <div>
          <Title className="ds-page-title" order={2}>
            {t('dashboard.coachTitle')}
          </Title>
          <Text className="ds-body" tt="capitalize">
            {today.toLocaleDateString('cs-CZ', { weekday: 'long', day: 'numeric', month: 'long' })}
          </Text>
        </div>
        <Button leftSection={<IconUserPlus size={16} />} onClick={() => navigate('/athletes')}>
          {t('relationships.invite')}
        </Button>
      </Group>

      {query.isLoading ? (
        <>
          <Skeleton height={86} radius="var(--radius-panel)" />
          <div className={classes.grid}>
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} height={240} radius="var(--radius-panel)" />
            ))}
          </div>
        </>
      ) : query.isError ? (
        <Panel>
          <EmptyState
            icon={<IconUsers size={28} stroke={1.6} />}
            title={t('common.error')}
            description={t('common.unknownError')}
            action={
              <Button variant="default" onClick={() => void query.refetch()}>
                {t('common.retry')}
              </Button>
            }
          />
        </Panel>
      ) : athletes.length === 0 ? (
        <Panel>
          <EmptyState
            icon={<IconUsers size={28} stroke={1.6} />}
            title={t('dashboard.noAthletes')}
            action={<Button onClick={() => navigate('/athletes')}>{t('relationships.invite')}</Button>}
          />
        </Panel>
      ) : (
        <>
          <Panel compact>
            <MetricStrip
              metrics={[
                { label: t('coachToday.metricAthletes'), value: String(athletes.length) },
                { label: t('coachToday.metricTrained'), value: metrics.trained, trend: t('coachToday.metricTrainedHint') },
                {
                  label: t('coachToday.metricReadiness'),
                  value: metrics.readiness,
                  trend: metrics.lowReadiness ? t('coachToday.metricLowReadiness', { count: metrics.lowReadiness }) : undefined,
                  trendTone: 'warning',
                },
                {
                  label: t('coachToday.metricFlags'),
                  value: String(metrics.flagged),
                  trendTone: 'danger',
                  trend: metrics.flagged ? t('coachToday.metricFlagsHint') : undefined,
                },
              ]}
            />
          </Panel>
          <div className={classes.grid}>
            {athletes.map((a) => (
              <AthleteTodayCard key={a.athleteUserId} athlete={a} />
            ))}
          </div>
        </>
      )}
    </Stack>
  );
}

export default CoachDashboardPage;
