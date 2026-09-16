import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, Stack, Text, Title } from '@mantine/core';
import { IconUserPlus, IconUsers } from '@tabler/icons-react';
import { Button, EmptyState, Skeleton } from '../../design-system/components';
import { useCoachDashboardData } from './useCoachDashboardData';
import { AthleteStatusCard } from './AthleteStatusCard';
import { AttentionQueue } from './AttentionQueue';
import classes from './CoachDashboardPage.module.css';

/** Coach dashboard — same primitives as the athlete dashboard, not a separate visual template
 * (docs/DESIGN_SYSTEM.md §7). */
export function CoachDashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const data = useCoachDashboardData();

  return (
    <Stack gap={17}>
      <Group justify="space-between" align="center">
        <Title className="ds-page-title" order={2}>
          {t('dashboard.coachTitle')}
        </Title>
        <Button leftSection={<IconUserPlus size={16} />} onClick={() => navigate('/athletes')}>
          {t('relationships.invite')}
        </Button>
      </Group>

      <div className={classes.layout}>
        <Stack gap="sm">
          <Text className="ds-eyebrow">{t('nav.athletes')}</Text>
          {data.athletesLoading ? (
            <div className={classes.roster}>
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} height={120} radius="var(--radius-panel)" />
              ))}
            </div>
          ) : data.athletesError ? (
            <EmptyState
              icon={<IconUsers size={28} stroke={1.6} />}
              title={t('common.error')}
              description={t('common.unknownError')}
              action={
                <Button variant="default" onClick={data.refetchAthletes}>
                  {t('common.back')}
                </Button>
              }
            />
          ) : data.athletes.length === 0 ? (
            <EmptyState
              icon={<IconUsers size={28} stroke={1.6} />}
              title={t('dashboard.noAthletes')}
              action={<Button onClick={() => navigate('/athletes')}>{t('relationships.invite')}</Button>}
            />
          ) : (
            <div className={classes.roster}>
              {data.athletes.map((rel) => (
                <AthleteStatusCard key={rel.id} relationship={rel} />
              ))}
            </div>
          )}
        </Stack>

        <AttentionQueue data={data.attentionQueue} />
      </div>
    </Stack>
  );
}

export default CoachDashboardPage;
