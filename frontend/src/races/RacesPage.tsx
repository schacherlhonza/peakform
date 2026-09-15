import { useMemo, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  Badge,
  Button,
  Card,
  Group,
  Loader,
  Modal,
  NumberInput,
  Select,
  SimpleGrid,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core';
import { DateInput, DateTimePicker } from '@mantine/dates';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconFlag, IconPlus, IconTrophy } from '@tabler/icons-react';
import {
  useGetApiAthletesAthleteUserIdGoals,
  getGetApiAthletesAthleteUserIdGoalsQueryKey,
  getPostApiGoalsMutationOptions,
  getPutApiGoalsIdMutationOptions,
} from '../api/generated/goals/goals';
import {
  useGetApiAthletesAthleteUserIdRaces,
  getGetApiAthletesAthleteUserIdRacesQueryKey,
  getPostApiRacesMutationOptions,
  getPutApiRacesIdMutationOptions,
} from '../api/generated/races/races';
import { GoalPriority, SportType, type GoalDto, type RaceDto } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { toIsoDate } from '../calendar/dateUtils';

const sportOptions = Object.values(SportType).map((value) => ({ value, label: value }));
const priorityOptions = Object.values(GoalPriority).map((value) => ({ value, label: value }));

const priorityColor: Record<string, string> = {
  [GoalPriority.A]: 'red',
  [GoalPriority.B]: 'yellow',
  [GoalPriority.C]: 'gray',
};

