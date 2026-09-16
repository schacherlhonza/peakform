import { useGetApiAthletesAthleteUserIdRecovery } from '../../api/generated/recovery-metrics/recovery-metrics';
import { useGetApiAthletesAthleteUserIdHealthFlags } from '../../api/generated/pain-or-health-flags/pain-or-health-flags';
import { useGetApiAthletesAthleteUserIdPlans, useGetApiPlansId } from '../../api/generated/training-plans/training-plans';
import { addDays, mondayOf, toIsoDate } from '../../calendar/dateUtils';

function latestByDate<T extends { date?: string }>(items: T[] | undefined): T | undefined {
  if (!items || items.length === 0) return undefined;
  return [...items].sort((a, b) => (b.date ?? '').localeCompare(a.date ?? ''))[0];
}

/** Per-athlete data for one AthleteStatusCard — kept in its own hook (not the parent) so each
 * card's queries mount/unmount cleanly as the coach's roster changes. */
export function useAthleteStatusData(athleteUserId: string) {
  const today = toIsoDate(new Date());
  const lookbackStart = toIsoDate(addDays(new Date(), -6));
  const weekStartIso = toIsoDate(mondayOf(new Date()));

  const recoveryQuery = useGetApiAthletesAthleteUserIdRecovery(athleteUserId, { from: lookbackStart, to: today });
  const healthFlagsQuery = useGetApiAthletesAthleteUserIdHealthFlags(athleteUserId, { activeOnly: true });
  const plansQuery = useGetApiAthletesAthleteUserIdPlans(athleteUserId);
  const activePlan = (plansQuery.data ?? []).find((p) => p.isActive) ?? [...(plansQuery.data ?? [])].sort((a, b) => (b.startDate ?? '').localeCompare(a.startDate ?? ''))[0];
  const planDetailQuery = useGetApiPlansId(activePlan?.id ?? '', { query: { enabled: !!activePlan?.id } });
  const week = planDetailQuery.data?.weeks?.find((w) => w.weekStartDate === weekStartIso);
  const todayWorkout = week?.workouts?.find((w) => w.date === today);

  const latestRecovery = latestByDate(recoveryQuery.data);
  const activeFlags = healthFlagsQuery.data ?? [];

  return {
    isLoading: recoveryQuery.isLoading || healthFlagsQuery.isLoading || plansQuery.isLoading,
    readinessScore: latestRecovery?.readinessScore ?? null,
    readinessDate: latestRecovery?.date ?? null,
    todayWorkoutTitle: todayWorkout ? (todayWorkout.isRestDay ? null : todayWorkout.title) : null,
    isRestDay: !!todayWorkout?.isRestDay,
    hasPlan: !!activePlan,
    activeHealthFlags: activeFlags,
  };
}
