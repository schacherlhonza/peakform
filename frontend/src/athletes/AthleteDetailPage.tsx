import { useParams } from 'react-router-dom';
import { Loader, Stack, Text, Title } from '@mantine/core';
import { useGetApiRelationships } from '../api/generated/relationships/relationships';
import { WeekCalendar } from '../calendar/WeekCalendar';

export function AthleteDetailPage() {
  const { athleteId } = useParams<{ athleteId: string }>();
  const relationshipsQuery = useGetApiRelationships();

  const relationship = relationshipsQuery.data?.find((r) => r.athleteUserId === athleteId);

  if (relationshipsQuery.isLoading) return <Loader />;
  if (!athleteId) return null;

  return (
    <Stack gap="lg">
      <div>
        <Title order={2}>{relationship?.athleteName ?? 'Sportovec'}</Title>
        <Text c="dimmed" size="sm">
          {relationship?.athleteEmail}
        </Text>
      </div>
      <WeekCalendar athleteUserId={athleteId} canEdit />
    </Stack>
  );
}
