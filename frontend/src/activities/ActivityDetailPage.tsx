import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { isAxiosError } from 'axios';
import { Stack, Text, Title } from '@mantine/core';
import { IconRun } from '@tabler/icons-react';
import { useGetApiActivitiesActivityId, useGetApiActivitiesActivityIdStreams } from '../api/generated/activities/activities';
import { Panel, Badge, MetricStrip, EmptyState, Skeleton, type Metric } from '../design-system/components';
import { StreamChart } from './StreamChart';
import { formatClock, formatDistanceKm, formatPace } from './activityFormat';

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

  const metrics: Metric[] = [
    { label: t('activity.distance'), value: activity.distanceMeters != null ? formatDistanceKm(activity.distanceMeters) : '—' },
    { label: t('activity.duration'), value: formatClock(activity.durationSeconds ?? 0) },
    { label: t('activity.avgHeartRate'), value: activity.averageHeartRateBpm != null ? `${activity.averageHeartRateBpm} bpm` : '—' },
    { label: t('activity.maxHeartRate'), value: activity.maxHeartRateBpm != null ? `${activity.maxHeartRateBpm} bpm` : '—' },
    { label: t('activity.avgPace'), value: activity.averagePaceSecondsPerKm != null ? formatPace(activity.averagePaceSecondsPerKm) : '—' },
    { label: t('activity.elevation'), value: activity.elevationGainMeters != null ? `${Math.round(activity.elevationGainMeters)} m` : '—' },
    { label: t('activity.avgPower'), value: activity.averagePowerWatts != null ? `${activity.averagePowerWatts} W` : '—' },
    { label: t('activity.calories'), value: activity.calories != null ? `${activity.calories} kcal` : '—' },
  ];

  return (
    <Stack gap="lg">
      <div>
        <Badge tone="info">{t(`sport.${activity.sport}`)}</Badge>
        <Title className="ds-section-title" order={2} mt={4}>
          {activity.title || t(`sport.${activity.sport}`)}
        </Title>
        <Text className="ds-metadata">
          {activity.startedAtUtc && new Date(activity.startedAtUtc).toLocaleString('cs-CZ')}
          {activity.source && ` · ${t(`activity.source.${activity.source}`)}`}
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
          <StreamChart
            title={t('activity.metric.heartRate')}
            explanation={t('activity.metric.heartRateExplanation')}
            unit="bpm"
            color="var(--color-warning)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.heartRateBpm ?? []}
          />
          <StreamChart
            title={t('activity.metric.pace')}
            explanation={t('activity.metric.paceExplanation')}
            unit="/km"
            color="var(--color-accent)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.paceSecondsPerKm ?? []}
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
          />
          <StreamChart
            title={t('activity.metric.cadence')}
            explanation={t('activity.metric.cadenceExplanation')}
            unit="rpm"
            color="var(--color-info)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.cadenceRpm ?? []}
          />
          <StreamChart
            title={t('activity.metric.power')}
            explanation={t('activity.metric.powerExplanation')}
            unit="W"
            color="var(--color-accent-deep)"
            kind="line"
            times={streams.timeOffsetsSeconds ?? []}
            values={streams.powerWatts ?? []}
          />
        </>
      )}
    </Stack>
  );
}

export default ActivityDetailPage;
