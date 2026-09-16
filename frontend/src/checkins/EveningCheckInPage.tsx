import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Checkbox, NumberInput, Stack, Textarea, Title } from '@mantine/core';
import { useAuth } from '../auth/AuthContext';
import { useGetApiAthletesAthleteUserIdCheckinsDateType, getPutApiCheckinsMutationOptions } from '../api/generated/check-ins/check-ins';
import { CheckInType, type SubmitCheckInRequest } from '../api/generated/models';
import { WellnessScaleControl } from './WellnessScaleControl';
import { Panel, FormField, Button, Skeleton, showToast } from '../design-system/components';

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function EveningCheckInSkeleton() {
  return (
    <Stack gap="md">
      <Skeleton height={42} radius="var(--radius-md)" />
      <Skeleton height={54} radius="var(--radius-md)" />
      {Array.from({ length: 5 }).map((_, i) => (
        <Skeleton key={i} height={54} radius="var(--radius-md)" />
      ))}
      <Skeleton height={54} radius="var(--radius-md)" />
      <Skeleton height={42} radius="var(--radius-md)" />
      <Skeleton height={78} radius="var(--radius-md)" />
      <Skeleton height={44} radius="var(--radius-md)" />
    </Stack>
  );
}

export function EveningCheckInPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const athleteUserId = user!.userId;
  const date = todayIso();

  const existingQuery = useGetApiAthletesAthleteUserIdCheckinsDateType(athleteUserId, date, CheckInType.Evening);
  const mutation = useMutation(getPutApiCheckinsMutationOptions());

  const [form, setForm] = useState<Partial<SubmitCheckInRequest>>({});

  useEffect(() => {
    if (existingQuery.data) {
      setForm(existingQuery.data);
    }
  }, [existingQuery.data]);

  const set = <K extends keyof SubmitCheckInRequest>(key: K, value: SubmitCheckInRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }));

  const onSubmit = async () => {
    try {
      await mutation.mutateAsync({
        data: { ...form, athleteUserId, date, type: CheckInType.Evening },
      });
      showToast({ tone: 'positive', message: t('checkin.saved') });
      navigate('/dashboard');
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg" maw={520}>
      <Title className="ds-page-title" order={2}>
        {t('checkin.eveningTitle')}
      </Title>
      <Panel>
        {existingQuery.isLoading ? (
          <EveningCheckInSkeleton />
        ) : (
          <Stack gap="md">
            <FormField label={t('checkin.completedPlannedWorkout')}>
              <Checkbox
                checked={form.completedPlannedWorkout ?? false}
                onChange={(e) => set('completedPlannedWorkout', e.currentTarget.checked)}
              />
            </FormField>
            <FormField label={t('checkin.rpe')}>
              <NumberInput min={1} max={10} value={form.rpe ?? undefined} onChange={(v) => set('rpe', typeof v === 'number' ? v : undefined)} />
            </FormField>
            <WellnessScaleControl label={t('checkin.energy')} value={form.energy} onChange={(v) => set('energy', v)} />
            <WellnessScaleControl label={t('checkin.fatigue')} value={form.fatigue} onChange={(v) => set('fatigue', v)} />
            <WellnessScaleControl label={t('checkin.legsFeeling')} value={form.legsFeeling} onChange={(v) => set('legsFeeling', v)} />
            <WellnessScaleControl label={t('checkin.stress')} value={form.stress} onChange={(v) => set('stress', v)} />
            <WellnessScaleControl label={t('checkin.mealQuality')} value={form.mealQuality} onChange={(v) => set('mealQuality', v)} />
            <FormField label={t('checkin.hydrationLiters')}>
              <NumberInput
                min={0}
                max={20}
                step={0.5}
                value={form.hydrationLiters ?? undefined}
                onChange={(v) => set('hydrationLiters', typeof v === 'number' ? v : undefined)}
              />
            </FormField>
            <FormField label={t('checkin.hasPainOrIllness')}>
              <Checkbox checked={form.hasPainOrIllness ?? false} onChange={(e) => set('hasPainOrIllness', e.currentTarget.checked)} />
            </FormField>
            <FormField label={t('checkin.note')}>
              <Textarea minRows={2} value={form.note ?? ''} onChange={(e) => set('note', e.currentTarget.value)} />
            </FormField>
            <Button size="md" onClick={onSubmit} loading={mutation.isPending}>
              {t('checkin.save')}
            </Button>
          </Stack>
        )}
      </Panel>
    </Stack>
  );
}
