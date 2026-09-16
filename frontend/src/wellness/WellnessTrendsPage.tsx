import { useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Select, SimpleGrid, Stack, Text, Textarea, TextInput, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useDisclosure } from '@mantine/hooks';
import { IconAlertTriangle, IconMedal, IconPlus } from '@tabler/icons-react';
import { Badge, type BadgeTone, Button, CardHeader, EmptyState, FormField, Modal, Panel, Skeleton, showToast } from '../design-system/components';
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

// Severity/status → Badge tone maps (docs/DESIGN_SYSTEM.md §8/§9): color is always paired
// with the translated label, never the sole signal.
const severityTone: Record<HealthFlagSeverity, BadgeTone> = {
  [HealthFlagSeverity.Mild]: 'neutral',
  [HealthFlagSeverity.Moderate]: 'warning',
  [HealthFlagSeverity.Severe]: 'danger',
};

const statusTone: Record<HealthFlagStatus, BadgeTone> = {
  [HealthFlagStatus.Active]: 'danger',
  [HealthFlagStatus.Improving]: 'warning',
  [HealthFlagStatus.Resolved]: 'positive',
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
    <Panel>
      <CardHeader kicker={t('wellness.title')} />
      <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="lg">
        <div>
          <Text className="ds-eyebrow" mb={6}>
            {t('wellness.hrv')}
          </Text>
          {hrvQuery.isLoading ? <Skeleton height={80} /> : <Sparkline data={hrvData} tone="accent" unit=" ms" />}
        </div>
        <div>
          <Text className="ds-eyebrow" mb={6}>
            {t('wellness.restingHr')}
          </Text>
          {recoveryQuery.isLoading ? <Skeleton height={80} /> : <Sparkline data={rhrData} tone="accent" unit=" tep/min" />}
        </div>
        <div>
          <Text className="ds-eyebrow" mb={6}>
            {t('wellness.sleepDuration')}
          </Text>
          {sleepQuery.isLoading ? <Skeleton height={80} /> : <Sparkline data={sleepData} tone="info" unit=" h" />}
        </div>
      </SimpleGrid>
    </Panel>
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
      showToast({ tone: 'positive', message: t('wellness.recordCreated') });
      reset({ sport: values.sport, distanceLabel: '', achievedDate: new Date() });
      close();
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdPersonalRecordsQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Panel>
      <CardHeader
        kicker={t('wellness.personalRecords')}
        right={
          <Button size="xs" leftSection={<IconPlus size={16} />} onClick={open}>
            {t('wellness.addRecord')}
          </Button>
        }
      />

      {recordsQuery.isLoading ? (
        <Stack gap="sm">
          {[0, 1].map((i) => (
            <Skeleton key={i} height={60} radius="var(--radius-panel)" />
          ))}
        </Stack>
      ) : records.length === 0 ? (
        <EmptyState icon={<IconMedal size={28} stroke={1.6} />} title={t('wellness.noPersonalRecords')} />
      ) : (
        <Stack gap={0}>
          {records.map((record) => (
            <div key={record.id} className="ds-list-row">
              <CardHeader
                kicker={t(`sport.${record.sport}`)}
                title={record.distanceLabel}
                right={<Text className="ds-metadata">{record.achievedDate}</Text>}
              />
              <Text className="ds-body">{formatDuration(record.timeSeconds)}</Text>
              {record.notes && (
                <Text className="ds-body" mt={4}>
                  {record.notes}
                </Text>
              )}
            </div>
          ))}
        </Stack>
      )}

      <Modal opened={opened} onClose={close} title={t('wellness.addRecord')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Controller
              name="sport"
              control={control}
              render={({ field }) => (
                <FormField label={t('races.sport')}>
                  <Select data={Object.values(SportType).map((v) => ({ value: v, label: t(`sport.${v}`) }))} {...field} />
                </FormField>
              )}
            />
            <FormField label={t('wellness.distanceLabel')} error={errors.distanceLabel?.message}>
              <TextInput {...register('distanceLabel')} />
            </FormField>
            <FormField label={t('wellness.recordTime')} unit="hh:mm:ss">
              <TextInput placeholder="hh:mm:ss" {...register('timeSeconds')} />
            </FormField>
            <Controller
              name="achievedDate"
              control={control}
              render={({ field }) => (
                <FormField label={t('wellness.achievedDate')}>
                  <DateInput value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
                </FormField>
              )}
            />
            <FormField label={t('common.notes')}>
              <Textarea minRows={2} {...register('notes')} />
            </FormField>
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Panel>
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
      showToast({ tone: 'positive', message: t('wellness.flagCreated') });
      reset({ type: values.type, severity: values.severity, startedOnDate: new Date() });
      close();
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const markResolved = async (flag: PainOrHealthFlagDto) => {
    if (!flag.id) return;
    try {
      await resolveMutation.mutateAsync({ athleteUserId, flagId: flag.id, data: { status: HealthFlagStatus.Resolved } });
      showToast({ tone: 'positive', message: t('wellness.flagResolved') });
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Panel>
      <CardHeader
        kicker={t('wellness.healthFlags')}
        right={
          <Button size="xs" leftSection={<IconPlus size={16} />} onClick={open}>
            {t('wellness.reportFlag')}
          </Button>
        }
      />

      {flagsQuery.isLoading ? (
        <Stack gap="sm">
          {[0, 1].map((i) => (
            <Skeleton key={i} height={60} radius="var(--radius-panel)" />
          ))}
        </Stack>
      ) : flags.length === 0 ? (
        <EmptyState icon={<IconAlertTriangle size={28} stroke={1.6} />} title={t('wellness.noHealthFlags')} />
      ) : (
        <Stack gap={0}>
          {flags.map((flag) => (
            <div key={flag.id} className="ds-list-row">
              <CardHeader
                kicker={t(`healthFlagType.${flag.type}`)}
                title={flag.bodyPart || undefined}
                right={
                  <Group gap="xs" wrap="wrap" justify="flex-end">
                    <Badge tone={severityTone[flag.severity ?? HealthFlagSeverity.Mild]}>
                      {t(`healthFlagSeverity.${flag.severity}`)}
                    </Badge>
                    <Badge tone={statusTone[flag.status ?? HealthFlagStatus.Active]}>{t(`healthFlagStatus.${flag.status}`)}</Badge>
                  </Group>
                }
              />
              {flag.description && <Text className="ds-body">{flag.description}</Text>}
              <Group justify="space-between" align="center" mt="xs" wrap="wrap">
                <Text className="ds-metadata">
                  {t('wellness.startedOn')}: {flag.startedOnDate}
                </Text>
                <Button size="xs" variant="light" onClick={() => markResolved(flag)} loading={resolveMutation.isPending}>
                  {t('wellness.markResolved')}
                </Button>
              </Group>
            </div>
          ))}
        </Stack>
      )}

      <Modal opened={opened} onClose={close} title={t('wellness.reportFlag')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Controller
              name="type"
              control={control}
              render={({ field }) => (
                <FormField label={t('wellness.flagType')}>
                  <Select data={Object.values(HealthFlagType).map((v) => ({ value: v, label: t(`healthFlagType.${v}`) }))} {...field} />
                </FormField>
              )}
            />
            <Controller
              name="severity"
              control={control}
              render={({ field }) => (
                <FormField label={t('wellness.severity')}>
                  <Select data={Object.values(HealthFlagSeverity).map((v) => ({ value: v, label: t(`healthFlagSeverity.${v}`) }))} {...field} />
                </FormField>
              )}
            />
            <FormField label={t('wellness.bodyPart')}>
              <TextInput {...register('bodyPart')} />
            </FormField>
            <FormField label={t('common.notes')}>
              <Textarea minRows={2} {...register('description')} />
            </FormField>
            <Controller
              name="startedOnDate"
              control={control}
              render={({ field }) => (
                <FormField label={t('wellness.startedOn')}>
                  <DateInput value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
                </FormField>
              )}
            />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Panel>
  );
}

function WellnessTrendsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('wellness.title')}
      </Title>
      <TrendsSection athleteUserId={athleteUserId} />
      <PersonalRecordsSection athleteUserId={athleteUserId} />
      <HealthFlagsSection athleteUserId={athleteUserId} />
    </Stack>
  );
}

export default WellnessTrendsPage;
