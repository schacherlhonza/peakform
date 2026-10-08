import { useMemo } from 'react';
import { useGetApiAthletesAthleteUserIdRaces } from '../../api/generated/races/races';
import { useGetApiAthletesAthleteUserIdReportsDateType } from '../../api/generated/reports/reports';
import { useGetApiAthletesAthleteUserIdReadiness } from '../../api/generated/readiness/readiness';
import { useGetApiAthletesAthleteUserIdTrainingLoad } from '../../api/generated/training-load/training-load';
import { useGetApiAthletesAthleteUserIdPlanVsActual, useGetApiAthletesAthleteUserIdPlans, useGetApiPlansId } from '../../api/generated/training-plans/training-plans';
import { useGetApiAthletesAthleteUserIdActivities } from '../../api/generated/activities/activities';
import { useGetApiAthletesAthleteUserIdFood } from '../../api/generated/food-entries/food-entries';
import { useGetApiAthletesAthleteUserIdHydration } from '../../api/generated/hydration-entries/hydration-entries';
import { useGetApiIntegrations } from '../../api/generated/integration-connections/integration-connections';
import { ReportType } from '../../api/generated/models';
import { addDays, mondayOf, toIsoDate } from '../../calendar/dateUtils';

function todayIso(): string {
  return toIsoDate(new Date());
}

/** Most-recent-first pick within a lookback window — used so "today has no sync yet" degrades
 * to the latest known reading instead of an empty card, matching how the backend's own report
 * calculator (ReportMetricsCalculator) already treats these metrics. */
function latestByDate<T extends { date?: string }>(items: T[] | undefined): T | undefined {
  if (!items || items.length === 0) return undefined;
  return [...items].sort((a, b) => (b.date ?? '').localeCompare(a.date ?? ''))[0];
}

/**
 * Composes every real data source the athlete dashboard needs. Business logic (finding today's
 * workout, picking the latest recovery reading, summing today's nutrition) lives here — the
 * card components only render what this hook returns (docs/DESIGN_SYSTEM.md §11).
 */
