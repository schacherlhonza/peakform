import { useTranslation } from 'react-i18next';
import { Text } from '@mantine/core';
import type { WorkoutSegmentDto } from '../../api/generated/models';
import { formatClock } from '../../activities/activityFormat';
import { describeSegment, formatSegmentDistance, isBlock, segmentTotals } from './segmentFormat';
import classes from './Segments.module.css';

const byOrder = (segments: readonly WorkoutSegmentDto[]) => [...segments].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

/** Read-only structure: one line per step, a repeat block's steps indented under it, plus the planned total. `compact` hides notes (list rows). */
export function SegmentList({ segments, compact = false }: { segments: readonly WorkoutSegmentDto[]; compact?: boolean }) {
  const { t } = useTranslation();
  const sorted = byOrder(segments);
  const totals = segmentTotals(sorted);
  const total = [totals.durationSeconds ? formatClock(totals.durationSeconds) : null, totals.distanceMeters ? formatSegmentDistance(totals.distanceMeters) : null]
    .filter(Boolean)
    .join(' · ');

  const item = (s: WorkoutSegmentDto, i: number) => (
    <li key={s.id ?? i} className={classes.listItem}>
      <Text fz={13} fw={isBlock(s) ? 600 : 500}>
        {describeSegment(s, t)}
      </Text>
      {!compact && s.notes && <Text className="ds-metadata">{s.notes}</Text>}
      {isBlock(s) && <ol className={`${classes.listItems} ${classes.listBlock}`}>{byOrder(s.steps ?? []).map(item)}</ol>}
    </li>
  );

  return (
    <div className={classes.list}>
      <ol className={classes.listItems}>{sorted.map(item)}</ol>
      {total && (
        <Text className="ds-metadata">
          {t('workout.structureTotal')}: {total}
        </Text>
      )}
    </div>
  );
}
