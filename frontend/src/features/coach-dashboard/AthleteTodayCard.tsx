import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Group, Progress, Text, Tooltip } from '@mantine/core';
import { IconAlertTriangle, IconArrowDownRight, IconArrowUpRight, IconBed, IconCircleCheck, IconCircleDashed, IconLock } from '@tabler/icons-react';
import { Badge, type BadgeTone } from '../../design-system/components';
import type { ActualActivityDto, CoachTodayAthleteDto, WorkoutComparisonDto } from '../../api/generated/models';
import { attentionLevel } from './coachToday';
import { sportIcon } from '../../activities/sportIcon';
import { formatClock, formatTotalDuration } from '../../activities/activityFormat';
import { formatSegmentDistance, summarizeStructure } from '../../workouts/segments/segmentFormat';
import classes from './AthleteTodayCard.module.css';

type Tone = 'positive' | 'warning' | 'danger';

const readinessTone = (score: number): Tone => (score >= 75 ? 'positive' : score >= 50 ? 'warning' : 'danger');
const TONE_COLOR: Record<Tone, string> = { positive: 'var(--color-accent)', warning: 'var(--color-warning)', danger: 'var(--color-danger)' };
const TONE_LABEL: Record<Tone, string> = { positive: 'dashboard.readinessGood', warning: 'dashboard.readinessModerate', danger: 'dashboard.readinessLow' };

function complianceTone(percent: number): BadgeTone {
  if (percent >= 90 && percent <= 115) return 'positive';
  if (percent >= 70) return 'warning';
  return 'danger';
}

function Chip({ label, value, trend }: { label: string; value: string; trend?: 'better' | 'worse' }) {
  return (
    <span className={classes.chip}>
      <span className={classes.chipLabel}>{label}</span>
      <span className={classes.chipValue}>{value}</span>
      {trend === 'better' && <IconArrowUpRight size={13} color="var(--color-success)" aria-hidden />}
      {trend === 'worse' && <IconArrowDownRight size={13} color="var(--color-warning)" aria-hidden />}
    </span>
  );
}

/** Higher-is-better metrics compare up, lower-is-better (resting HR) compare down; ±3 % counts as unchanged. */
function trendOf(value: number | null | undefined, baseline: number | null | undefined, higherIsBetter: boolean): 'better' | 'worse' | undefined {
  if (value == null || baseline == null || baseline === 0) return undefined;
  const change = (value - baseline) / baseline;
  if (Math.abs(change) < 0.03) return undefined;
  return change > 0 === higherIsBetter ? 'better' : 'worse';
}

function ReadinessBlock({ athlete }: { athlete: CoachTodayAthleteDto }) {
  const { t } = useTranslation();
  if (!athlete.wellnessShared) {
    return (
      <div className={classes.readinessEmpty}>
        <IconLock size={14} aria-hidden />
        <Text className="ds-metadata">{t('coachToday.wellnessNotShared')}</Text>
      </div>
    );
  }
  const score = athlete.readiness?.score ?? null;
  const tone = score != null ? readinessTone(score) : null;
  const color = tone ? TONE_COLOR[tone] : 'var(--color-text-subtle)';
  const stale = athlete.readiness && !athlete.readiness.isToday && athlete.readiness.date;
  return (
    <div className={classes.readiness}>
      <div
        className={classes.ring}
        style={{ background: `conic-gradient(${color} ${(score ?? 0) * 3.6}deg, var(--color-surface-inset) 0deg)` }}
        role="img"
        aria-label={score != null ? t('dashboard.readinessScoreLabel', { score }) : t('dashboard.readinessNoScore')}
      >
        <div className={classes.ringInner}>
          <Text fw={800} fz={20} lh={1}>
            {score ?? '—'}
          </Text>
        </div>
      </div>
      <div>
        <Text className="ds-eyebrow">{t('dashboard.readiness')}</Text>
        <Text fw={700} fz={14} c={color}>
          {tone ? t(TONE_LABEL[tone]) : t('coachToday.noReadiness')}
        </Text>
        {stale && <Text className="ds-metadata">{t('coachToday.readinessFrom', { date: new Date(athlete.readiness!.date!).toLocaleDateString('cs-CZ') })}</Text>}
      </div>
    </div>
  );
}

