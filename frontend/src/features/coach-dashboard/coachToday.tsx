import type { CoachTodayAthleteDto } from '../../api/generated/models';

/** "Needs a look": an active health flag outranks low readiness. */
export function attentionLevel(a: CoachTodayAthleteDto): 'danger' | 'warning' | null {
  if ((a.activeHealthFlags?.length ?? 0) > 0) return 'danger';
  const score = a.readiness?.score;
  if (score != null && score < 50) return 'warning';
  return null;
}
