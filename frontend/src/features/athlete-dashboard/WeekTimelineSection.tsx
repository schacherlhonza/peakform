import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Text } from '@mantine/core';
import { IconCalendarStats } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, EmptyState, Skeleton } from '../../design-system/components';
import type { PlannedWorkoutDto, CompletedActivityDto, TrainingWeekDto } from '../../api/generated/models';
import { toIsoDate } from '../../calendar/dateUtils';
import classes from './WeekTimelineSection.module.css';

export interface WeekTimelineData {
  days: Date[];
  week: TrainingWeekDto | null;
  activities: CompletedActivityDto[];
  isLoading: boolean;
  isError: boolean;
}

const weekdayKeys = ['0', '1', '2', '3', '4', '5', '6'];

/** Plan-vs-actual weekly timeline — horizontally scrollable on mobile, never crushed into an
 * unreadable table (docs/DESIGN_SYSTEM.md §4, §7). */
export function WeekTimelineSection({ data }: { data: WeekTimelineData }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const todayIso = toIsoDate(new Date());

  if (data.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={160} mb="md" />
        <Skeleton height={140} />
      </Panel>
    );
  }

  if (data.isError) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.weekTimeline')} />
        <EmptyState icon={<IconCalendarStats size={28} stroke={1.6} />} title={t('common.error')} description={t('common.unknownError')} />
      </Panel>
    );
  }

  const workoutsByDate = new Map<string, PlannedWorkoutDto>();
  for (const w of data.week?.workouts ?? []) {
    if (w.date) workoutsByDate.set(w.date, w);
  }
  const activitiesByDate = new Map<string, CompletedActivityDto[]>();
  for (const a of data.activities) {
    if (!a.startedAtUtc) continue;
    const date = toIsoDate(new Date(a.startedAtUtc));
    activitiesByDate.set(date, [...(activitiesByDate.get(date) ?? []), a]);
  }

  if (!data.week && data.activities.length === 0) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.weekTimeline')} />
        <EmptyState icon={<IconCalendarStats size={28} stroke={1.6} />} title={t('dashboard.weekTimelineEmpty')} />
      </Panel>
    );
  }

  return (
    <Panel>
      <CardHeader kicker={t('dashboard.weekTimeline')} />
      <div className={classes.scroll}>
        <div className={classes.grid}>
          {data.days.map((day, i) => {
            const iso = toIsoDate(day);
            const isToday = iso === todayIso;
            const workout = workoutsByDate.get(iso);
            const dayActivities = activitiesByDate.get(iso) ?? [];
            return (
              <div key={iso} className={isToday ? `${classes.day} ${classes.dayToday}` : classes.day}>
                <Text className="ds-eyebrow">{t(`weekday.${weekdayKeys[i]}`)}</Text>
                <div className={classes.row}>
                  <Badge tone="info">{t('dashboard.planned')}</Badge>
                  <Text fz={12} fw={600} lineClamp={2}>
                    {workout?.isRestDay ? t('calendar.restDay') : (workout?.title ?? '—')}
                  </Text>
                </div>
                <div className={classes.row}>
                  <Badge tone={dayActivities.length > 0 ? 'positive' : 'neutral'}>{t('dashboard.actual')}</Badge>
                  {dayActivities.length === 0 ? (
                    <Text fz={12} fw={600}>
                      —
                    </Text>
                  ) : (
                    dayActivities.map((activity) => (
                      <Text
                        key={activity.id}
                        fz={12}
                        fw={600}
                        lineClamp={2}
                        className={activity.id ? classes.activityLink : undefined}
                        role={activity.id ? 'button' : undefined}
                        tabIndex={activity.id ? 0 : undefined}
                        onClick={() => activity.id && navigate(`/activities/${activity.id}`)}
                        onKeyDown={(e) => {
                          if (activity.id && (e.key === 'Enter' || e.key === ' ')) navigate(`/activities/${activity.id}`);
                        }}
                      >
                        {activity.title ?? t(`sport.${activity.sport}`)}
                      </Text>
                    ))
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </Panel>
  );
}
