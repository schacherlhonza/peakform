import { useMemo, useState } from 'react';
import { useGetApiAthletesAthleteUserIdPlanVsActual } from '../api/generated/training-plans/training-plans';
import type { PlanVsActualDto } from '../api/generated/models';
import type { BadgeTone } from '../design-system/components';
import { ZoneCompareBars } from './ZoneCompareBars';
import { formatClock, formatTotalDuration } from '../activities/activityFormat';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Checkbox, Select, Stack, Text, Textarea, TextInput, Group } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useDisclosure } from '@mantine/hooks';
import { IconCalendarOff, IconChevronLeft, IconChevronRight, IconPlus } from '@tabler/icons-react';
import {
  useGetApiAthletesAthleteUserIdPlans,
  useGetApiPlansId,
  getGetApiPlansIdQueryKey,
  getGetApiAthletesAthleteUserIdPlansQueryKey,
  getPostApiPlansMutationOptions,
  getPostApiPlansPlanIdWeeksMutationOptions,
  getPostApiWorkoutsMutationOptions,
} from '../api/generated/training-plans/training-plans';
import { useGetApiWorkoutTemplates } from '../api/generated/workout-templates/workout-templates';
import { SegmentList } from '../workouts/segments/SegmentList';
import { segmentTotals } from '../workouts/segments/segmentFormat';
import { SportType, type PlannedWorkoutDto } from '../api/generated/models';
import { Panel, CardHeader, Badge, Button, Modal, FormField, EmptyState, Skeleton, showToast } from '../design-system/components';
import { addDays, mondayOf, toIsoDate } from './dateUtils';
import classes from './WeekCalendar.module.css';

const sportOptions = Object.values(SportType).map((value) => ({ value, label: value }));

const createWorkoutSchema = z
  .object({
    date: z.date(),
    sport: z.nativeEnum(SportType),
    title: z.string().optional(),
    coachDescription: z.string().optional(),
    isRestDay: z.boolean(),
  })
  .refine((values) => values.isRestDay || !!values.title?.trim(), { path: ['title'], message: 'Required' });
type CreateWorkoutValues = z.infer<typeof createWorkoutSchema>;

const createPlanSchema = z.object({
  name: z.string().min(1),
  startDate: z.date(),
});
type CreatePlanValues = z.infer<typeof createPlanSchema>;

function WeekCalendarSkeleton() {
  return (
    <Panel>
      <Skeleton height={16} width={160} mb="md" />
      <div className={classes.dayGrid}>
        {Array.from({ length: 7 }, (_, i) => (
          <Skeleton key={i} height={120} radius="var(--radius-md)" />
        ))}
      </div>
    </Panel>
  );
}

/**
 * Weekly training calendar — athlete's own read-only view (CalendarPage) and coach's editable
 * view of one athlete (AthleteDetailPage). See docs/DESIGN_SYSTEM.md §7 "WeekTimeline" for the
 * visual spec (today gets an accent border, 730px+ horizontally-scrollable axis on mobile).
 */
type DayComparison = NonNullable<PlanVsActualDto['days']>[number];

function complianceTone(percent: number | null | undefined): BadgeTone {
  if (percent == null) return 'neutral';
  if (percent >= 80 && percent <= 120) return 'positive';
  if (percent >= 50 && percent <= 150) return 'warning';
  return 'danger';
}

function useOpenActivity() {
  const navigate = useNavigate();
  return (id?: string) => (e: React.MouseEvent | React.KeyboardEvent) => {
    e.stopPropagation();
    if (id) navigate(`/activities/${id}`);
  };
}

type WorkoutComparison = NonNullable<DayComparison['workouts']>[number];

/** What was done for one planned workout: the paired activity with duration compliance and
 * planned-vs-actual zones, or "missed" once the day is over. */
function WorkoutActual({ comparison, isPast }: { comparison?: WorkoutComparison; isPast: boolean }) {
  const { t } = useTranslation();
  const open = useOpenActivity();
  if (!comparison || comparison.isRestDay) return null;
  const actual = comparison.actual;
  if (!actual) return isPast ? <Badge tone="danger">{t('calendar.compare.missed')}</Badge> : null;
  const hasPlannedZones = (comparison.plannedZoneSeconds ?? []).some((s) => s > 0);
  return (
    <Stack gap={4}>
      <Group gap={6} wrap="nowrap" justify="space-between">
        <Text
          fz={12}
          lineClamp={1}
          role="link"
          tabIndex={0}
          title={actual.title ?? undefined}
          onClick={open(actual.activityId)}
          onKeyDown={(e) => (e.key === 'Enter' ? open(actual.activityId)(e) : undefined)}
          style={{ cursor: 'pointer', textDecoration: 'underline', textDecorationColor: 'rgba(255,255,255,.25)' }}
        >
          ✓ {formatClock(actual.durationSeconds ?? 0)}
        </Text>
        {comparison.durationCompliancePercent != null && (
          <Badge tone={complianceTone(comparison.durationCompliancePercent)}>{comparison.durationCompliancePercent} %</Badge>
        )}
      </Group>
      {comparison.sportMismatch && (
        <Text className="ds-metadata" lineClamp={1}>
          {t('calendar.compare.otherSport', { sport: t(`sport.${actual.sport}`) })}
        </Text>
      )}
      {hasPlannedZones && (
        <ZoneCompareBars
          plannedZones={comparison.plannedZoneSeconds ?? []}
          plannedUnspecified={comparison.plannedUnspecifiedSeconds ?? 0}
          actualZones={actual.zoneSeconds ?? []}
          actualBelow={actual.belowZonesSeconds ?? 0}
        />
      )}
    </Stack>
  );
}

