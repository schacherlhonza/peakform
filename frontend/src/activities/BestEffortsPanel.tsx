import { useTranslation } from 'react-i18next';
import { Group, SimpleGrid, Stack, Text, Tooltip } from '@mantine/core';
import { useGetApiActivitiesActivityIdBestEfforts } from '../api/generated/activities/activities';
import { Panel, CardHeader, Badge } from '../design-system/components';
import { EFFORT_ORDER, formatEffortDetail, formatEffortValue } from './bestEfforts';
import { formatClock } from './activityFormat';

/** This activity's best efforts, with how each ranks among all of the athlete's efforts. */
export function BestEffortsPanel({ activityId }: { activityId: string }) {
  const { t } = useTranslation();
  const query = useGetApiActivitiesActivityIdBestEfforts(activityId);
  const efforts = [...(query.data ?? [])].sort((a, b) => EFFORT_ORDER.indexOf(a.type!) - EFFORT_ORDER.indexOf(b.type!));
  if (efforts.length === 0) return null;

  return (
    <Panel>
      <CardHeader kicker={t('activity.bestEfforts.title')} />
      <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md">
        {efforts.map((e) => {
          const detail = formatEffortDetail(e.type!, e.value ?? 0);
          return (
            <Stack key={e.type} gap={2}>
              <Text className="ds-eyebrow">{t(`activities.records.type.${e.type}`)}</Text>
              <Group gap={6} wrap="nowrap">
                <Text fw={600} style={{ fontVariantNumeric: 'tabular-nums' }}>
                  {!e.isPrecise && (
                    <Tooltip label={t('activities.records.estimate')}>
                      <span>≈ </span>
                    </Tooltip>
                  )}
                  {formatEffortValue(e.type!, e.value ?? 0)}
                </Text>
                {e.isPersonalBest ? (
                  <Badge tone="positive">{t('activity.bestEfforts.personalBest')}</Badge>
                ) : (e.rank ?? 99) <= 3 ? (
                  <Badge tone="info">{t('activity.bestEfforts.rank', { rank: e.rank })}</Badge>
                ) : null}
              </Group>
              <Text className="ds-metadata">
                {[detail, t('activity.bestEfforts.at', { time: formatClock(e.startOffsetSeconds ?? 0) })].filter(Boolean).join(' · ')}
              </Text>
            </Stack>
          );
        })}
      </SimpleGrid>
    </Panel>
  );
}
