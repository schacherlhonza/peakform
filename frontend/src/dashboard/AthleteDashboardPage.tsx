import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Badge, Button, Card, Grid, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { IconFlag, IconMoonStars, IconSun } from '@tabler/icons-react';
import { useAuth } from '../auth/AuthContext';
import { useGetApiAthletesAthleteUserIdRaces } from '../api/generated/races/races';
import { useGetApiAthletesAthleteUserIdReportsDateType } from '../api/generated/reports/reports';
import { ReportType } from '../api/generated/models';

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export function AthleteDashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  const racesQuery = useGetApiAthletesAthleteUserIdRaces(athleteUserId);
  const morningReportQuery = useGetApiAthletesAthleteUserIdReportsDateType(athleteUserId, todayIso(), ReportType.Morning);

  const nextRace = useMemo(() => {
    const races = racesQuery.data ?? [];
    const now = Date.now();
    return races
      .filter((r) => r.startsAtUtc && new Date(r.startsAtUtc).getTime() >= now)
      .sort((a, b) => new Date(a.startsAtUtc!).getTime() - new Date(b.startsAtUtc!).getTime())[0];
  }, [racesQuery.data]);

  const daysToRace = nextRace?.startsAtUtc
    ? Math.max(0, Math.ceil((new Date(nextRace.startsAtUtc).getTime() - Date.now()) / 86_400_000))
    : null;

  return (
    <Stack gap="lg">
      <Title order={2}>{t('dashboard.athleteTitle', { name: '' }).replace(/,\s*$/, '')}</Title>

      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <Card withBorder radius="md" p="lg" h="100%">
            <Group justify="space-between" mb="xs">
              <Text fw={600}>{t('dashboard.morningCheckIn')}</Text>
              <IconSun size={20} />
            </Group>
            {morningReportQuery.isLoading ? (
              <Loader size="sm" />
            ) : morningReportQuery.data?.narrativeText ? (
              <Text size="sm" c="dimmed" lineClamp={4}>
                {morningReportQuery.data.narrativeText}
              </Text>
            ) : (
              <Text size="sm" c="dimmed">
                {t('dashboard.fillMorningCheckIn')}
              </Text>
            )}
            <Button mt="md" variant="light" size="xs" onClick={() => navigate('/checkins/morning')}>
              {t('dashboard.fillMorningCheckIn')}
            </Button>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <Card withBorder radius="md" p="lg" h="100%">
            <Group justify="space-between" mb="xs">
              <Text fw={600}>{t('dashboard.eveningCheckIn')}</Text>
              <IconMoonStars size={20} />
            </Group>
            <Text size="sm" c="dimmed">
              {t('dashboard.fillEveningCheckIn')}
            </Text>
            <Button mt="md" variant="light" size="xs" onClick={() => navigate('/checkins/evening')}>
              {t('dashboard.fillEveningCheckIn')}
            </Button>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <Card withBorder radius="md" p="lg" h="100%">
            <Group justify="space-between" mb="xs">
              <Text fw={600}>{t('dashboard.nextRace')}</Text>
              <IconFlag size={20} />
            </Group>
            {racesQuery.isLoading ? (
              <Loader size="sm" />
            ) : nextRace ? (
              <Stack gap={4}>
                <Text size="sm" fw={500}>
                  {nextRace.name}
                </Text>
                <Badge variant="light">{t('dashboard.daysToRace', { days: daysToRace })}</Badge>
              </Stack>
            ) : (
              <Text size="sm" c="dimmed">
                —
              </Text>
            )}
          </Card>
        </Grid.Col>
      </Grid>

      <Card withBorder radius="md" p="lg">
        <Title order={4} mb="sm">
          {t('nav.calendar')}
        </Title>
        <Button variant="light" onClick={() => navigate('/calendar')}>
          {t('nav.calendar')}
        </Button>
      </Card>
    </Stack>
  );
}
