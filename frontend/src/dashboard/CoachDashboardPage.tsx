import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Badge, Button, Card, Group, Loader, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { IconUserPlus } from '@tabler/icons-react';
import { useGetApiRelationships } from '../api/generated/relationships/relationships';
import { RelationshipStatus } from '../api/generated/models';

export function CoachDashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const relationshipsQuery = useGetApiRelationships();
  const athletes = (relationshipsQuery.data ?? []).filter((r) => r.status === RelationshipStatus.Active);

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Title order={2}>{t('dashboard.coachTitle')}</Title>
        <Button leftSection={<IconUserPlus size={16} />} onClick={() => navigate('/athletes')}>
          {t('relationships.invite')}
        </Button>
      </Group>

      {relationshipsQuery.isLoading ? (
        <Loader />
      ) : athletes.length === 0 ? (
        <Card withBorder radius="md" p="xl">
          <Text c="dimmed" ta="center">
            {t('dashboard.noAthletes')}
          </Text>
        </Card>
      ) : (
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
          {athletes.map((rel) => (
            <Card
              key={rel.id}
              withBorder
              radius="md"
              p="lg"
              style={{ cursor: 'pointer' }}
              onClick={() => navigate(`/athletes/${rel.athleteUserId}`)}
            >
              <Group>
                <Avatar radius="xl" color="brand">
                  {rel.athleteName?.[0] ?? '?'}
                </Avatar>
                <div>
                  <Text fw={500}>{rel.athleteName}</Text>
                  <Text size="xs" c="dimmed">
                    {rel.athleteEmail}
                  </Text>
                </div>
                <Badge ml="auto" color="green" variant="light">
                  {t(`relationships.status.${rel.status}`)}
                </Badge>
              </Group>
            </Card>
          ))}
        </SimpleGrid>
      )}
    </Stack>
  );
}
