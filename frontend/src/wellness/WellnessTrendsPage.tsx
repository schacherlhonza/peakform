import { useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Badge, Button, Card, Group, Loader, Modal, Select, SimpleGrid, Stack, Text, Textarea, TextInput, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconAlertTriangle, IconMedal, IconPlus } from '@tabler/icons-react';
import { useGetApiAthletesAthleteUserIdHrv } from '../api/generated/hrv-measurements/hrv-measurements';
import { useGetApiAthletesAthleteUserIdRecovery } from '../api/generated/recovery-metrics/recovery-metrics';
import { useGetApiAthletesAthleteUserIdSleep } from '../api/generated/sleep-records/sleep-records';
import {
  useGetApiAthletesAthleteUserIdPersonalRecords,
  getGetApiAthletesAthleteUserIdPersonalRecordsQueryKey,
  getPostApiAthletesAthleteUserIdPersonalRecordsMutationOptions,
} from '../api/generated/personal-records/personal-records';
import {
  useGetApiAthletesAthleteUserIdHealthFlags,
  getGetApiAthletesAthleteUserIdHealthFlagsQueryKey,
  getPostApiAthletesAthleteUserIdHealthFlagsMutationOptions,
  getPutApiAthletesAthleteUserIdHealthFlagsFlagIdStatusMutationOptions,
} from '../api/generated/pain-or-health-flags/pain-or-health-flags';
import { HealthFlagSeverity, HealthFlagStatus, HealthFlagType, SportType, type PainOrHealthFlagDto } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { addDays, toIsoDate } from '../calendar/dateUtils';
import { Sparkline } from './Sparkline';

const sportOptions = Object.values(SportType).map((value) => ({ value, label: value }));
const flagTypeOptions = Object.values(HealthFlagType).map((value) => ({ value, label: value }));
const flagSeverityOptions = Object.values(HealthFlagSeverity).map((value) => ({ value, label: value }));

const severityColor: Record<string, string> = {
  [HealthFlagSeverity.Mild]: 'yellow',
  [HealthFlagSeverity.Moderate]: 'orange',
  [HealthFlagSeverity.Severe]: 'red',
};

