import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router-dom';
import { Group, Stack, Text, Title } from '@mantine/core';
import { IconArrowLeft, IconFlagOff, IconMapPin, IconPencil } from '@tabler/icons-react';
import { Badge, Button, EmptyState, MetricStrip, Panel, Skeleton, type BadgeTone } from '../design-system/components';
import { useGetApiRacesId } from '../api/generated/races/races';
import { AppRole, GoalPriority } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { CommentThread } from '../comments/CommentThread';
import { RaceBadgeIcon } from './RaceBadgeIcon';
import { daysUntil, formatRaceTime, priorityColor } from './raceUtils';
import classes from './RaceDetailPage.module.css';

/** "sobota 10. října" → "Sobota 10. října" (CSS capitalize would capitalise every word). */
const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

const priorityTone: Record<GoalPriority, BadgeTone> = { [GoalPriority.A]: 'danger', [GoalPriority.B]: 'warning', [GoalPriority.C]: 'neutral' };

/**
 * One race for the athlete and their coach: a hero with the priority-coloured trophy and a countdown,
 * the key numbers (target vs result) and the shared comment thread.
 */
export default function RaceDetailPage() {
  const { raceId } = useParams<{ raceId: string }>();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const query = useGetApiRacesId(raceId ?? '', { query: { enabled: !!raceId } });

  if (!raceId) return null;
  if (query.isLoading) {
    return (
      <Stack gap="lg">
        <Skeleton height={180} radius="var(--radius-panel)" />
        <Skeleton height={120} radius="var(--radius-panel)" />
      </Stack>
    );
  }
  const race = query.data;
  if (query.isError || !race) {
    return (
      <Panel>
        <EmptyState icon={<IconFlagOff size={28} stroke={1.6} />} title={t('raceDetail.notFound')} />
      </Panel>
    );
  }

  const color = priorityColor(race.priority);
  const days = race.startsAtUtc ? daysUntil(race.startsAtUtc) : null;
  const starts = race.startsAtUtc ? new Date(race.startsAtUtc) : null;
  const target = formatRaceTime(race.targetTimeSeconds);
  const result = formatRaceTime(race.actualTimeSeconds);
  const diff = race.actualTimeSeconds != null && race.targetTimeSeconds != null ? race.actualTimeSeconds - race.targetTimeSeconds : null;

  const countdown =
    days == null ? null : days > 0 ? t('raceDetail.inDays', { count: days }) : days === 0 ? t('raceDetail.raceDay') : t('raceDetail.past');

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Button variant="subtle" size="compact-sm" leftSection={<IconArrowLeft size={14} />} onClick={() => navigate(-1)}>
          {t('common.back')}
        </Button>
        {user?.role === AppRole.Athlete && (
          <Button variant="default" size="compact-sm" leftSection={<IconPencil size={14} />} onClick={() => navigate('/races')}>
            {t('raceDetail.editOnRacesPage')}
          </Button>
        )}
      </Group>

      <div className={classes.hero} style={{ ['--race-color' as string]: color }}>
        <RaceBadgeIcon priority={race.priority} size={84} />
        <div className={classes.heroText}>
          <Group gap={8}>
            <Badge tone={priorityTone[race.priority ?? GoalPriority.C]}>{t('raceDetail.priority', { priority: race.priority })}</Badge>
            <Badge tone="info">{t(`sport.${race.sport}`)}</Badge>
          </Group>
          <Title order={2} className="ds-page-title">
            {race.name}
          </Title>
          <Group gap={14} c="var(--color-text-muted)">
            {starts && (
              <Text fz={14}>
                {capitalize(starts.toLocaleDateString('cs-CZ', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }))} ·{' '}
                {starts.toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' })}
              </Text>
            )}
            {race.location && (
              <Group gap={4}>
                <IconMapPin size={14} />
                <Text fz={14}>{race.location}</Text>
              </Group>
            )}
          </Group>
        </div>
        {countdown && (
          <div className={classes.countdown}>
            {days != null && days > 0 && <span className={classes.countdownNumber}>{days}</span>}
            <span className={classes.countdownLabel}>{days != null && days > 0 ? t('raceDetail.daysLeft', { count: days }) : countdown}</span>
          </div>
        )}
      </div>

      <Panel compact>
        <MetricStrip
          metrics={[
            { label: t('raceDetail.distance'), value: race.distanceMeters ? `${(race.distanceMeters / 1000).toLocaleString('cs-CZ', { maximumFractionDigits: 2 })} km` : '—' },
            { label: t('raceDetail.elevation'), value: race.elevationGainMeters ? `${race.elevationGainMeters} m` : '—' },
            { label: t('raceDetail.target'), value: target ?? '—', trend: race.targetResultNote ?? undefined },
            {
              label: t('raceDetail.result'),
              value: result ?? '—',
              trend: diff != null ? t(diff <= 0 ? 'raceDetail.underTarget' : 'raceDetail.overTarget', { time: formatRaceTime(Math.abs(diff)) }) : (race.actualResultNote ?? undefined),
              trendTone: diff != null ? (diff <= 0 ? 'positive' : 'warning') : 'neutral',
            },
          ]}
        />
        {(race.actualResultNote || race.resultNotes) && (
          <Stack gap={4} mt="sm">
            {race.actualResultNote && <Text className="ds-body">{race.actualResultNote}</Text>}
            {race.resultNotes && (
              <Text className="ds-metadata" style={{ whiteSpace: 'pre-wrap' }}>
                {race.resultNotes}
              </Text>
            )}
          </Stack>
        )}
      </Panel>

      <CommentThread raceId={raceId} />
    </Stack>
  );
}
