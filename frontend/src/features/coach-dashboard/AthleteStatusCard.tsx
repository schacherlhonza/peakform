import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Group, Text } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { Panel, Badge, Skeleton } from '../../design-system/components';
import type { CoachAthleteRelationshipDto } from '../../api/generated/models';
import { useAthleteStatusData } from './useAthleteStatusData';
import classes from './AthleteStatusCard.module.css';

function readinessTone(score: number | null): 'positive' | 'warning' | 'danger' | 'neutral' {
  if (score == null) return 'neutral';
  if (score >= 75) return 'positive';
  if (score >= 50) return 'warning';
  return 'danger';
}

/** One roster card — avatar, today's plan, readiness, active warnings (docs/DESIGN_SYSTEM.md §7). */
export function AthleteStatusCard({ relationship }: { relationship: CoachAthleteRelationshipDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const status = useAthleteStatusData(relationship.athleteUserId!);

  if (status.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={140} mb="sm" />
        <Skeleton height={40} circle mb="sm" />
        <Skeleton height={40} />
      </Panel>
    );
  }

  const severeFlag = status.activeHealthFlags.find((f) => f.severity === 'Severe') ?? status.activeHealthFlags[0];

  return (
    <Panel
      className={classes.card}
      role="button"
      tabIndex={0}
      onClick={() => navigate(`/athletes/${relationship.athleteUserId}`)}
      onKeyDown={(e: React.KeyboardEvent) => {
        if (e.key === 'Enter' || e.key === ' ') navigate(`/athletes/${relationship.athleteUserId}`);
      }}
    >
      <Group justify="space-between" align="flex-start" wrap="nowrap">
        <Group gap="sm" wrap="nowrap">
          <Avatar radius="xl" color="brand">
            {relationship.athleteName?.[0] ?? '?'}
          </Avatar>
          <div style={{ minWidth: 0 }}>
            <Text fw={700} fz={14} truncate>
              {relationship.athleteName}
            </Text>
            <Text className="ds-metadata" truncate>
              {relationship.athleteEmail}
            </Text>
          </div>
        </Group>
        {status.readinessScore != null && <Badge tone={readinessTone(status.readinessScore)}>{status.readinessScore}</Badge>}
      </Group>

      <Text className="ds-body" mt="sm">
        {status.isRestDay ? t('calendar.restDay') : (status.todayWorkoutTitle ?? (status.hasPlan ? t('dashboard.todayWorkoutEmptyTitle') : t('dashboard.todayWorkoutNoPlanTitle')))}
      </Text>

      {severeFlag && (
        <Group gap={6} mt="sm">
          <IconAlertTriangle size={16} stroke={1.8} color="var(--color-danger)" />
          <Text fz={12} c="var(--color-danger)">
            {severeFlag.bodyPart ?? t(`healthFlagType.${severeFlag.type}`)} · {t(`healthFlagSeverity.${severeFlag.severity}`)}
          </Text>
        </Group>
      )}
    </Panel>
  );
}
