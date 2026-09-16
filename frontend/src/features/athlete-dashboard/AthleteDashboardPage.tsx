import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, Stack, Text, Title } from '@mantine/core';
import { IconFlag, IconMoonStars, IconSun } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { Panel, CardHeader, Badge, Button } from '../../design-system/components';
import { useAthleteDashboardData } from './useAthleteDashboardData';
import { ReadinessCard } from './ReadinessCard';
import { TodayWorkoutCard } from './TodayWorkoutCard';
import { FuelHydrationCard } from './FuelHydrationCard';
import { WeekTimelineSection } from './WeekTimelineSection';
import { RecoveryInsightsSection } from './RecoveryInsightsSection';
import classes from './AthleteDashboardPage.module.css';

/**
 * Reference screen for the redesign — docs/DESIGN_SYSTEM.md §4, §7. Dominant readiness, today's
 * workout, fuel/hydration, plan-vs-actual, recovery insights, then the pre-existing check-in
 * cards and next-race panel kept as compact secondary panels (nothing that worked before is
 * removed, only restyled and reorganized).
 */
export function AthleteDashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  const data = useAthleteDashboardData(athleteUserId);

  return (
    <Stack gap={17}>
      <Title className="ds-page-title" order={2}>
        {t('dashboard.athleteTitle', { name: '' }).replace(/,\s*$/, '')}
      </Title>

      <div className={classes.overview}>
        <div className={classes.readinessSlot}>
          <ReadinessCard data={data.readiness} />
        </div>
        <TodayWorkoutCard data={data.todayWorkout} />
        <FuelHydrationCard data={data.fuelHydration} />
      </div>

      <WeekTimelineSection data={data.weekTimeline} />
      <RecoveryInsightsSection data={data.insight} />

      <div className={classes.secondaryGrid}>
        <Panel compact>
          <CardHeader kicker={t('dashboard.morningCheckIn')} right={<IconSun size={18} stroke={1.8} color="var(--color-text-muted)" />} />
          <Text className="ds-body" lineClamp={4}>
            {data.checkIns.morningReport?.narrativeText ?? t('dashboard.fillMorningCheckIn')}
          </Text>
          <Button variant="default" mt="sm" onClick={() => navigate('/checkins/morning')}>
            {t('dashboard.fillMorningCheckIn')}
          </Button>
        </Panel>

        <Panel compact>
          <CardHeader kicker={t('dashboard.eveningCheckIn')} right={<IconMoonStars size={18} stroke={1.8} color="var(--color-text-muted)" />} />
          <Text className="ds-body" lineClamp={4}>
            {data.checkIns.eveningReport?.narrativeText ?? t('dashboard.fillEveningCheckIn')}
          </Text>
          <Button variant="default" mt="sm" onClick={() => navigate('/checkins/evening')}>
            {t('dashboard.fillEveningCheckIn')}
          </Button>
        </Panel>

        <Panel compact>
          <CardHeader kicker={t('dashboard.nextRace')} right={<IconFlag size={18} stroke={1.8} color="var(--color-text-muted)" />} />
          {data.nextRace.race ? (
            <Group gap="xs">
              <Text fw={600} fz={14}>
                {data.nextRace.race.name}
              </Text>
              <Badge tone="info">{t('dashboard.daysToRace', { days: data.nextRace.daysToRace })}</Badge>
            </Group>
          ) : (
            <Text className="ds-body">—</Text>
          )}
        </Panel>
      </div>
    </Stack>
  );
}

export default AthleteDashboardPage;
