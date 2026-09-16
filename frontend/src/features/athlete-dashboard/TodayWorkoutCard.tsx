import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, Stack, Text } from '@mantine/core';
import { IconBed, IconRun } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, MetricStrip, EmptyState, Skeleton, Button } from '../../design-system/components';
import type { PlannedWorkoutDto } from '../../api/generated/models';

export interface TodayWorkoutCardData {
  workout: PlannedWorkoutDto | null;
  isLoading: boolean;
  isError: boolean;
  hasPlan: boolean;
}

function formatDistance(meters?: number | null): string | null {
  if (meters == null) return null;
  return meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${meters} m`;
}

function formatDuration(seconds?: number | null): string | null {
  if (seconds == null) return null;
  const minutes = Math.round(seconds / 60);
  return `${minutes} min`;
}

/** Today's workout — dominant dose + structured parameters, never reduced to a text blob
 * (docs/DESIGN_SYSTEM.md §7). */
export function TodayWorkoutCard({ data }: { data: TodayWorkoutCardData }) {
  const { t } = useTranslation();
  const navigate = useNavigate();

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

  if (!data.hasPlan) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.todayWorkout')} />
        <EmptyState icon={<IconRun size={28} stroke={1.6} />} title={t('dashboard.todayWorkoutNoPlanTitle')} description={t('dashboard.todayWorkoutNoPlanDescription')} />
      </Panel>
    );
  }

  const workout = data.workout;

  if (!workout) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.todayWorkout')} />
        <EmptyState icon={<IconRun size={28} stroke={1.6} />} title={t('dashboard.todayWorkoutEmptyTitle')} description={t('dashboard.todayWorkoutEmptyDescription')} />
      </Panel>
    );
  }

  if (workout.isRestDay) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.todayWorkout')} right={<Badge tone="info">{t('calendar.restDay')}</Badge>} />
        <Group gap="sm" align="center">
          <IconBed size={28} stroke={1.6} color="var(--color-info)" />
          <Text className="ds-body">{t('dashboard.todayWorkoutRest')}</Text>
        </Group>
      </Panel>
    );
  }

  const dose = formatDistance(workout.plannedDistanceMeters) ?? formatDuration(workout.plannedDurationSeconds) ?? '—';

  return (
    <Panel>
      <CardHeader kicker={t('dashboard.todayWorkout')} right={<Badge tone="info">{workout.sport ? t(`sport.${workout.sport}`) : ''}</Badge>} />
      <Group align="baseline" gap="sm" wrap="wrap">
        <Text className="ds-card-headline">{workout.title}</Text>
        <Text className="ds-key-metric" fz={24} c="var(--color-accent)">
          {dose}
        </Text>
      </Group>
      {workout.coachDescription && (
        <Text className="ds-body" mt={4}>
          {workout.coachDescription}
        </Text>
      )}

      <MetricStrip
        metrics={[
          { label: t('workout.plannedDistance'), value: formatDistance(workout.plannedDistanceMeters) ?? '—' },
          { label: t('workout.plannedDuration'), value: formatDuration(workout.plannedDurationSeconds) ?? '—' },
          { label: t('workout.plannedElevation'), value: workout.plannedElevationGainMeters != null ? `${workout.plannedElevationGainMeters} m` : '—' },
        ]}
      />

      <Stack mt="md">
        <Button variant="default" onClick={() => workout.id && navigate(`/workouts/${workout.id}`)} disabled={!workout.id}>
          {t('dashboard.openWorkout')}
        </Button>
      </Stack>
    </Panel>
  );
}
