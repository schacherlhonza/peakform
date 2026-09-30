import { lazy, Suspense, useCallback, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { isAxiosError } from 'axios';
import { Anchor, Stack, Text, Title } from '@mantine/core';
import { IconArrowLeft, IconRun } from '@tabler/icons-react';
import { useAuth } from '../auth/AuthContext';
import { useGetApiActivitiesActivityId, useGetApiActivitiesActivityIdStreams } from '../api/generated/activities/activities';
import { ActivityMetricType, AppRole, SportType } from '../api/generated/models';
import { Panel, Badge, CardHeader, MetricStrip, EmptyState, Skeleton, type Metric } from '../design-system/components';
import { StreamChart } from './StreamChart';
import { formatClock, formatDistanceKm, formatPace } from './activityFormat';

// Leaflet (~40 kB gzip) only loads when an activity actually has a GPS route.
const ActivityMap = lazy(() => import('./ActivityMap'));

function ActivityDetailSkeleton() {
  return (
    <Stack gap="lg">
      <Skeleton height={28} width={280} />
      <Skeleton height={120} radius="var(--radius-panel)" />
      <Skeleton height={200} radius="var(--radius-panel)" />
      <Skeleton height={200} radius="var(--radius-panel)" />
    </Stack>
  );
}

export function ActivityDetailPage() {
  const { activityId } = useParams<{ activityId: string }>();
  const { t } = useTranslation();
  const { user } = useAuth();
  // Set by ActivitiesPage so "back" restores the same filters/page; absent when opened from elsewhere.
  const listSearch = (useLocation().state as { listSearch?: string } | null)?.listSearch;
  const [hoverTime, setHoverTime] = useState<number | null>(null);
  const handleHoverTime = useCallback((offset: number | null) => setHoverTime(offset), []);

  const activityQuery = useGetApiActivitiesActivityId(activityId ?? '', { query: { enabled: !!activityId } });
  const streamsQuery = useGetApiActivitiesActivityIdStreams(activityId ?? '', { query: { enabled: !!activityId } });

  if (!activityId) return null;
  if (activityQuery.isLoading) return <ActivityDetailSkeleton />;
  if (activityQuery.isError || !activityQuery.data) {
    return (
      <Panel>
        <EmptyState icon={<IconRun size={28} stroke={1.6} />} title={t('activity.notFound')} />
      </Panel>
    );
  }

  const activity = activityQuery.data;
  const streamsUnavailable = isAxiosError(streamsQuery.error) && streamsQuery.error.response?.status === 404;
  const streams = streamsQuery.data;

  const findMetric = (type: ActivityMetricType) => activity.additionalMetrics?.find((m) => m.type === type);
  const elapsedTime = findMetric(ActivityMetricType.ElapsedTimeSeconds);
  const trainingLoad = findMetric(ActivityMetricType.TrainingLoad);
  const intensity = findMetric(ActivityMetricType.Intensity);
  const workJoules = findMetric(ActivityMetricType.WorkJoules);
  const weightedAvgPower = findMetric(ActivityMetricType.WeightedAveragePowerWatts);

  const metrics: Metric[] = [
    { label: t('activity.distance'), value: activity.distanceMeters != null ? formatDistanceKm(activity.distanceMeters) : '—' },
    { label: t('activity.duration'), value: formatClock(activity.durationSeconds ?? 0) },
    { label: t('activity.avgHeartRate'), value: activity.averageHeartRateBpm != null ? `${activity.averageHeartRateBpm} bpm` : '—' },
    { label: t('activity.maxHeartRate'), value: activity.maxHeartRateBpm != null ? `${activity.maxHeartRateBpm} bpm` : '—' },
    { label: t('activity.avgPace'), value: activity.averagePaceSecondsPerKm != null ? formatPace(activity.averagePaceSecondsPerKm) : '—' },
    { label: t('activity.elevation'), value: activity.elevationGainMeters != null ? `${Math.round(activity.elevationGainMeters)} m` : '—' },
    { label: t('activity.avgPower'), value: activity.averagePowerWatts != null ? `${activity.averagePowerWatts} W` : '—' },
    { label: t('activity.calories'), value: activity.calories != null ? `${activity.calories} kcal` : '—' },
    ...(elapsedTime ? [{ label: t('activity.elapsedTime'), value: formatClock(elapsedTime.value ?? 0) }] : []),
    ...(trainingLoad ? [{ label: t('activity.trainingLoad'), value: `${Math.round(trainingLoad.value ?? 0)}` }] : []),
    ...(intensity ? [{ label: t('activity.intensity'), value: `${Math.round(intensity.value ?? 0)}%` }] : []),
    ...(weightedAvgPower ? [{ label: t('activity.weightedAvgPower'), value: `${Math.round(weightedAvgPower.value ?? 0)} W` }] : []),
    ...(workJoules ? [{ label: t('activity.workEnergy'), value: `${Math.round((workJoules.value ?? 0) / 1000)} kJ` }] : []),
  ];

  return (
    <Stack gap="lg">
      <div>
        {user?.role === AppRole.Athlete && (
          <Anchor component={Link} to={`/activities${listSearch ? `?${listSearch}` : ''}`} size="sm" mb={6} display="flex" w="fit-content" style={{ alignItems: 'center', gap: 4 }}>
            <IconArrowLeft size={14} />
            {t('activities.backToList')}
          </Anchor>
        )}
        <Badge tone="info">{t(`sport.${activity.sport}`)}</Badge>
        <Title className="ds-section-title" order={2} mt={4}>
          {activity.title || t(`sport.${activity.sport}`)}
        </Title>
        <Text className="ds-metadata">
          {activity.startedAtUtc && new Date(activity.startedAtUtc).toLocaleString('cs-CZ')}
          {activity.source && ` · ${t(`activity.source.${activity.source}`)}`}
          {activity.deviceName && ` · ${activity.deviceName}`}
          {activity.plannedWorkoutId && (
            <>
              {' · '}
              <Link to={`/workouts/${activity.plannedWorkoutId}`}>{t('activity.plannedWorkoutLink')}</Link>
            </>
          )}
        </Text>
      </div>

      <Panel>
        <MetricStrip metrics={metrics} />
      </Panel>

      {streamsUnavailable && (
        <Panel>
          <EmptyState icon={<IconRun size={28} stroke={1.6} />} title={t('activity.streamsUnavailable')} description={t('activity.streamsUnavailableHint')} />
        </Panel>
      )}

      {streamsQuery.isLoading && !streamsUnavailable && <Skeleton height={200} radius="var(--radius-panel)" />}

      {streams && (
        <>
          {streams.latitude?.some((v) => v != null) && (
            <Panel>
              <CardHeader kicker={t('activity.map.title')} />
              <Suspense fallback={<Skeleton height={340} radius="var(--radius-panel)" />}>
                <ActivityMap
                  latitude={streams.latitude ?? []}
                  longitude={streams.longitude ?? []}
                  times={streams.timeOffsetsSeconds ?? []}
                  hoverTime={hoverTime}
                  startLabel={t('activity.map.start')}
                  finishLabel={t('activity.map.finish')}
                />
              </Suspense>
            </Panel>
          )}
          <StreamChart
            title={t('activity.metric.heartRate')}
            explanation={t('activity.metric.heartRateExplanation')}
            unit="bpm"
            color="var(--color-warning)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.heartRateBpm ?? []}
            onHoverTime={handleHoverTime}
          />
          <StreamChart
            title={t('activity.metric.pace')}
            explanation={t('activity.metric.paceExplanation')}
            unit="/km"
            color="var(--color-accent)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.paceSecondsPerKm ?? []}
            onHoverTime={handleHoverTime}
            formatValue={(v) => formatPace(v)}
            reverseYAxis
          />
          <StreamChart
            title={t('activity.metric.elevation')}
            explanation={t('activity.metric.elevationExplanation')}
            unit="m"
            color="var(--color-success)"
            kind="area"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.elevationMeters ?? []}
            onHoverTime={handleHoverTime}
          />
          <StreamChart
            title={t('activity.metric.cadence')}
            explanation={t('activity.metric.cadenceExplanation')}
            unit={activity.sport === SportType.Running ? 'spm' : 'rpm'}
            color="var(--color-info)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.cadenceRpm ?? []}
            onHoverTime={handleHoverTime}
          />
          <StreamChart
            title={t('activity.metric.power')}
            explanation={t('activity.metric.powerExplanation')}
            unit="W"
            color="var(--color-accent-deep)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.powerWatts ?? []}
            onHoverTime={handleHoverTime}
          />
          <StreamChart
            title={t('activity.metric.grade')}
            explanation={t('activity.metric.gradeExplanation')}
            unit="%"
            color="var(--color-danger)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.gradePercent ?? []}
            onHoverTime={handleHoverTime}
          />
          <StreamChart
            title={t('activity.metric.temperature')}
            explanation={t('activity.metric.temperatureExplanation')}
            unit="°C"
            color="var(--color-warning)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.temperatureC ?? []}
            onHoverTime={handleHoverTime}
          />
        </>
      )}
      {/* intervals.icu API terms: Garmin-sourced data must carry Garmin attribution. */}
      {activity.deviceName?.toLowerCase().includes('garmin') && (
        <Text className="ds-metadata">{t('activity.garminAttribution', { device: activity.deviceName })}</Text>
      )}
    </Stack>
  );
}

export default ActivityDetailPage;