function Signals({ athlete }: { athlete: CoachTodayAthleteDto }) {
  const { t } = useTranslation();
  const r = athlete.readiness;
  if (!r) return null;
  const chips: ReactNode[] = [];
  if (r.hrvRmssdMs != null) chips.push(<Chip key="hrv" label="HRV" value={`${Math.round(r.hrvRmssdMs)} ms`} trend={trendOf(r.hrvRmssdMs, r.hrvBaselineMs, true)} />);
  if (r.restingHeartRateBpm != null)
    chips.push(<Chip key="rhr" label={t('coachToday.restingHr')} value={`${r.restingHeartRateBpm}`} trend={trendOf(r.restingHeartRateBpm, r.restingHeartRateBaselineBpm, false)} />);
  if (r.sleepDurationMinutes != null) chips.push(<Chip key="sleep" label={t('coachToday.sleep')} value={formatTotalDuration(r.sleepDurationMinutes * 60)} />);
  if (r.ctl != null && r.atl != null) chips.push(<Chip key="load" label={t('coachToday.load')} value={`${Math.round(r.ctl)} / ${Math.round(r.atl)}`} />);
  if (chips.length === 0) return null;
  return <div className={classes.chips}>{chips}</div>;
}

function ActualLine({ activity, workout }: { activity: ActualActivityDto; workout?: WorkoutComparisonDto }) {
  const { t } = useTranslation();
  const time = activity.startedAtUtc ? new Date(activity.startedAtUtc).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' }) : null;
  const compliance = workout?.durationCompliancePercent ?? workout?.distanceCompliancePercent ?? null;
  return (
    <Group gap={8} wrap="nowrap" className={classes.actual}>
      <span className={classes.actualIcon}>{sportIcon(activity.sport)}</span>
      <Text fz={13} fw={600} truncate style={{ flex: 1, minWidth: 0 }}>
        {activity.title || t(`sport.${activity.sport}`)}
      </Text>
      <Text className="ds-metadata" style={{ whiteSpace: 'nowrap' }}>
        {[time, formatClock(activity.durationSeconds ?? 0), activity.distanceMeters ? formatSegmentDistance(Math.round(activity.distanceMeters)) : null].filter(Boolean).join(' · ')}
      </Text>
      {workout ? (
        compliance != null ? (
          <Tooltip label={t('coachToday.complianceHint')}>
            <span>
              <Badge tone={complianceTone(compliance)}>{compliance} %</Badge>
            </span>
          </Tooltip>
        ) : (
          <Badge tone="positive">{t('coachToday.done')}</Badge>
        )
      ) : (
        <Badge tone="neutral">{t('coachToday.unplanned')}</Badge>
      )}
    </Group>
  );
}

function PlannedWorkout({ workout, activitiesShared }: { workout: WorkoutComparisonDto; activitiesShared: boolean }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  if (workout.isRestDay) {
    return (
      <Group gap={8}>
        <IconBed size={16} color="var(--color-info)" aria-hidden />
        <Text fz={13} fw={600}>
          {t('calendar.restDay')}
        </Text>
      </Group>
    );
  }
  const segments = workout.segments ?? [];
  const planned = [workout.plannedDurationSeconds ? formatClock(workout.plannedDurationSeconds) : null, workout.plannedDistanceMeters ? formatSegmentDistance(workout.plannedDistanceMeters) : null]
    .filter(Boolean)
    .join(' · ');
  return (
    <div className={classes.workout}>
      <Group gap={8} wrap="nowrap">
        <span className={classes.plannedIcon}>{sportIcon(workout.sport)}</span>
        <Text
          fz={14}
          fw={700}
          truncate
          className={classes.workoutTitle}
          onClick={(e) => {
            e.stopPropagation();
            if (workout.workoutId) navigate(`/workouts/${workout.workoutId}`);
          }}
        >
          {workout.title}
        </Text>
        {planned && (
          <Text className="ds-metadata" style={{ whiteSpace: 'nowrap', marginLeft: 'auto' }}>
            {t('coachToday.planned')} {planned}
          </Text>
        )}
      </Group>
      {segments.length > 0 && (
        <Text className="ds-metadata" lineClamp={2} mt={2} ml={24}>
          {summarizeStructure(segments, t)}
        </Text>
      )}
      <div className={classes.workoutResult}>
        {workout.actual ? (
          <ActualLine activity={workout.actual} workout={workout} />
        ) : activitiesShared ? (
          <Group gap={6} className={classes.pending}>
            <IconCircleDashed size={15} aria-hidden />
            <Text fz={12}>{t('coachToday.notDoneYet')}</Text>
          </Group>
        ) : null}
      </div>
    </div>
  );
}