/** Activities of the day that no planned workout claimed. */
function UnplannedActivities({ day }: { day?: DayComparison }) {
  const { t } = useTranslation();
  const open = useOpenActivity();
  return (day?.unplannedActivities ?? []).map((a) => (
    <Text
      key={a.activityId}
      className="ds-metadata"
      lineClamp={1}
      role="link"
      tabIndex={0}
      onClick={open(a.activityId)}
      onKeyDown={(e) => (e.key === 'Enter' ? open(a.activityId)(e) : undefined)}
      style={{ cursor: 'pointer' }}
    >
      + {a.title || t(`sport.${a.sport}`)} · {formatClock(a.durationSeconds ?? 0)}
    </Text>
  ));
}

/** Week totals: planned vs done (duration, distance, completed workouts) and zone distribution. */
function WeekComparison({ data }: { data: PlanVsActualDto }) {
  const { t } = useTranslation();
  const totals = data.totals;
  if (!totals || (totals.plannedWorkouts ?? 0) === 0 && (totals.actualDurationSeconds ?? 0) === 0) return null;
  const km = (m?: number | null) => `${((m ?? 0) / 1000).toFixed(1)} km`;
  const plannedDuration = totals.plannedDurationSeconds ?? 0;
  const plannedDistance = totals.plannedDistanceMeters ?? 0;
  const hasPlannedZones = (totals.plannedZoneSeconds ?? []).some((s) => s > 0);
  const hasZones = hasPlannedZones || (totals.actualZoneSeconds ?? []).some((s) => s > 0);
  return (
    <Stack gap="xs" mt="md">
      <Text className="ds-eyebrow">{t('calendar.compare.weekTitle')}</Text>
      <Group gap="lg" wrap="wrap">
        <Text className="ds-metadata">
          {t('calendar.compare.duration')}: <b>{formatTotalDuration(totals.actualDurationSeconds ?? 0)}</b>
          {plannedDuration > 0 && <> / {formatTotalDuration(plannedDuration)}</>}
        </Text>
        {((totals.actualDistanceMeters ?? 0) > 0 || plannedDistance > 0) && (
          <Text className="ds-metadata">
            {t('calendar.compare.distance')}: <b>{km(totals.actualDistanceMeters)}</b>
            {plannedDistance > 0 && <> / {km(plannedDistance)}</>}
          </Text>
        )}
        {data.actualAvailable && (
          <Text className="ds-metadata">
            {t('calendar.compare.completed')}: <b>{totals.completedWorkouts}</b> / {totals.plannedWorkouts}
          </Text>
        )}
      </Group>
      {hasZones && data.actualAvailable && (
        <ZoneCompareBars
          plannedZones={totals.plannedZoneSeconds ?? []}
          plannedUnspecified={totals.plannedUnspecifiedSeconds ?? 0}
          actualZones={totals.actualZoneSeconds ?? []}
          actualBelow={totals.actualBelowZonesSeconds ?? 0}
          height={12}
          showPlan={hasPlannedZones}
        />
      )}
    </Stack>
  );
}

