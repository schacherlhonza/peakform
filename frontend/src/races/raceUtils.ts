import { GoalPriority } from '../api/generated/models';

/** Priority colours, the same scale as the priority badges: A (main race) red, B orange, C muted. */
export const PRIORITY_COLOR: Record<GoalPriority, string> = {
  [GoalPriority.A]: 'var(--color-danger)',
  [GoalPriority.B]: 'var(--color-warning)',
  [GoalPriority.C]: 'var(--color-text-muted)',
};

export const priorityColor = (priority: GoalPriority | undefined) => PRIORITY_COLOR[priority ?? GoalPriority.C];

/** Whole calendar days from today to the race (local time); negative once it's over. */
export function daysUntil(startsAtUtc: string, today = new Date()): number {
  const start = new Date(startsAtUtc);
  const a = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
  const b = Date.UTC(start.getFullYear(), start.getMonth(), start.getDate());
  return Math.round((b - a) / 86_400_000);
}

/** The race's local calendar date as YYYY-MM-DD — where it sits in the week calendar. */
export function raceLocalDate(startsAtUtc: string): string {
  const d = new Date(startsAtUtc);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function formatRaceTime(seconds: number | null | undefined): string | null {
  if (seconds == null) return null;
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}` : `${m}:${String(s).padStart(2, '0')}`;
}
