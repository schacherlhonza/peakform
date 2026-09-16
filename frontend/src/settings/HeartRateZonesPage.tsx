import { useMemo } from 'react';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, NumberInput, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconHeartbeat } from '@tabler/icons-react';
import { Panel, Button, FormField, Skeleton, EmptyState, showToast } from '../design-system/components';
import {
  useGetApiAthletesAthleteUserIdHeartRateZones,
  getGetApiAthletesAthleteUserIdHeartRateZonesQueryKey,
  getPutApiAthletesAthleteUserIdHeartRateZonesMutationOptions,
} from '../api/generated/heart-rate-zones/heart-rate-zones';
import { useAuth } from '../auth/AuthContext';
import { AppRole } from '../api/generated/models';

const zoneSchema = z.object({
  zoneNumber: z.number(),
  name: z.string().optional(),
  minBpm: z.number().min(0),
  maxBpm: z.number().min(0),
});

const schema = z.object({
  effectiveFromDate: z.date(),
  zones: z.array(zoneSchema).length(5),
});
type FormValues = z.infer<typeof schema>;

function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function ZonesSkeleton() {
  return (
    <Stack gap="sm">
      {[0, 1, 2, 3, 4].map((i) => (
        <Skeleton key={i} height={40} radius="var(--radius-xs)" />
      ))}
    </Stack>
  );
}

export default function HeartRateZonesPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { user } = useAuth();

  const isCoach = user?.role === AppRole.Coach;
  const athleteUserId = user?.userId ?? '';

  const zonesQuery = useGetApiAthletesAthleteUserIdHeartRateZones(athleteUserId, {
    query: { enabled: !isCoach && !!athleteUserId },
  });

  const defaultZones = useMemo(() => {
    const byNumber = new Map((zonesQuery.data ?? []).map((z) => [z.zoneNumber, z]));
    return [1, 2, 3, 4, 5].map((n) => {
      const existing = byNumber.get(n);
      return {
        zoneNumber: n,
        name: existing?.name ?? '',
        minBpm: existing?.minBpm ?? 0,
        maxBpm: existing?.maxBpm ?? 0,
      };
    });
  }, [zonesQuery.data]);

  const defaultEffectiveFromDate = useMemo(() => {
    const existing = zonesQuery.data?.[0]?.effectiveFromDate;
    return existing ? new Date(existing) : new Date();
  }, [zonesQuery.data]);

  const {
    control,
    register,
    handleSubmit,
    formState: { isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: { effectiveFromDate: defaultEffectiveFromDate, zones: defaultZones },
  });
  const { fields } = useFieldArray({ control, name: 'zones' });

  const saveMutation = useMutation(getPutApiAthletesAthleteUserIdHeartRateZonesMutationOptions());

  const onSubmit = handleSubmit(async (values) => {
    try {
      await saveMutation.mutateAsync({
        athleteUserId,
        data: {
          athleteUserId,
          effectiveFromDate: toIsoDate(values.effectiveFromDate),
          zones: values.zones.map((z) => ({
            zoneNumber: z.zoneNumber,
            name: z.name,
            minBpm: z.minBpm,
            maxBpm: z.maxBpm,
          })),
        },
      });
      showToast({ tone: 'positive', message: t('settings.heartRateZonesSaved') });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdHeartRateZonesQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  if (isCoach) {
    return (
      <Stack gap="lg">
        <Title className="ds-page-title" order={2}>
          {t('settings.heartRateZones')}
        </Title>
        <Panel>
          <EmptyState
            icon={<IconHeartbeat size={28} stroke={1.6} />}
            title={t('settings.heartRateZones')}
            description={t('settings.heartRateZonesCoachNotice')}
          />
        </Panel>
      </Stack>
    );
  }

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('settings.heartRateZones')}
      </Title>
      <Panel>
        {zonesQuery.isLoading ? (
          <ZonesSkeleton />
        ) : (
          <form onSubmit={onSubmit}>
            <Stack gap="md">
              <FormField label={t('settings.effectiveFromDate')}>
                <Controller
                  name="effectiveFromDate"
                  control={control}
                  render={({ field }) => (
                    <DateInput w={220} value={field.value} onChange={(v) => field.onChange(v ? new Date(v) : new Date())} />
                  )}
                />
              </FormField>

              <Table verticalSpacing="sm" horizontalSpacing="md">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th className="ds-eyebrow">{t('settings.zone')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('settings.zoneName')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('settings.minBpm')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('settings.maxBpm')}</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {fields.map((field, index) => (
                    <Table.Tr key={field.id}>
                      <Table.Td>
                        <Text fw={700} c="var(--color-text)">
                          {field.zoneNumber}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <TextInput {...register(`zones.${index}.name`)} />
                      </Table.Td>
                      <Table.Td>
                        <Controller
                          control={control}
                          name={`zones.${index}.minBpm`}
                          render={({ field: f }) => (
                            <NumberInput w={110} value={f.value} onChange={(v) => f.onChange(Number(v))} />
                          )}
                        />
                      </Table.Td>
                      <Table.Td>
                        <Controller
                          control={control}
                          name={`zones.${index}.maxBpm`}
                          render={({ field: f }) => (
                            <NumberInput w={110} value={f.value} onChange={(v) => f.onChange(Number(v))} />
                          )}
                        />
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>

              <Group justify="flex-end">
                <Button type="submit" loading={isSubmitting || saveMutation.isPending}>
                  {t('common.save')}
                </Button>
              </Group>
            </Stack>
          </form>
        )}
      </Panel>
    </Stack>
  );
}
