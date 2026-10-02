import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import { Text } from '@mantine/core';
import { IconUserExclamation } from '@tabler/icons-react';
import { EmptyState, Skeleton } from '../design-system/components';
import { useGetApiRelationships } from '../api/generated/relationships/relationships';
import { ActivitiesBrowser } from '../activities/ActivitiesPage';
import { AthleteViewSwitch } from './AthleteViewSwitch';

/** A coach's view of one athlete's activity history and records — same as the athlete's own
 * page; the backend still checks the coach's ViewCompletedActivities permission. */
export default function AthleteActivitiesPage() {
  const { t } = useTranslation();
  const { athleteId } = useParams<{ athleteId: string }>();
  const relationshipsQuery = useGetApiRelationships();

  if (!athleteId) return null;
  if (relationshipsQuery.isLoading) return <Skeleton height={420} radius="var(--radius-panel)" />;

  const relationship = relationshipsQuery.data?.find((r) => r.athleteUserId === athleteId);
  if (!relationship) {
    return <EmptyState icon={<IconUserExclamation size={28} stroke={1.6} />} title={t('athletes.notFoundTitle')} description={t('athletes.notFoundDescription')} />;
  }

  return (
    <ActivitiesBrowser
      athleteUserId={athleteId}
      title={relationship.athleteName ?? t('nav.activities')}
      subtitle={<Text className="ds-body">{relationship.athleteEmail}</Text>}
      viewSwitch={<AthleteViewSwitch athleteId={athleteId} />}
    />
  );
}