export function WeekCalendar({ athleteUserId, canEdit }: { athleteUserId: string; canEdit: boolean }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure();
  const [createPlanOpened, { open: openCreatePlan, close: closeCreatePlan }] = useDisclosure();
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);

  const templatesQuery = useGetApiWorkoutTemplates({ query: { enabled: canEdit } });
  const templates = templatesQuery.data ?? [];
  const selectedTemplate = templates.find((tpl) => tpl.id === selectedTemplateId);

  const plansQuery = useGetApiAthletesAthleteUserIdPlans(athleteUserId);
  const plans = plansQuery.data ?? [];
  const activePlan = useMemo(
    () => plans.find((p) => p.isActive) ?? [...plans].sort((a, b) => (b.startDate ?? '').localeCompare(a.startDate ?? ''))[0],
    [plans],
  );

  const planDetailQuery = useGetApiPlansId(activePlan?.id ?? '', { query: { enabled: !!activePlan?.id } });
  const plan = planDetailQuery.data;

  const weekIso = toIsoDate(weekStart);
  const week = plan?.weeks?.find((w) => w.weekStartDate === weekIso);
  const todayIso = toIsoDate(new Date());

  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const comparisonQuery = useGetApiAthletesAthleteUserIdPlanVsActual(athleteUserId, { from: weekIso, to: toIsoDate(addDays(weekStart, 6)) });
  const comparison = comparisonQuery.data;
  const comparisonByDate = new Map((comparison?.days ?? []).map((d) => [d.date!, d]));
  const workoutsByDate = new Map<string, PlannedWorkoutDto[]>();
  for (const w of week?.workouts ?? []) {
    if (w.date) workoutsByDate.set(w.date, [...(workoutsByDate.get(w.date) ?? []), w]);
  }

  const createWeekMutation = useMutation(getPostApiPlansPlanIdWeeksMutationOptions());
  const createWorkoutMutation = useMutation(getPostApiWorkoutsMutationOptions());
  const createPlanMutation = useMutation(getPostApiPlansMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<CreateWorkoutValues>({
    resolver: zodResolver(createWorkoutSchema),
    defaultValues: { date: weekStart, sport: SportType.Running, title: '', isRestDay: false },
  });
  const isRestDay = watch('isRestDay');

  const {
    register: registerPlan,
    handleSubmit: handleSubmitPlan,
    control: controlPlan,
    reset: resetPlan,
    formState: { errors: planErrors, isSubmitting: isSubmittingPlan },
  } = useForm<CreatePlanValues>({
    resolver: zodResolver(createPlanSchema),
    defaultValues: { name: '', startDate: mondayOf(new Date()) },
  });

  const onSubmitPlan = handleSubmitPlan(async (values) => {
    try {
      await createPlanMutation.mutateAsync({
        data: { athleteUserId, name: values.name, startDate: toIsoDate(values.startDate) },
      });
      showToast({ tone: 'positive', message: t('calendar.createPlan') + ' ✓' });
      resetPlan();
      closeCreatePlan();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdPlansQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const onSubmit = handleSubmit(async (values) => {
    if (!activePlan?.id) return;
    const templateSegments = values.isRestDay ? [] : (selectedTemplate?.segments ?? []);
    const templateTotals = segmentTotals(templateSegments);
    try {
      let targetWeekId = week?.id;
      if (!targetWeekId) {
        const newWeek = await createWeekMutation.mutateAsync({
          planId: activePlan.id,
          data: { weekStartDate: weekIso, weekIndex: (plan?.weeks?.length ?? 0) + 1 },
        });
        targetWeekId = newWeek.id;
      }

      await createWorkoutMutation.mutateAsync({
        data: {
          trainingWeekId: targetWeekId,
          date: toIsoDate(values.date),
          sport: values.isRestDay ? SportType.Rest : values.sport,
          title: values.isRestDay ? t('calendar.restDay') : values.title,
          coachDescription: values.coachDescription ?? '',
          isRestDay: values.isRestDay,
          segments: templateSegments,
          plannedDurationSeconds: templateTotals.durationSeconds,
          plannedDistanceMeters: templateTotals.distanceMeters,
        },
      });

      showToast({ tone: 'positive', message: t('calendar.createWorkout') + ' ✓' });
      reset();
      setSelectedTemplateId(null);
      closeModal();
      await queryClient.invalidateQueries({ queryKey: getGetApiPlansIdQueryKey(activePlan.id) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  if (plansQuery.isLoading) return <WeekCalendarSkeleton />;

  if (!activePlan) {
    return (
      <>
        <Panel>
          <EmptyState
            icon={<IconCalendarOff size={28} stroke={1.6} />}
            title={t('calendar.noPlan')}
            action={canEdit ? <Button onClick={openCreatePlan}>{t('calendar.createPlan')}</Button> : undefined}
          />
        </Panel>

        {canEdit && (
          <Modal opened={createPlanOpened} onClose={closeCreatePlan} title={t('calendar.createPlan')}>
            <form onSubmit={onSubmitPlan}>
              <Stack gap="sm">
                <FormField label={t('calendar.planName')} error={planErrors.name?.message}>
                  <TextInput {...registerPlan('name')} />
                </FormField>
                <Controller
                  name="startDate"
                  control={controlPlan}
                  render={({ field }) => (
                    <FormField label={t('calendar.startDate')}>
                      <DateInput value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
                    </FormField>
                  )}
                />
                <Button type="submit" loading={isSubmittingPlan} fullWidth mt="sm">
                  {t('calendar.createPlan')}
                </Button>
              </Stack>
            </form>
          </Modal>
        )}
      </>
    );
  }

  return (
    <Panel>
      <CardHeader
        kicker={t('nav.calendar')}
        title={weekIso}
        right={
          <Stack gap={4} align="flex-end">
            <div style={{ display: 'flex', gap: 8 }}>
              <Button variant="default" size="compact-sm" leftSection={<IconChevronLeft size={14} />} onClick={() => setWeekStart(addDays(weekStart, -7))}>
                {t('calendar.previousWeek')}
              </Button>
              <Button variant="default" size="compact-sm" rightSection={<IconChevronRight size={14} />} onClick={() => setWeekStart(addDays(weekStart, 7))}>
                {t('calendar.nextWeek')}
              </Button>
            </div>
          </Stack>
        }
      />

      {canEdit && (
        <Button leftSection={<IconPlus size={16} />} onClick={openModal} mb="md">
          {t('calendar.addWorkout')}
        </Button>
      )}

      <div className={classes.dayGrid}>
        {days.map((day) => {
          const iso = toIsoDate(day);
          const dayWorkouts = workoutsByDate.get(iso) ?? [];
          const dayComparison = comparison?.actualAvailable ? comparisonByDate.get(iso) : undefined;
          const isToday = iso === todayIso;
          const dayClasses = [classes.day, isToday && classes.dayToday].filter(Boolean).join(' ');
          return (
            <div key={iso} className={dayClasses}>
              <Text className="ds-eyebrow">
                {t(`weekday.${day.getDay() === 0 ? 6 : day.getDay() - 1}`)} · {iso.slice(5)}
              </Text>
              {dayWorkouts.map((workout) => (
                <div
                  key={workout.id}
                  className={classes.workout}
                  role="button"
                  tabIndex={0}
                  onClick={() => workout.id && navigate(`/workouts/${workout.id}`)}
                  onKeyDown={(e) => {
                    if (workout.id && (e.key === 'Enter' || e.key === ' ')) navigate(`/workouts/${workout.id}`);
                  }}
                >
                  <div>
                    <Badge tone={workout.isRestDay ? 'neutral' : 'info'}>{t(`sport.${workout.sport}`)}</Badge>
                  </div>
                  <Text fz={13} fw={600} lineClamp={2}>
                    {workout.isRestDay ? t('calendar.restDay') : workout.title}
                  </Text>
                  <WorkoutActual
                    comparison={dayComparison?.workouts?.find((c) => c.workoutId === workout.id)}
                    isPast={iso < todayIso}
                  />
                </div>
              ))}
              {dayWorkouts.length === 0 && !dayComparison?.unplannedActivities?.length && <Text className="ds-metadata">—</Text>}
              <UnplannedActivities day={dayComparison} />
            </div>
          );
        })}
      </div>

      {comparison && <WeekComparison data={comparison} />}

      <Modal
        opened={modalOpened}
        onClose={() => {
          setSelectedTemplateId(null);
          closeModal();
        }}
        title={t('calendar.addWorkout')}
      >
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Controller
              name="date"
              control={control}
              render={({ field }) => (
                <FormField label={t('common.date')}>
                  <DateInput value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
                </FormField>
              )}
            />
            <Controller
              name="isRestDay"
              control={control}
              render={({ field }) => (
                <Checkbox label={t('calendar.isRestDay')} checked={field.value} onChange={(e) => field.onChange(e.currentTarget.checked)} />
              )}
            />
            {!isRestDay && (
              <>
                {templates.length > 0 && (
                  <FormField label={t('workout.startFromTemplate')}>
                    <Select
                      placeholder={t('workout.startFromTemplatePlaceholder')}
                      data={templates.map((tpl) => ({ value: tpl.id ?? '', label: tpl.name ?? '' }))}
                      value={selectedTemplateId}
                      onChange={(value) => {
                        setSelectedTemplateId(value);
                        const tpl = templates.find((x) => x.id === value);
                        if (tpl) {
                          setValue('title', tpl.name ?? '');
                          setValue('sport', tpl.sport ?? SportType.Running);
                          setValue('coachDescription', tpl.description ?? '');
                        }
                      }}
                      clearable
                    />
                  </FormField>
                )}
                {(selectedTemplate?.segments?.length ?? 0) > 0 && <SegmentList segments={selectedTemplate?.segments ?? []} compact />}
                <Controller
                  name="sport"
                  control={control}
                  render={({ field }) => (
                    <FormField label={t('workout.detail')}>
                      <Select data={sportOptions} {...field} />
                    </FormField>
                  )}
                />
                <FormField label={t('calendar.title')} error={errors.title?.message}>
                  <TextInput {...register('title')} />
                </FormField>
              </>
            )}
            <FormField label={t('calendar.description')}>
              <Textarea minRows={3} {...register('coachDescription')} />
            </FormField>
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('calendar.createWorkout')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Panel>
  );
}