export function useAthleteDashboardData(athleteUserId: string) {
  const today = todayIso();
  const weekStart = mondayOf(new Date());
  const weekStartIso = toIsoDate(weekStart);
  const lookbackStart = toIsoDate(addDays(new Date(), -6));

  const racesQuery = useGetApiAthletesAthleteUserIdRaces(athleteUserId);
  const morningReportQuery = useGetApiAthletesAthleteUserIdReportsDateType(athleteUserId, today, ReportType.Morning);
  const eveningReportQuery = useGetApiAthletesAthleteUserIdReportsDateType(athleteUserId, today, ReportType.Evening);

  // Source selection, baselines and the score itself are resolved server-side (ReadinessService).
  const readinessQuery = useGetApiAthletesAthleteUserIdReadiness(athleteUserId, { date: today });
  const trainingLoadQuery = useGetApiAthletesAthleteUserIdTrainingLoad(athleteUserId, { from: lookbackStart, to: today });
  const integrationsQuery = useGetApiIntegrations();

  const plansQuery = useGetApiAthletesAthleteUserIdPlans(athleteUserId);
  const plans = plansQuery.data ?? [];
  const activePlan = useMemo(
    () => plans.find((p) => p.isActive) ?? [...plans].sort((a, b) => (b.startDate ?? '').localeCompare(a.startDate ?? ''))[0],
    [plans],
  );
  const planDetailQuery = useGetApiPlansId(activePlan?.id ?? '', { query: { enabled: !!activePlan?.id } });
  const week = planDetailQuery.data?.weeks?.find((w) => w.weekStartDate === weekStartIso);
  const todayWorkout = week?.workouts?.find((w) => w.date === today);

  const activitiesQuery = useGetApiAthletesAthleteUserIdActivities(athleteUserId, { from: weekStartIso, to: today });
  // Today's plan paired with what was done — compliance and time in zones for the today card.
  const todayComparisonQuery = useGetApiAthletesAthleteUserIdPlanVsActual(athleteUserId, { from: today, to: today });
  const todayComparison = todayComparisonQuery.data?.days?.find((d) => d.date === today);

  const foodQuery = useGetApiAthletesAthleteUserIdFood(athleteUserId, { from: `${today}T00:00:00`, to: `${today}T23:59:59` });
  const hydrationQuery = useGetApiAthletesAthleteUserIdHydration(athleteUserId, { from: `${today}T00:00:00`, to: `${today}T23:59:59` });

  const upcomingRaces = useMemo(() => {
    const now = Date.now();
    return (racesQuery.data ?? [])
      .filter((r) => r.startsAtUtc && new Date(r.startsAtUtc).getTime() >= now - 12 * 3_600_000) // still "next" on race day
      .sort((a, b) => new Date(a.startsAtUtc!).getTime() - new Date(b.startsAtUtc!).getTime());
  }, [racesQuery.data]);

  const latestTrainingLoad = latestByDate(trainingLoadQuery.data);

  const syncedIntegration = (integrationsQuery.data ?? []).find((c) => c.lastSyncedAtUtc);
  const lastSyncedAtUtc = syncedIntegration?.lastSyncedAtUtc ?? null;

  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const activities = activitiesQuery.data ?? [];

  const carbsToday = (foodQuery.data ?? []).reduce((sum, e) => sum + (e.estimatedCarbsGrams ?? 0), 0);
  const hydrationToday = (hydrationQuery.data ?? []).reduce((sum, e) => sum + (e.volumeMilliliters ?? 0), 0);

  const latestInsightReport = eveningReportQuery.data?.insights?.length ? eveningReportQuery.data : morningReportQuery.data;

  return {
    isLoading: readinessQuery.isLoading,
    readiness: {
      readiness: readinessQuery.data ?? null,
      lastSyncedAtUtc,
      isLoading: readinessQuery.isLoading,
      isError: readinessQuery.isError,
      refetch: () => void readinessQuery.refetch(),
    },
    trainingLoad: {
      ctl: latestTrainingLoad?.ctl ?? null,
      atl: latestTrainingLoad?.atl ?? null,
      rampRate: latestTrainingLoad?.rampRate ?? null,
      date: latestTrainingLoad?.date ?? null,
      isToday: latestTrainingLoad?.date === today,
      isLoading: trainingLoadQuery.isLoading,
      isError: trainingLoadQuery.isError,
      refetch: () => void trainingLoadQuery.refetch(),
    },
    todayWorkout: {
      workout: todayWorkout ?? null,
      comparison: todayComparison?.workouts?.find((c) => c.workoutId === todayWorkout?.id) ?? null,
      activities: (activitiesQuery.data ?? []).filter((a) => a.startedAtUtc && toIsoDate(new Date(a.startedAtUtc)) === today),
      isLoading: plansQuery.isLoading || planDetailQuery.isLoading,
      isError: planDetailQuery.isError,
      hasPlan: !!activePlan,
    },
    fuelHydration: {
      carbsGrams: carbsToday,
      hydrationMilliliters: hydrationToday,
      foodEntryCount: foodQuery.data?.length ?? 0,
      hydrationEntryCount: hydrationQuery.data?.length ?? 0,
      isLoading: foodQuery.isLoading || hydrationQuery.isLoading,
    },
    weekTimeline: {
      days,
      week: week ?? null,
      activities,
      isLoading: planDetailQuery.isLoading || activitiesQuery.isLoading,
      isError: activitiesQuery.isError,
    },
    insight: {
      report: latestInsightReport ?? null,
      isLoading: morningReportQuery.isLoading || eveningReportQuery.isLoading,
    },
    checkIns: {
      morningReport: morningReportQuery.data ?? null,
      eveningReport: eveningReportQuery.data ?? null,
      morningIsLoading: morningReportQuery.isLoading,
      eveningIsLoading: eveningReportQuery.isLoading,
    },
    nextRace: {
      upcoming: upcomingRaces,
      isLoading: racesQuery.isLoading,
    },
  };
}
