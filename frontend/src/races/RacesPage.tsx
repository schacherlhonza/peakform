import { useMemo, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, NumberInput, Select, Stack, Text, Textarea, TextInput, Title } from '@mantine/core';
import { DateInput, DateTimePicker } from '@mantine/dates';
import { useDisclosure } from '@mantine/hooks';
import { IconFlag, IconPlus, IconTarget, IconTrophy } from '@tabler/icons-react';
import { Panel, CardHeader, Button, Modal, FormField, Skeleton, EmptyState, Badge, showToast, type BadgeTone } from '../design-system/components';
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

// Priority pill mapping, reused wherever a goal/race priority is shown: A (highest priority,
// demands the most attention) = danger, B (normal) = warning, C (low) = neutral.
const priorityTone: Record<string, BadgeTone> = {
  [GoalPriority.A]: 'danger',
  [GoalPriority.B]: 'warning',
  [GoalPriority.C]: 'neutral',
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

function ListSkeleton() {
  return (
    <Stack gap="sm">
      {[0, 1, 2].map((i) => (
        <Skeleton key={i} height={64} radius="var(--radius-md)" />
      ))}
    </Stack>
  );
}

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
      showToast({ tone: 'positive', message: t('races.goalCreated') });
      reset();
      close();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdGoalsQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
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
      showToast({ tone: 'positive', message: t('races.goalAchieved') });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdGoalsQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Panel>
      <CardHeader
        kicker={t('races.goalsSection')}
        right={
          <Button size="xs" leftSection={<IconPlus size={16} />} onClick={open}>
            {t('races.addGoal')}
          </Button>
        }
      />

      {goalsQuery.isLoading ? (
        <ListSkeleton />
      ) : goals.length === 0 ? (
        <EmptyState
          icon={<IconTarget size={28} stroke={1.6} />}
          title={t('races.noGoals')}
          action={
            <Button size="xs" variant="light" leftSection={<IconPlus size={16} />} onClick={open}>
              {t('races.addGoal')}
            </Button>
          }
        />
      ) : (
        <Stack gap={0}>
          {goals.map((goal) => (
            <div key={goal.id} className="ds-list-row">
              <Group justify="space-between" align="flex-start" wrap="wrap">
                <div style={{ minWidth: 0 }}>
                  <Group gap={6} mb={4}>
                    <Text fw={600} size="sm">
                      {goal.title}
                    </Text>
                    <Badge tone={priorityTone[goal.priority ?? GoalPriority.C]}>{goal.priority}</Badge>
                    {goal.isAchieved && <Badge tone="positive">{t('races.achieved')}</Badge>}
                  </Group>
                  {goal.description && (
                    <Text className="ds-body" lineClamp={3} mb={4}>
                      {goal.description}
                    </Text>
                  )}
                  {goal.targetDate && (
                    <Text className="ds-metadata">
                      {t('races.targetDate')}: {goal.targetDate}
                    </Text>
                  )}
                </div>
                {!goal.isAchieved && (
                  <Button size="xs" variant="light" onClick={() => markAchieved(goal)} loading={achieveMutation.isPending}>
                    {t('races.markAchieved')}
                  </Button>
                )}
              </Group>
            </div>
          ))}
        </Stack>
      )}

      <Modal opened={opened} onClose={close} title={t('races.addGoal')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <FormField label={t('races.goalTitle')} error={errors.title?.message}>
              <TextInput {...register('title')} />
            </FormField>
            <FormField label={t('races.goalDescription')}>
              <Textarea minRows={2} {...register('description')} />
            </FormField>
            <Controller
              name="targetDate"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.targetDate')}>
                  <DateInput value={field.value ?? null} onChange={(v) => field.onChange(v ? new Date(v) : null)} clearable />
                </FormField>
              )}
            />
            <Controller
              name="priority"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.priority')}>
                  <Select data={priorityOptions} {...field} />
                </FormField>
              )}
            />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('races.createGoal')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Panel>
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
      showToast({ tone: 'positive', message: t('races.raceCreated') });
      reset();
      closeCreate();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdRacesQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
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
      showToast({ tone: 'positive', message: t('races.resultSaved') });
      closeResult();
      setSelectedRace(null);
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdRacesQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const now = Date.now();

  return (
    <Panel>
      <CardHeader
        kicker={t('races.racesSection')}
        right={
          <Button size="xs" leftSection={<IconPlus size={16} />} onClick={openCreate}>
            {t('races.addRace')}
          </Button>
        }
      />

      {racesQuery.isLoading ? (
        <ListSkeleton />
      ) : races.length === 0 ? (
        <EmptyState
          icon={<IconFlag size={28} stroke={1.6} />}
          title={t('races.noRaces')}
          action={
            <Button size="xs" variant="light" leftSection={<IconPlus size={16} />} onClick={openCreate}>
              {t('races.addRace')}
            </Button>
          }
        />
      ) : (
        <Stack gap="sm">
          {races.map((race) => {
            const isPast = race.startsAtUtc ? new Date(race.startsAtUtc).getTime() < now : false;
            return (
              <div
                key={race.id}
                style={{
                  background: 'var(--color-surface-inset)',
                  border: '1px solid var(--color-border)',
                  borderRadius: 'var(--radius-md)',
                  padding: 'var(--space-4)',
                }}
              >
                <Group justify="space-between" align="flex-start" wrap="wrap">
                  <div style={{ minWidth: 0 }}>
                    <Group gap="xs" mb={4}>
                      <IconFlag size={16} color="var(--color-text-subtle)" />
                      <Text fw={600} size="sm">
                        {race.name}
                      </Text>
                      <Badge tone="info">{t(`sport.${race.sport}`)}</Badge>
                      <Badge tone={priorityTone[race.priority ?? GoalPriority.C]}>{race.priority}</Badge>
                    </Group>
                    <Text className="ds-body">
                      {race.startsAtUtc && new Date(race.startsAtUtc).toLocaleString('cs-CZ')}
                      {race.location ? ` · ${race.location}` : ''}
                      {race.distanceMeters ? ` · ${(race.distanceMeters / 1000).toFixed(1)} km` : ''}
                    </Text>
                    {race.targetTimeSeconds != null && (
                      <Group gap={4} mt={4}>
                        <Text className="ds-metadata">{t('races.targetTime')}:</Text>
                        <Text className="ds-key-metric" fz={14}>
                          {formatDuration(race.targetTimeSeconds)}
                        </Text>
                      </Group>
                    )}
                    {race.actualTimeSeconds != null ? (
                      <Group gap={4} mt={4}>
                        <IconTrophy size={14} color="var(--color-accent)" />
                        <Text className="ds-key-metric" fz={14}>
                          {formatDuration(race.actualTimeSeconds)}
                        </Text>
                        {race.actualResultNote && <Text className="ds-metadata">– {race.actualResultNote}</Text>}
                      </Group>
                    ) : isPast ? (
                      <Text className="ds-metadata" mt={4}>
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
              </div>
            );
          })}
        </Stack>
      )}

      <Modal opened={createOpened} onClose={closeCreate} title={t('races.addRace')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <FormField label={t('races.raceName')} error={errors.name?.message}>
              <TextInput {...register('name')} />
            </FormField>
            <Controller
              name="sport"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.sport')}>
                  <Select data={sportOptions} {...field} />
                </FormField>
              )}
            />
            <Controller
              name="startsAt"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.startsAt')}>
                  <DateTimePicker value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
                </FormField>
              )}
            />
            <FormField label={t('races.location')}>
              <TextInput {...register('location')} />
            </FormField>
            <Controller
              name="distanceMeters"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.distance')}>
                  <NumberInput value={field.value ?? undefined} onChange={(v) => field.onChange(v === '' ? undefined : Number(v))} />
                </FormField>
              )}
            />
            <Controller
              name="elevationGainMeters"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.elevationGain')}>
                  <NumberInput value={field.value ?? undefined} onChange={(v) => field.onChange(v === '' ? undefined : Number(v))} />
                </FormField>
              )}
            />
            <Controller
              name="priority"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.priority')}>
                  <Select data={priorityOptions} {...field} />
                </FormField>
              )}
            />
            <FormField label={t('races.targetTime')}>
              <TextInput placeholder="hh:mm:ss" {...register('targetTime')} />
            </FormField>
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('races.createRace')}
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal opened={resultOpened} onClose={closeResult} title={t('races.recordResult')}>
        <form onSubmit={onResultSubmit}>
          <Stack gap="sm">
            <FormField label={t('races.actualTime')}>
              <TextInput placeholder="hh:mm:ss" {...registerResult('actualTime')} />
            </FormField>
            <FormField label={t('races.actualResultNote')}>
              <TextInput {...registerResult('actualResultNote')} />
            </FormField>
            <FormField label={t('races.resultNotes')}>
              <Textarea minRows={2} {...registerResult('resultNotes')} />
            </FormField>
            <Button type="submit" loading={isSubmittingResult} fullWidth mt="sm">
              {t('races.saveResult')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Panel>
  );
}

function RacesPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('races.title')}
      </Title>
      <GoalsSection athleteUserId={athleteUserId} />
      <RacesSection athleteUserId={athleteUserId} />
    </Stack>
  );
}

export default RacesPage;
