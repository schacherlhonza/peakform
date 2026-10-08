import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, RingProgress, Stack, Text } from '@mantine/core';
import { IconBed, IconHeart, IconRun } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, EmptyState, Skeleton, Button } from '../../design-system/components';
import type { CompletedActivityDto, PlannedWorkoutDto, WorkoutComparisonDto } from '../../api/generated/models';
import { formatClock, formatPace } from '../../activities/activityFormat';
import { sportIcon } from '../../activities/sportIcon';
import { ZoneCompareBars } from '../../calendar/ZoneCompareBars';
import { formatSegmentDistance, segmentTotals, summarizeStructure } from '../../workouts/segments/segmentFormat';
import classes from './TodayWorkoutCard.module.css';

export interface TodayWorkoutCardData {
  workout: PlannedWorkoutDto | null;
  /** Today's workout paired with the activity that fulfilled it (plan vs actual). */
  comparison: WorkoutComparisonDto | null;
  /** Every activity started today. */
  activities: CompletedActivityDto[];
  isLoading: boolean;
  isError: boolean;
  hasPlan: boolean;
}

/** On target within ±10 % (a bit over is fine), short of it orange. */
function fulfilmentColor(percent: number): string {
  if (percent >= 90 && percent <= 120) return 'var(--color-accent)';
  return 'var(--color-warning)';
}

/** One "actual of planned" ring — the share fills the ring, the numbers sit inside. */
function PlanRing({ label, actual, planned, format }: { label: string; actual: number | null; planned: number; format: (v: number) => string }) {
  const { t } = useTranslation();
  const percent = actual != null ? Math.round((actual / planned) * 100) : 0;
  const color = actual != null ? fulfilmentColor(percent) : 'var(--color-text-subtle)';
  return (
    <div className={classes.ring}>
      <RingProgress
        size={128}
        thickness={9}
        roundCaps
        rootColor="rgba(255, 255, 255, 0.08)"
        sections={actual != null ? [{ value: Math.min(100, percent), color }] : []}
        label={
          <Stack gap={0} align="center">
            <Text fw={800} fz={17} lh={1.1}>
              {actual != null ? format(actual) : '—'}
            </Text>
            <Text className="ds-metadata">{t('todayCard.ofPlanned', { planned: format(planned) })}</Text>
          </Stack>
        }
      />
      <Text className="ds-eyebrow">{label}</Text>
      {actual != null && (
        <Text fz={13} fw={800} c={color}>
          {percent} %
        </Text>
      )}
    </div>
  );
}

function PlanVsActual({ workout, comparison }: { workout: PlannedWorkoutDto; comparison: WorkoutComparisonDto | null }) {
  const { t } = useTranslation();
  const fromStructure = segmentTotals(workout.segments ?? []);
  const plannedDuration = workout.plannedDurationSeconds ?? fromStructure.durationSeconds;
  const plannedDistance = workout.plannedDistanceMeters ?? fromStructure.distanceMeters;
  const actual = comparison?.actual ?? null;

  const plannedZones = comparison?.plannedZoneSeconds ?? [];
  const hasZonePlan = plannedZones.some((s) => s > 0);
  const actualZones = actual?.zoneSeconds?.some((s) => s > 0) ? actual.zoneSeconds! : null;

  if (!plannedDuration && !plannedDistance && !hasZonePlan && !actualZones) return null;

  return (
    <section className={classes.section}>
      <Text className="ds-eyebrow">{t('todayCard.planVsActual')}</Text>
      <div className={classes.rings}>
        {plannedDuration ? (
          <PlanRing label={t('todayCard.time')} planned={plannedDuration} actual={actual?.durationSeconds ?? null} format={formatClock} />
        ) : null}
        {plannedDistance ? (
          <PlanRing
            label={t('todayCard.distance')}
            planned={plannedDistance}
            actual={actual?.distanceMeters ?? null}
            format={(m) => formatSegmentDistance(Math.round(m))}
          />
        ) : null}
      </div>
      {(hasZonePlan || actualZones) && (
        <div className={classes.zones}>
          <Text className="ds-metadata" mb={4}>
            {t('todayCard.zones')}
          </Text>
          <ZoneCompareBars
            plannedZones={plannedZones}
            plannedUnspecified={comparison?.plannedUnspecifiedSeconds ?? 0}
            actualZones={actualZones}
            actualBelow={actual?.belowZonesSeconds ?? 0}
            height={10}
            showPlan={hasZonePlan}
          />
        </div>
      )}
      {!actual && <Text className="ds-metadata">{t('todayCard.waitingForActivity')}</Text>}
    </section>
  );
}