function parseDuration(input: string | undefined): number | undefined {
  if (!input || !input.trim()) return undefined;
  const parts = input.split(':').map((p) => Number(p));
  if (parts.some((p) => Number.isNaN(p))) return undefined;
  if (parts.length === 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
  if (parts.length === 2) return parts[0] * 60 + parts[1];
  return parts[0];
}

function formatDuration(seconds: number | null | undefined): string {
  if (seconds == null) return '';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

const goalSchema = z.object({
  title: z.string().min(1),
  description: z.string().optional(),
  targetDate: z.date().optional().nullable(),
  priority: z.nativeEnum(GoalPriority),
});
type GoalFormValues = z.infer<typeof goalSchema>;

const raceSchema = z.object({
  name: z.string().min(1),
  sport: z.nativeEnum(SportType),
  startsAt: z.date(),
  location: z.string().optional(),
  distanceMeters: z.number().optional(),
  elevationGainMeters: z.number().optional(),
  priority: z.nativeEnum(GoalPriority),
  targetTime: z.string().optional(),
});
type RaceFormValues = z.infer<typeof raceSchema>;

const resultSchema = z.object({
  actualTime: z.string().optional(),
  actualResultNote: z.string().optional(),
  resultNotes: z.string().optional(),
});
type ResultFormValues = z.infer<typeof resultSchema>;

function GoalsSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [opened, { open, close }] = useDisclosure();

  const goalsQuery = useGetApiAthletesAthleteUserIdGoals(athleteUserId);
  const goals = useMemo(
    () => [...(goalsQuery.data ?? [])].sort((a, b) => (a.targetDate ?? '9999').localeCompare(b.targetDate ?? '9999')),
    [goalsQuery.data],
  );

  const createMutation = useMutation(getPostApiGoalsMutationOptions());
  const achieveMutation = useMutation(getPutApiGoalsIdMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<GoalFormValues>({
    resolver: zodResolver(goalSchema),
    defaultValues: { title: '', description: '', priority: GoalPriority.B },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({
        data: {
          athleteUserId,
          title: values.title,
          description: values.description || undefined,
          targetDate: values.targetDate ? toIsoDate(values.targetDate) : undefined,
          priority: values.priority,
        },
      });
      notifications.show({ color: 'green', message: t('races.goalCreated') });
      reset();
      close();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdGoalsQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const markAchieved = async (goal: GoalDto) => {
    if (!goal.id) return;
    try {
      await achieveMutation.mutateAsync({
        id: goal.id,
        data: {
          seasonId: goal.seasonId,
          title: goal.title,
          description: goal.description,
          targetDate: goal.targetDate,
          priority: goal.priority,
          isAchieved: true,
        },
      });
      notifications.show({ color: 'green', message: t('races.goalAchieved') });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdGoalsQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Card withBorder radius="md" p="lg">
      <Group justify="space-between" mb="sm">
        <Title order={4}>{t('races.goalsSection')}</Title>
        <Button size="xs" leftSection={<IconPlus size={16} />} onClick={open}>
          {t('races.addGoal')}
        </Button>
      </Group>

      {goalsQuery.isLoading ? (
        <Loader size="sm" />
      ) : goals.length === 0 ? (
        <Text c="dimmed" size="sm">
          {t('races.noGoals')}
        </Text>
      ) : (
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
          {goals.map((goal) => (
            <Card key={goal.id} withBorder radius="sm" p="md">
              <Group justify="space-between" mb={4}>
                <Text fw={600} size="sm">
                  {goal.title}
                </Text>
                <Badge color={priorityColor[goal.priority ?? GoalPriority.C]} variant="light">
                  {goal.priority}
                </Badge>
              </Group>
              {goal.description && (
                <Text size="xs" c="dimmed" lineClamp={3} mb={4}>
                  {goal.description}
                </Text>
              )}
              {goal.targetDate && (
                <Text size="xs" c="dimmed">
                  {t('races.targetDate')}: {goal.targetDate}
                </Text>
              )}
              <Group justify="space-between" mt="sm">
                {goal.isAchieved ? (
                  <Badge color="green" variant="light">
                    {t('races.achieved')}
                  </Badge>
                ) : (
                  <Button size="xs" variant="light" onClick={() => markAchieved(goal)} loading={achieveMutation.isPending}>
                    {t('races.markAchieved')}
                  </Button>
                )}
              </Group>
            </Card>
          ))}
        </SimpleGrid>
      )}

      <Modal opened={opened} onClose={close} title={t('races.addGoal')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <TextInput label={t('races.goalTitle')} error={errors.title?.message} {...register('title')} />
            <Textarea label={t('races.goalDescription')} minRows={2} {...register('description')} />
            <Controller
              name="targetDate"
              control={control}
              render={({ field }) => (
                <DateInput
                  label={t('races.targetDate')}
                  value={field.value ?? null}
                  onChange={(v) => field.onChange(v ? new Date(v) : null)}
                  clearable
                />
              )}
            />
            <Controller
              name="priority"
              control={control}
              render={({ field }) => <Select label={t('races.priority')} data={priorityOptions} {...field} />}
            />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('races.createGoal')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Card>
  );
}

function RacesSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [createOpened, { open: openCreate, close: closeCreate }] = useDisclosure();
  const [resultOpened, { open: openResult, close: closeResult }] = useDisclosure();
  const [selectedRace, setSelectedRace] = useState<RaceDto | null>(null);

  const racesQuery = useGetApiAthletesAthleteUserIdRaces(athleteUserId);
  const races = useMemo(
    () => [...(racesQuery.data ?? [])].sort((a, b) => (a.startsAtUtc ?? '').localeCompare(b.startsAtUtc ?? '')),
    [racesQuery.data],
  );

  const createMutation = useMutation(getPostApiRacesMutationOptions());
  const updateMutation = useMutation(getPutApiRacesIdMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<RaceFormValues>({
    resolver: zodResolver(raceSchema),
    defaultValues: { name: '', sport: SportType.Running, startsAt: new Date(), priority: GoalPriority.B },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({
        data: {
          athleteUserId,
          name: values.name,
          sport: values.sport,
          startsAtUtc: values.startsAt.toISOString(),
          location: values.location || undefined,
          distanceMeters: values.distanceMeters,
          elevationGainMeters: values.elevationGainMeters,
          priority: values.priority,
          targetTimeSeconds: parseDuration(values.targetTime),
        },
      });
      notifications.show({ color: 'green', message: t('races.raceCreated') });
      reset();
      closeCreate();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdRacesQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const {
    register: registerResult,
    handleSubmit: handleResultSubmit,
    reset: resetResult,
    formState: { isSubmitting: isSubmittingResult },
  } = useForm<ResultFormValues>({ resolver: zodResolver(resultSchema) });

  const openResultModal = (race: RaceDto) => {
    setSelectedRace(race);
    resetResult({
      actualTime: formatDuration(race.actualTimeSeconds),
      actualResultNote: race.actualResultNote ?? '',
      resultNotes: race.resultNotes ?? '',
    });
    openResult();
  };

  const onResultSubmit = handleResultSubmit(async (values) => {
    if (!selectedRace?.id) return;
    try {
      await updateMutation.mutateAsync({
        id: selectedRace.id,
        data: {
          seasonId: selectedRace.seasonId,
          goalId: selectedRace.goalId,
          name: selectedRace.name,
          sport: selectedRace.sport,
          startsAtUtc: selectedRace.startsAtUtc,
          location: selectedRace.location,
          distanceMeters: selectedRace.distanceMeters,
          elevationGainMeters: selectedRace.elevationGainMeters,
          priority: selectedRace.priority,
          targetTimeSeconds: selectedRace.targetTimeSeconds,
          targetResultNote: selectedRace.targetResultNote,
          actualTimeSeconds: parseDuration(values.actualTime),
          actualResultNote: values.actualResultNote || undefined,
          resultNotes: values.resultNotes || undefined,
        },
      });
      notifications.show({ color: 'green', message: t('races.resultSaved') });
      closeResult();
      setSelectedRace(null);
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdRacesQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const now = Date.now();

  return (
    <Card withBorder radius="md" p="lg">
      <Group justify="space-between" mb="sm">
        <Title order={4}>{t('races.racesSection')}</Title>
        <Button size="xs" leftSection={<IconPlus size={16} />} onClick={openCreate}>
          {t('races.addRace')}
        </Button>
      </Group>

      {racesQuery.isLoading ? (
        <Loader size="sm" />
      ) : races.length === 0 ? (
        <Text c="dimmed" size="sm">
          {t('races.noRaces')}
        </Text>
      ) : (
        <Stack gap="sm">
          {races.map((race) => {
            const isPast = race.startsAtUtc ? new Date(race.startsAtUtc).getTime() < now : false;
            return (
              <Card key={race.id} withBorder radius="sm" p="md">
                <Group justify="space-between" align="flex-start" wrap="wrap">
                  <div>
                    <Group gap="xs" mb={4}>
                      <IconFlag size={16} />
                      <Text fw={600} size="sm">
                        {race.name}
                      </Text>
                      <Badge size="sm" variant="light">
                        {t(`sport.${race.sport}`)}
                      </Badge>
                      <Badge size="sm" color={priorityColor[race.priority ?? GoalPriority.C]} variant="light">
                        {race.priority}
                      </Badge>
                    </Group>
                    <Text size="xs" c="dimmed">
                      {race.startsAtUtc && new Date(race.startsAtUtc).toLocaleString('cs-CZ')}
                      {race.location ? ` · ${race.location}` : ''}
                      {race.distanceMeters ? ` · ${(race.distanceMeters / 1000).toFixed(1)} km` : ''}
                    </Text>
                    {race.actualTimeSeconds != null ? (
                      <Group gap={4} mt={4}>
                        <IconTrophy size={14} />
                        <Text size="xs">{formatDuration(race.actualTimeSeconds)}</Text>
                        {race.actualResultNote && (
                          <Text size="xs" c="dimmed">
                            – {race.actualResultNote}
                          </Text>
                        )}
                      </Group>
                    ) : isPast ? (
                      <Text size="xs" c="dimmed" mt={4}>
                        {t('races.noResultYet')}
                      </Text>
                    ) : null}
                  </div>
                  {isPast && (
                    <Button size="xs" variant="light" onClick={() => openResultModal(race)}>
                      {t('races.recordResult')}
                    </Button>
                  )}
                </Group>
              </Card>
            );
          })}
        </Stack>
      )}

      <Modal opened={createOpened} onClose={closeCreate} title={t('races.addRace')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <TextInput label={t('races.raceName')} error={errors.name?.message} {...register('name')} />
            <Controller
              name="sport"
              control={control}
              render={({ field }) => <Select label={t('races.sport')} data={sportOptions} {...field} />}
            />
            <Controller
              name="startsAt"
              control={control}
              render={({ field }) => (
                <DateTimePicker
                  label={t('races.startsAt')}
                  value={field.value}
                  onChange={(v) => field.onChange(v ? new Date(v) : new Date())}
                />
              )}
            />
            <TextInput label={t('races.location')} {...register('location')} />
            <Controller
              name="distanceMeters"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label={t('races.distance')}
                  value={field.value ?? undefined}
                  onChange={(v) => field.onChange(v === '' ? undefined : Number(v))}
                />
              )}
            />
            <Controller
              name="elevationGainMeters"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label={t('races.elevationGain')}
                  value={field.value ?? undefined}
                  onChange={(v) => field.onChange(v === '' ? undefined : Number(v))}
                />
              )}
            />
            <Controller
              name="priority"
              control={control}
              render={({ field }) => <Select label={t('races.priority')} data={priorityOptions} {...field} />}
            />
            <TextInput label={t('races.targetTime')} placeholder="hh:mm:ss" {...register('targetTime')} />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('races.createRace')}
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal opened={resultOpened} onClose={closeResult} title={t('races.recordResult')}>
        <form onSubmit={onResultSubmit}>
          <Stack gap="sm">
            <TextInput label={t('races.actualTime')} placeholder="hh:mm:ss" {...registerResult('actualTime')} />
            <TextInput label={t('races.actualResultNote')} {...registerResult('actualResultNote')} />
            <Textarea label={t('races.resultNotes')} minRows={2} {...registerResult('resultNotes')} />
            <Button type="submit" loading={isSubmittingResult} fullWidth mt="sm">
              {t('races.saveResult')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Card>
  );
}

function RacesPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  return (
    <Stack gap="lg">
      <Title order={2}>{t('races.title')}</Title>
      <GoalsSection athleteUserId={athleteUserId} />
      <RacesSection athleteUserId={athleteUserId} />
    </Stack>
  );
}

export default RacesPage;
