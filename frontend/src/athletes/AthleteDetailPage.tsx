import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import { Stack, Text, Title } from '@mantine/core';
import { IconUserExclamation } from '@tabler/icons-react';
import { Button, EmptyState, Skeleton } from '../design-system/components';
import { useGetApiRelationships } from '../api/generated/relationships/relationships';
import { WeekCalendar } from '../calendar/WeekCalendar';

function AthleteDetailSkeleton() {
  return (
    <Stack gap="lg">
      <div>
        <Skeleton height={28} width={220} mb={8} />
        <Skeleton height={14} width={160} />
      </div>
      <Skeleton height={420} radius="var(--radius-panel)" />
    </Stack>
  );
}

export function AthleteDetailPage() {
  const { t } = useTranslation();
  const { athleteId } = useParams<{ athleteId: string }>();
  const relationshipsQuery = useGetApiRelationships();

  if (!athleteId) return null;

  if (relationshipsQuery.isLoading) return <AthleteDetailSkeleton />;

  if (relationshipsQuery.isError) {
    return (
      <EmptyState
        icon={<IconUserExclamation size={28} stroke={1.6} />}
        title={t('common.error')}
        description={t('common.unknownError')}
        action={
          <Button variant="default" onClick={() => relationshipsQuery.refetch()}>
            {t('common.back')}
          </Button>
        }
      />
    );
  }

  const relationship = relationshipsQuery.data?.find((r) => r.athleteUserId === athleteId);

  if (!relationship) {
    return (
      <EmptyState
        icon={<IconUserExclamation size={28} stroke={1.6} />}
        title={t('athletes.notFoundTitle')}
        description={t('athletes.notFoundDescription')}
      />
    );
  }

  return (
    <Stack gap="lg">
      <div>
        <Title className="ds-page-title" order={2}>
          {relationship.athleteName}
        </Title>
        <Text className="ds-body">{relationship.athleteEmail}</Text>
      </div>
      <WeekCalendar athleteUserId={athleteId} canEdit />
    </Stack>
  );
}
