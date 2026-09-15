import { useMemo, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  Group,
  Loader,
  Modal,
  Select,
  SimpleGrid,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconChevronLeft, IconChevronRight, IconPlus } from '@tabler/icons-react';
import {
  useGetApiAthletesAthleteUserIdPlans,
  useGetApiPlansId,
  getGetApiPlansIdQueryKey,
  getPostApiPlansPlanIdWeeksMutationOptions,
  getPostApiWorkoutsMutationOptions,
} from '../api/generated/training-plans/training-plans';
import { SportType, type PlannedWorkoutDto } from '../api/generated/models';
import { addDays, mondayOf, toIsoDate } from './dateUtils';

const sportOptions = Object.values(SportType).map((value) => ({ value, label: value }));

const createWorkoutSchema = z.object({
  date: z.date(),
  sport: z.nativeEnum(SportType),
  title: z.string().min(1),
  coachDescription: z.string().optional(),
  isRestDay: z.boolean(),
});
type CreateWorkoutValues = z.infer<typeof createWorkoutSchema>;

export function WeekCalendar({ athleteUserId, canEdit }: { athleteUserId: string; canEdit: boolean }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure();

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

  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const workoutsByDate = new Map<string, PlannedWorkoutDto>();
  for (const w of week?.workouts ?? []) {
    if (w.date) workoutsByDate.set(w.date, w);
  }

  const createWeekMutation = useMutation(getPostApiPlansPlanIdWeeksMutationOptions());
  const createWorkoutMutation = useMutation(getPostApiWorkoutsMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<CreateWorkoutValues>({
    resolver: zodResolver(createWorkoutSchema),
    defaultValues: { date: weekStart, sport: SportType.Running, title: '', isRestDay: false },
  });
  const isRestDay = watch('isRestDay');

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
          sport: values.sport,
          title: values.isRestDay ? t('calendar.restDay') : values.title,
          coachDescription: values.coachDescription ?? '',
          isRestDay: values.isRestDay,
        },
      });

      notifications.show({ color: 'green', message: 'Trénink byl vytvořen.' });
      reset();
      closeModal();
      await queryClient.invalidateQueries({ queryKey: getGetApiPlansIdQueryKey(activePlan.id) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  if (plansQuery.isLoading) return <Loader />;

  if (!activePlan) {
    return (
      <Card withBorder radius="md" p="xl">
        <Text c="dimmed" ta="center">
          {t('calendar.noPlan')}
        </Text>
      </Card>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Group>
          <Button variant="subtle" leftSection={<IconChevronLeft size={16} />} onClick={() => setWeekStart(addDays(weekStart, -7))}>
            {t('calendar.previousWeek')}
          </Button>
          <Title order={4}>{weekIso}</Title>
          <Button variant="subtle" rightSection={<IconChevronRight size={16} />} onClick={() => setWeekStart(addDays(weekStart, 7))}>
            {t('calendar.nextWeek')}
          </Button>
        </Group>
        {canEdit && (
          <Button leftSection={<IconPlus size={16} />} onClick={openModal}>
            {t('calendar.addWorkout')}
          </Button>
        )}
      </Group>

      <SimpleGrid cols={{ base: 1, sm: 2, md: 4, lg: 7 }} spacing="sm">
        {days.map((day) => {
          const iso = toIsoDate(day);
          const workout = workoutsByDate.get(iso);
          return (
            <Card
              key={iso}
              withBorder
              radius="md"
              p="sm"
              style={{ cursor: workout ? 'pointer' : 'default', minHeight: 110 }}
              onClick={() => workout?.id && navigate(`/workouts/${workout.id}`)}
            >
              <Text size="xs" c="dimmed">
                {t(`weekday.${day.getDay() === 0 ? 6 : day.getDay() - 1}`)} · {iso.slice(5)}
              </Text>
              {workout ? (
                <Stack gap={4} mt={4}>
                  <Badge size="sm" variant="light">
                    {t(`sport.${workout.sport}`)}
                  </Badge>
                  <Text size="sm" fw={500} lineClamp={2}>
                    {workout.isRestDay ? t('calendar.restDay') : workout.title}
                  </Text>
                </Stack>
              ) : (
                <Text size="xs" c="dimmed" mt={4}>
                  —
                </Text>
              )}
            </Card>
          );
        })}
      </SimpleGrid>

      <Modal opened={modalOpened} onClose={closeModal} title={t('calendar.addWorkout')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Controller
              name="date"
              control={control}
              render={({ field }) => (
                <DateInput label={t('common.date')} value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
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
                <Controller
                  name="sport"
                  control={control}
                  render={({ field }) => <Select label={t('workout.detail')} data={sportOptions} {...field} />}
                />
                <TextInput label={t('calendar.title')} error={errors.title?.message} {...register('title')} />
                <Textarea label={t('calendar.description')} minRows={3} {...register('coachDescription')} />
              </>
            )}
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('calendar.createWorkout')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