function formatDuration(seconds: number | null | undefined): string {
  if (seconds == null) return '—';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`;
}

function parseDuration(input: string | undefined): number | undefined {
  if (!input || !input.trim()) return undefined;
  const parts = input.split(':').map((p) => Number(p));
  if (parts.some((p) => Number.isNaN(p))) return undefined;
  if (parts.length === 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
  if (parts.length === 2) return parts[0] * 60 + parts[1];
  return parts[0];
}

const recordSchema = z.object({
  sport: z.nativeEnum(SportType),
  distanceLabel: z.string().min(1),
  timeSeconds: z.string().optional(),
  achievedDate: z.date(),
  notes: z.string().optional(),
});
type RecordFormValues = z.infer<typeof recordSchema>;

const flagSchema = z.object({
  type: z.nativeEnum(HealthFlagType),
  severity: z.nativeEnum(HealthFlagSeverity),
  bodyPart: z.string().optional(),
  description: z.string().optional(),
  startedOnDate: z.date(),
});
type FlagFormValues = z.infer<typeof flagSchema>;

function TrendsSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const from = toIsoDate(addDays(new Date(), -13));
  const to = toIsoDate(new Date());

  const hrvQuery = useGetApiAthletesAthleteUserIdHrv(athleteUserId, { from, to });
  const recoveryQuery = useGetApiAthletesAthleteUserIdRecovery(athleteUserId, { from, to });
  const sleepQuery = useGetApiAthletesAthleteUserIdSleep(athleteUserId, { from, to });

  const hrvData = useMemo(
    () =>
      [...(hrvQuery.data ?? [])]
        .filter((d) => d.date && d.rmssdMs != null)
        .sort((a, b) => (a.date ?? '').localeCompare(b.date ?? ''))
        .map((d) => ({ date: d.date!, value: Math.round(d.rmssdMs!) })),
    [hrvQuery.data],
  );

  const rhrData = useMemo(
    () =>
      [...(recoveryQuery.data ?? [])]
        .filter((d) => d.date && d.restingHeartRateBpm != null)
        .sort((a, b) => (a.date ?? '').localeCompare(b.date ?? ''))
        .map((d) => ({ date: d.date!, value: Math.round(d.restingHeartRateBpm!) })),
    [recoveryQuery.data],
  );

  const sleepData = useMemo(
    () =>
      [...(sleepQuery.data ?? [])]
        .filter((d) => d.date && d.durationMinutes != null)
        .sort((a, b) => (a.date ?? '').localeCompare(b.date ?? ''))
        .map((d) => ({ date: d.date!, value: Math.round((d.durationMinutes! / 60) * 10) / 10 })),
    [sleepQuery.data],
  );

  return (
    <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }} spacing="lg">
      <Card withBorder radius="md" p="lg">
        <Title order={4} mb="sm">
          {t('wellness.hrv')}
        </Title>
        {hrvQuery.isLoading ? <Loader size="sm" /> : <Sparkline data={hrvData} color="var(--mantine-color-grape-6)" unit=" ms" />}
      </Card>
      <Card withBorder radius="md" p="lg">
        <Title order={4} mb="sm">
          {t('wellness.restingHr')}
        </Title>
        {recoveryQuery.isLoading ? <Loader size="sm" /> : <Sparkline data={rhrData} color="var(--mantine-color-red-6)" unit=" tep/min" />}
      </Card>
      <Card withBorder radius="md" p="lg">
        <Title order={4} mb="sm">
          {t('wellness.sleepDuration')}
        </Title>
        {sleepQuery.isLoading ? <Loader size="sm" /> : <Sparkline data={sleepData} color="var(--mantine-color-blue-6)" unit=" h" />}
      </Card>
    </SimpleGrid>
  );
}

function PersonalRecordsSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [opened, { open, close }] = useDisclosure();

  const recordsQuery = useGetApiAthletesAthleteUserIdPersonalRecords(athleteUserId);
  const records = useMemo(
    () => [...(recordsQuery.data ?? [])].sort((a, b) => (b.achievedDate ?? '').localeCompare(a.achievedDate ?? '')),
    [recordsQuery.data],
  );

  const createMutation = useMutation(getPostApiAthletesAthleteUserIdPersonalRecordsMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<RecordFormValues>({
    resolver: zodResolver(recordSchema),
    defaultValues: { sport: SportType.Running, distanceLabel: '', achievedDate: new Date() },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({
        athleteUserId,
        data: {
          athleteUserId,
          sport: values.sport,
          distanceLabel: values.distanceLabel,
          timeSeconds: parseDuration(values.timeSeconds),
          achievedDate: toIsoDate(values.achievedDate),
          notes: values.notes || undefined,
        },
      });
      notifications.show({ color: 'green', message: t('wellness.recordCreated') });
      reset({ sport: values.sport, distanceLabel: '', achievedDate: new Date() });
      close();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdPersonalRecordsQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Card withBorder radius="md" p="lg">
      <Group justify="space-between" mb="sm">
        <Title order={4}>{t('wellness.personalRecords')}</Title>
        <Button size="xs" leftSection={<IconPlus size={16} />} onClick={open}>
          {t('wellness.addRecord')}
        </Button>
      </Group>

      {recordsQuery.isLoading ? (
        <Loader size="sm" />
      ) : records.length === 0 ? (
        <Text c="dimmed" size="sm">
          {t('wellness.noPersonalRecords')}
        </Text>
      ) : (
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
          {records.map((record) => (
            <Card key={record.id} withBorder radius="sm" p="md">
              <Group gap="xs" mb={4}>
                <IconMedal size={16} />
                <Badge size="sm" variant="light">
                  {t(`sport.${record.sport}`)}
                </Badge>
              </Group>
              <Text fw={600} size="sm">
                {record.distanceLabel}
              </Text>
              <Text size="sm">{formatDuration(record.timeSeconds)}</Text>
              <Text size="xs" c="dimmed">
                {record.achievedDate}
              </Text>
              {record.notes && (
                <Text size="xs" c="dimmed" mt={4}>
                  {record.notes}
                </Text>
              )}
            </Card>
          ))}
        </SimpleGrid>
      )}

      <Modal opened={opened} onClose={close} title={t('wellness.addRecord')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Controller
              name="sport"
              control={control}
              render={({ field }) => <Select label={t('races.sport')} data={sportOptions} {...field} />}
            />
            <TextInput label={t('wellness.distanceLabel')} error={errors.distanceLabel?.message} {...register('distanceLabel')} />
            <TextInput label={t('wellness.recordTime')} placeholder="hh:mm:ss" {...register('timeSeconds')} />
            <Controller
              name="achievedDate"
              control={control}
              render={({ field }) => (
                <DateInput label={t('wellness.achievedDate')} value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
              )}
            />
            <Textarea label={t('common.notes')} minRows={2} {...register('notes')} />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Card>
  );
}

function HealthFlagsSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [opened, { open, close }] = useDisclosure();

  const flagsQuery = useGetApiAthletesAthleteUserIdHealthFlags(athleteUserId, { activeOnly: true });
  const flags = flagsQuery.data ?? [];

  const createMutation = useMutation(getPostApiAthletesAthleteUserIdHealthFlagsMutationOptions());
  const resolveMutation = useMutation(getPutApiAthletesAthleteUserIdHealthFlagsFlagIdStatusMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { isSubmitting },
  } = useForm<FlagFormValues>({
    resolver: zodResolver(flagSchema),
    defaultValues: { type: HealthFlagType.Pain, severity: HealthFlagSeverity.Mild, startedOnDate: new Date() },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdHealthFlagsQueryKey(athleteUserId, { activeOnly: true }) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({
        athleteUserId,
        data: {
          athleteUserId,
          type: values.type,
          severity: values.severity,
          bodyPart: values.bodyPart || undefined,
          description: values.description || undefined,
          startedOnDate: toIsoDate(values.startedOnDate),
        },
      });
      notifications.show({ color: 'green', message: t('wellness.flagCreated') });
      reset({ type: values.type, severity: values.severity, startedOnDate: new Date() });
      close();
      await invalidate();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const markResolved = async (flag: PainOrHealthFlagDto) => {
    if (!flag.id) return;
    try {
      await resolveMutation.mutateAsync({ athleteUserId, flagId: flag.id, data: { status: HealthFlagStatus.Resolved } });
      notifications.show({ color: 'green', message: t('wellness.flagResolved') });
      await invalidate();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Card withBorder radius="md" p="lg">
      <Group justify="space-between" mb="sm">
        <Title order={4}>{t('wellness.healthFlags')}</Title>
        <Button size="xs" leftSection={<IconPlus size={16} />} onClick={open}>
          {t('wellness.reportFlag')}
        </Button>
      </Group>

      {flagsQuery.isLoading ? (
        <Loader size="sm" />
      ) : flags.length === 0 ? (
        <Text c="dimmed" size="sm">
          {t('wellness.noHealthFlags')}
        </Text>
      ) : (
        <Stack gap="xs">
          {flags.map((flag) => (
            <Card key={flag.id} withBorder radius="sm" p="md">
              <Group justify="space-between" wrap="wrap">
                <div>
                  <Group gap="xs" mb={4}>
                    <IconAlertTriangle size={16} />
                    <Badge size="sm" variant="light">
                      {t(`healthFlagType.${flag.type}`)}
                    </Badge>
                    <Badge size="sm" color={severityColor[flag.severity ?? HealthFlagSeverity.Mild]} variant="light">
                      {t(`healthFlagSeverity.${flag.severity}`)}
                    </Badge>
                    <Badge size="sm" variant="outline">
                      {t(`healthFlagStatus.${flag.status}`)}
                    </Badge>
                  </Group>
                  {flag.bodyPart && <Text size="sm">{flag.bodyPart}</Text>}
                  {flag.description && (
                    <Text size="xs" c="dimmed">
                      {flag.description}
                    </Text>
                  )}
                  <Text size="xs" c="dimmed">
                    {t('wellness.startedOn')}: {flag.startedOnDate}
                  </Text>
                </div>
                <Button size="xs" variant="light" onClick={() => markResolved(flag)} loading={resolveMutation.isPending}>
                  {t('wellness.markResolved')}
                </Button>
              </Group>
            </Card>
          ))}
        </Stack>
      )}

      <Modal opened={opened} onClose={close} title={t('wellness.reportFlag')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Controller
              name="type"
              control={control}
              render={({ field }) => <Select label={t('wellness.flagType')} data={flagTypeOptions} {...field} />}
            />
            <Controller
              name="severity"
              control={control}
              render={({ field }) => <Select label={t('wellness.severity')} data={flagSeverityOptions} {...field} />}
            />
            <TextInput label={t('wellness.bodyPart')} {...register('bodyPart')} />
            <Textarea label={t('common.notes')} minRows={2} {...register('description')} />
            <Controller
              name="startedOnDate"
              control={control}
              render={({ field }) => (
                <DateInput label={t('wellness.startedOn')} value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
              )}
            />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Card>
  );
}

function WellnessTrendsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  return (
    <Stack gap="lg">
      <Title order={2}>{t('wellness.title')}</Title>
      <TrendsSection athleteUserId={athleteUserId} />
      <PersonalRecordsSection athleteUserId={athleteUserId} />
      <HealthFlagsSection athleteUserId={athleteUserId} />
    </Stack>
  );
}

export default WellnessTrendsPage;
