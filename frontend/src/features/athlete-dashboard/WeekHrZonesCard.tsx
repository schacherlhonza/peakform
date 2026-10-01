import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Anchor, Text } from '@mantine/core';
import { Panel, CardHeader, Skeleton } from '../../design-system/components';
import { useGetApiAthletesAthleteUserIdHeartRateZones } from '../../api/generated/heart-rate-zones/heart-rate-zones';
import type { CompletedActivityDto } from '../../api/generated/models';
import { HrZoneBars } from '../../activities/HrZoneBars';
import { sumZoneSeconds } from '../../activities/hrZones';
import { formatClock } from '../../activities/activityFormat';

/** This week's time in heart rate zones, summed over the week's activities. */
export function WeekHrZonesCard({
  athleteUserId,
  activities,
  isLoading,
}: {
  athleteUserId: string;
  activities: readonly CompletedActivityDto[];
  isLoading: boolean;
}) {
  const { t } = useTranslation();
  const zonesQuery = useGetApiAthletesAthleteUserIdHeartRateZones(athleteUserId);
  const seconds = sumZoneSeconds(activities.map((a) => a.additionalMetrics));
  const total = seconds.total;
  const hasZones = (zonesQuery.data?.length ?? 0) > 0;

  return (
    <Panel>
      <CardHeader
        kicker={t('dashboard.weekHrZones.kicker')}
        title={t('dashboard.weekHrZones.title')}
        right={total > 0 ? <Text className="ds-metadata">{formatClock(Math.round(total))}</Text> : undefined}
      />
      {isLoading || zonesQuery.isLoading ? (
        <Skeleton height={120} />
      ) : !hasZones ? (
        <Text className="ds-body">
          {t('dashboard.weekHrZones.noZones')}{' '}
          <Anchor component={Link} to="/settings/heart-rate-zones">
            {t('dashboard.weekHrZones.setZones')}
          </Anchor>
        </Text>
      ) : total === 0 ? (
        <Text className="ds-body">{t('dashboard.weekHrZones.empty')}</Text>
      ) : (
        <HrZoneBars data={seconds} zones={zonesQuery.data} />
      )}
    </Panel>
  );
}
