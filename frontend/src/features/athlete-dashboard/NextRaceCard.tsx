import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, Text } from '@mantine/core';
import { IconChevronRight, IconFlag, IconMapPin } from '@tabler/icons-react';
import { Badge, Button, CardHeader, EmptyState, Panel, Skeleton, type BadgeTone } from '../../design-system/components';
import { GoalPriority, type RaceDto } from '../../api/generated/models';
import { RaceBadgeIcon } from '../../races/RaceBadgeIcon';
import { daysUntil, formatRaceTime, priorityColor } from '../../races/raceUtils';
import classes from './NextRaceCard.module.css';

export interface NextRaceCardData {
  /** Upcoming races, soonest first; the first one is featured. */
  upcoming: RaceDto[];
  isLoading: boolean;
}

const priorityTone: Record<GoalPriority, BadgeTone> = { [GoalPriority.A]: 'danger', [GoalPriority.B]: 'warning', [GoalPriority.C]: 'neutral' };

type Phase = 'build' | 'sharpen' | 'raceWeek' | 'raceDay';
const PHASES: Phase[] = ['build', 'sharpen', 'raceWeek', 'raceDay'];

/** Where the athlete is on the way to the start: building (>3 weeks), sharpening (1–3 weeks), race week, race day. */
function phaseOf(days: number): Phase {
  if (days <= 0) return 'raceDay';
  if (days <= 7) return 'raceWeek';
  if (days <= 21) return 'sharpen';
  return 'build';
}

/** The next race as a motivating countdown: priority-coloured trophy, days to go, the course, and the build-up phase. */
export function NextRaceCard({ data }: { data: NextRaceCardData }) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  if (data.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={140} mb="md" />
        <Skeleton height={140} />
      </Panel>
    );
  }

  const race = data.upcoming[0];
  if (!race?.startsAtUtc) {
    return (
      <Panel>
        <CardHeader kicker={t('dashboard.nextRace')} />
        <EmptyState
          icon={<IconFlag size={28} stroke={1.6} />}
          title={t('nextRace.noneTitle')}
          description={t('nextRace.noneDescription')}
          action={<Button onClick={() => navigate('/races')}>{t('races.addRace')}</Button>}
        />
      </Panel>
    );
  }

  const color = priorityColor(race.priority);
  const days = Math.max(0, daysUntil(race.startsAtUtc));
  const phase = phaseOf(days);
  const starts = new Date(race.startsAtUtc);
  const date = starts.toLocaleDateString('cs-CZ', { weekday: 'long', day: 'numeric', month: 'long' });
  const target = formatRaceTime(race.targetTimeSeconds);
  const others = data.upcoming.length - 1;

  return (
    <div className={classes.card} style={{ ['--race-color' as string]: color }}>
      <CardHeader kicker={t('dashboard.nextRace')} right={<Badge tone={priorityTone[race.priority ?? GoalPriority.C]}>{t('raceDetail.priority', { priority: race.priority })}</Badge>} />

      <button type="button" className={classes.hero} onClick={() => race.id && navigate(`/races/${race.id}`)}>
        <RaceBadgeIcon priority={race.priority} size={68} />
        <div className={classes.heroText}>
          <Text fw={800} fz={20} lh={1.2} lineClamp={2}>
            {race.name}
          </Text>
          <Group gap={10} c="var(--color-text-muted)">
            <Text fz={13}>
              {date.charAt(0).toUpperCase() + date.slice(1)} · {starts.toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' })}
            </Text>
            {race.location && (
              <Group gap={3}>
                <IconMapPin size={13} />
                <Text fz={13}>{race.location}</Text>
              </Group>
            )}
          </Group>
        </div>
        <IconChevronRight size={18} className={classes.chevron} />
      </button>

      <div className={classes.stats}>
        <div className={classes.countdown}>
          <span className={classes.countdownNumber}>{days === 0 ? <IconFlag size={36} stroke={2} /> : days}</span>
          <span className={classes.statLabel}>{days === 0 ? t('raceDetail.raceDay') : t('raceDetail.daysLeft', { count: days })}</span>
        </div>
        <div className={classes.stat}>
          <span className={classes.statValue}>
            {race.distanceMeters ? `${(race.distanceMeters / 1000).toLocaleString('cs-CZ', { maximumFractionDigits: 1 })} km` : '—'}
          </span>
          <span className={classes.statLabel}>{t('nextRace.course')}</span>
        </div>
        <div className={classes.stat}>
          <span className={classes.statValue}>{target ?? '—'}</span>
          <span className={classes.statLabel}>{t('raceDetail.target')}</span>
        </div>
      </div>

      {/* The build-up as a track: done phases filled, the current one glowing. */}
      <div className={classes.phases} role="list" aria-label={t('nextRace.phasesLabel')}>
        {PHASES.map((p, i) => {
          const current = PHASES.indexOf(phase);
          const state = i < current ? classes.phaseDone : i === current ? classes.phaseCurrent : '';
          return (
            <div key={p} role="listitem" className={`${classes.phase} ${state}`} aria-current={i === current ? 'step' : undefined}>
              <span className={classes.phaseBar} />
              <span className={classes.phaseLabel}>{t(`nextRace.phase.${p}`)}</span>
            </div>
          );
        })}
      </div>
      <Text className={classes.motivation}>{t(`nextRace.motivation.${phase}`)}</Text>

      {others > 0 && (
        <Text className="ds-metadata" mt={6}>
          {t('nextRace.more', { count: others, name: data.upcoming[1]?.name ?? '' })}
        </Text>
      )}
    </div>
  );
}
