import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Text } from '@mantine/core';
import type { RaceDto } from '../api/generated/models';
import { RaceBadgeIcon } from './RaceBadgeIcon';
import { daysUntil, formatRaceTime, priorityColor } from './raceUtils';
import classes from './RaceCalendarCard.module.css';

/** A race in the week calendar: priority-tinted card with the trophy, start time and a countdown or the result. */
export function RaceCalendarCard({ race }: { race: RaceDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const color = priorityColor(race.priority);
  const days = race.startsAtUtc ? daysUntil(race.startsAtUtc) : null;
  const start = race.startsAtUtc ? new Date(race.startsAtUtc).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' }) : null;
  const result = formatRaceTime(race.actualTimeSeconds);
  const open = () => race.id && navigate(`/races/${race.id}`);

  const status =
    result != null
      ? t('raceDetail.resultShort', { time: result })
      : days == null
        ? null
        : days > 0
          ? t('raceDetail.inDays', { count: days })
          : days === 0
            ? t('raceDetail.raceDay')
            : null;

  return (
    <div
      className={classes.card}
      style={{ ['--race-color' as string]: color }}
      role="button"
      tabIndex={0}
      onClick={(e) => {
        e.stopPropagation();
        open();
      }}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') open();
      }}
    >
      <div className={classes.top}>
        <RaceBadgeIcon priority={race.priority} size={30} />
        <Text className={classes.kicker}>{t('raceDetail.kicker', { priority: race.priority })}</Text>
      </div>
      <Text fz={13} fw={800} lineClamp={2}>
        {race.name}
      </Text>
      <Text className="ds-metadata">
        {[start, race.distanceMeters ? `${(race.distanceMeters / 1000).toLocaleString('cs-CZ', { maximumFractionDigits: 1 })} km` : null].filter(Boolean).join(' · ')}
      </Text>
      {status && <span className={classes.status}>{status}</span>}
    </div>
  );
}