function TodayActivities({ activities, matchedId }: { activities: CompletedActivityDto[]; matchedId: string | null }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <section className={classes.section}>
      <Text className="ds-eyebrow">{t('todayCard.activities')}</Text>
      {activities.length === 0 ? (
        <Text className="ds-metadata">{t('todayCard.noActivities')}</Text>
      ) : (
        <Stack gap={6}>
          {activities.map((a) => {
            const stats = [
              formatClock(a.durationSeconds ?? 0),
              a.distanceMeters ? formatSegmentDistance(Math.round(a.distanceMeters)) : null,
              a.averagePaceSecondsPerKm ? formatPace(a.averagePaceSecondsPerKm) : null,
            ].filter(Boolean);
            return (
              <button key={a.id} type="button" className={classes.activity} onClick={() => a.id && navigate(`/activities/${a.id}`)}>
                <span className={classes.activityIcon}>{sportIcon(a.sport, 18)}</span>
                <span className={classes.activityMain}>
                  <Text fz={14} fw={700} truncate>
                    {a.title || t(`sport.${a.sport}`)}
                  </Text>
                  <Text className="ds-metadata" truncate>
                    {a.startedAtUtc ? new Date(a.startedAtUtc).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' }) : ''} · {stats.join(' · ')}
                  </Text>
                </span>
                {a.averageHeartRateBpm != null && (
                  <span className={classes.hr}>
                    <IconHeart size={13} aria-hidden />
                    {a.averageHeartRateBpm}
                  </span>
                )}
                <Badge tone={a.id === matchedId ? 'positive' : 'neutral'}>{t(a.id === matchedId ? 'todayCard.fulfilsPlan' : 'todayCard.unplanned')}</Badge>
              </button>
            );
          })}
        </Stack>
      )}
    </section>
  );
}

function Shell({ right, children, footer, activities, matchedId }: {
  right?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  activities: CompletedActivityDto[];
  matchedId: string | null;
}) {
  const { t } = useTranslation();
  return (
    <Panel className={classes.card}>
      <CardHeader kicker={t('dashboard.todayWorkout')} right={right} />
      {children}
      <TodayActivities activities={activities} matchedId={matchedId} />
      {footer}
    </Panel>
  );
}

/**
 * Today at a glance: the planned workout with its structure, plan vs reality as fulfilment rings and
 * time in zones, and every activity of the day (marked whether it fulfils the plan).
 */
export function TodayWorkoutCard({ data }: { data: TodayWorkoutCardData }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const matchedId = data.comparison?.actual?.activityId ?? null;

  if (data.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={120} mb="md" />
        <Skeleton height={80} />
      </Panel>
    );
  }

  if (data.isError) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.todayWorkout')} />
        <EmptyState icon={<IconRun size={28} stroke={1.6} />} title={t('common.error')} description={t('common.unknownError')} />
      </Panel>
    );
  }

  const workout = data.workout;

  if (!data.hasPlan || !workout) {
    return (
      <Shell activities={data.activities} matchedId={matchedId}>
        <EmptyState
          icon={<IconRun size={28} stroke={1.6} />}
          title={t(data.hasPlan ? 'dashboard.todayWorkoutEmptyTitle' : 'dashboard.todayWorkoutNoPlanTitle')}
          description={t(data.hasPlan ? 'dashboard.todayWorkoutEmptyDescription' : 'dashboard.todayWorkoutNoPlanDescription')}
        />
      </Shell>
    );
  }

  if (workout.isRestDay) {
    return (
      <Shell right={<Badge tone="info">{t('calendar.restDay')}</Badge>} activities={data.activities} matchedId={matchedId}>
        <Group gap="sm" align="center">
          <IconBed size={28} stroke={1.6} color="var(--color-info)" />
          <Text className="ds-body">{t('dashboard.todayWorkoutRest')}</Text>
        </Group>
      </Shell>
    );
  }

  const fromStructure = segmentTotals(workout.segments ?? []);
  const duration = workout.plannedDurationSeconds ?? fromStructure.durationSeconds;
  const distance = workout.plannedDistanceMeters ?? fromStructure.distanceMeters;
  const dose = [distance ? formatSegmentDistance(distance) : null, duration ? formatClock(duration) : null].filter(Boolean).join(' · ');
  const segments = workout.segments ?? [];

  return (
    <Shell
      right={<Badge tone="info">{workout.sport ? t(`sport.${workout.sport}`) : ''}</Badge>}
      activities={data.activities}
      matchedId={matchedId}
      footer={
        <Button variant="default" mt="md" fullWidth onClick={() => workout.id && navigate(`/workouts/${workout.id}`)} disabled={!workout.id}>
          {t('dashboard.openWorkout')}
        </Button>
      }
    >
      <Group align="baseline" gap="sm" wrap="wrap" justify="space-between">
        <Group gap={8} wrap="nowrap" style={{ minWidth: 0 }}>
          <span className={classes.titleIcon}>{sportIcon(workout.sport, 22)}</span>
          <Text className="ds-card-headline" truncate>
            {workout.title}
          </Text>
        </Group>
        {dose && (
          <Text className="ds-key-metric" fz={22} c="var(--color-accent)">
            {dose}
          </Text>
        )}
      </Group>
      {workout.coachDescription && (
        <Text className="ds-body" mt={6} lineClamp={3}>
          {workout.coachDescription}
        </Text>
      )}
      {segments.length > 0 && <div className={classes.structure}>{summarizeStructure(segments, t)}</div>}

      <PlanVsActual workout={workout} comparison={data.comparison} />
    </Shell>
  );
}