/**
 * One athlete's day: readiness with its overnight signals, today's plan with its structure, and what was
 * actually done (paired with the plan, compliance on top). Accent border when it needs a look.
 */
export function AthleteTodayCard({ athlete }: { athlete: CoachTodayAthleteDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const attention = attentionLevel(athlete);
  const workouts = athlete.todayWorkouts ?? [];
  const unplanned = athlete.unplannedActivities ?? [];
  const flag = athlete.activeHealthFlags?.[0];
  const open = () => navigate(`/athletes/${athlete.athleteUserId}`);
  const weekPlanned = athlete.weekPlannedWorkouts ?? 0;
  const weekDone = athlete.weekCompletedWorkouts ?? 0;

  return (
    <article
      className={[classes.card, attention && classes[`attention_${attention}`]].filter(Boolean).join(' ')}
      role="button"
      tabIndex={0}
      onClick={open}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') open();
      }}
    >
      <header className={classes.header}>
        <Group gap="sm" wrap="nowrap" style={{ minWidth: 0 }}>
          <Avatar radius="xl" color="brand" size={42}>
            {athlete.name?.[0] ?? '?'}
          </Avatar>
          <div style={{ minWidth: 0 }}>
            <Text fw={800} fz={16} truncate>
              {athlete.name}
            </Text>
            {flag ? (
              <Group gap={4} wrap="nowrap">
                <IconAlertTriangle size={13} color="var(--color-danger)" aria-hidden />
                <Text fz={12} c="var(--color-danger)" truncate>
                  {flag.bodyPart ?? t(`healthFlagType.${flag.type}`)} · {t(`healthFlagSeverity.${flag.severity}`)}
                </Text>
              </Group>
            ) : (
              <Text className="ds-metadata" truncate>
                {athlete.email}
              </Text>
            )}
          </div>
        </Group>
        <ReadinessBlock athlete={athlete} />
      </header>

      <Signals athlete={athlete} />

      <section className={classes.today}>
        <Text className="ds-eyebrow">{t('coachToday.today')}</Text>
        {!athlete.planShared ? (
          <Group gap={6}>
            <IconLock size={14} aria-hidden />
            <Text className="ds-metadata">{t('coachToday.planNotShared')}</Text>
          </Group>
        ) : workouts.length === 0 && unplanned.length === 0 ? (
          <Text className="ds-metadata">{t('coachToday.nothingPlanned')}</Text>
        ) : (
          <>
            {workouts.map((w) => (
              <PlannedWorkout key={w.workoutId} workout={w} activitiesShared={!!athlete.activitiesShared} />
            ))}
            {unplanned.map((a) => (
              <ActualLine key={a.activityId} activity={a} />
            ))}
          </>
        )}
      </section>

      {athlete.planShared && weekPlanned > 0 && (
        <footer className={classes.week}>
          <Text className="ds-metadata" style={{ whiteSpace: 'nowrap' }}>
            {t('coachToday.week')}
          </Text>
          <Progress value={(weekDone / weekPlanned) * 100} size="sm" radius="xl" color="var(--color-accent)" style={{ flex: 1 }} aria-hidden />
          <Group gap={4} wrap="nowrap">
            <IconCircleCheck size={14} aria-hidden />
            <Text fz={12} fw={700}>
              {athlete.activitiesShared ? `${weekDone} / ${weekPlanned}` : weekPlanned}
            </Text>
          </Group>
        </footer>
      )}
    </article>
  );
}
