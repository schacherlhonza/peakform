import { useMemo, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Checkbox, Select, Stack, Text, Textarea, TextInput } from '@mantine/core';
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
  const workoutsByDate = new Map<string, PlannedWorkoutDto>();
  for (const w of week?.workouts ?? []) {
    if (w.date) workoutsByDate.set(w.date, w);
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
          segments: values.isRestDay ? [] : (selectedTemplate?.segments ?? []),
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
          const workout = workoutsByDate.get(iso);
          const isToday = iso === todayIso;
          const clickable = !!workout?.id;
          const dayClasses = [classes.day, isToday && classes.dayToday, clickable && classes.dayClickable].filter(Boolean).join(' ');
          return (
            <div
              key={iso}
              className={dayClasses}
              role={clickable ? 'button' : undefined}
              tabIndex={clickable ? 0 : undefined}
              onClick={() => workout?.id && navigate(`/workouts/${workout.id}`)}
              onKeyDown={(e) => {
                if (clickable && (e.key === 'Enter' || e.key === ' ')) navigate(`/workouts/${workout!.id}`);
              }}
            >
              <Text className="ds-eyebrow">
                {t(`weekday.${day.getDay() === 0 ? 6 : day.getDay() - 1}`)} · {iso.slice(5)}
              </Text>
              {workout ? (
                <Stack gap={4}>
                  <Badge tone={workout.isRestDay ? 'neutral' : 'info'}>{t(`sport.${workout.sport}`)}</Badge>
                  <Text fz={13} fw={600} lineClamp={2}>
                    {workout.isRestDay ? t('calendar.restDay') : workout.title}
                  </Text>
                </Stack>
              ) : (
                <Text className="ds-metadata">—</Text>
              )}
            </div>
          );
        })}
      </div>

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
