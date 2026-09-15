import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Button, Card, Checkbox, NumberInput, Stack, Textarea, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useAuth } from '../auth/AuthContext';
import { useGetApiAthletesAthleteUserIdCheckinsDateType, getPutApiCheckinsMutationOptions } from '../api/generated/check-ins/check-ins';
import { CheckInType, type SubmitCheckInRequest } from '../api/generated/models';
import { WellnessScaleControl } from './WellnessScaleControl';

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
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
      notifications.show({ color: 'green', message: t('checkin.saved') });
      navigate('/dashboard');
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg" maw={520}>
      <Title order={2}>{t('checkin.eveningTitle')}</Title>
      <Card withBorder radius="md" p="lg">
        <Stack gap="md">
          <Checkbox
            label={t('checkin.completedPlannedWorkout')}
            checked={form.completedPlannedWorkout ?? false}
            onChange={(e) => set('completedPlannedWorkout', e.currentTarget.checked)}
          />
          <NumberInput
            label={t('checkin.rpe')}
            min={1}
            max={10}
            value={form.rpe ?? undefined}
            onChange={(v) => set('rpe', typeof v === 'number' ? v : undefined)}
          />
          <WellnessScaleControl label={t('checkin.energy')} value={form.energy} onChange={(v) => set('energy', v)} />
          <WellnessScaleControl label={t('checkin.fatigue')} value={form.fatigue} onChange={(v) => set('fatigue', v)} />
          <WellnessScaleControl label={t('checkin.legsFeeling')} value={form.legsFeeling} onChange={(v) => set('legsFeeling', v)} />
          <WellnessScaleControl label={t('checkin.stress')} value={form.stress} onChange={(v) => set('stress', v)} />
          <WellnessScaleControl label={t('checkin.mealQuality')} value={form.mealQuality} onChange={(v) => set('mealQuality', v)} />
          <NumberInput
            label={t('checkin.hydrationLiters')}
            min={0}
            max={20}
            step={0.5}
            value={form.hydrationLiters ?? undefined}
            onChange={(v) => set('hydrationLiters', typeof v === 'number' ? v : undefined)}
          />
          <Checkbox
            label={t('checkin.hasPainOrIllness')}
            checked={form.hasPainOrIllness ?? false}
            onChange={(e) => set('hasPainOrIllness', e.currentTarget.checked)}
          />
          <Textarea label={t('checkin.note')} minRows={2} value={form.note ?? ''} onChange={(e) => set('note', e.currentTarget.value)} />
          <Button size="md" onClick={onSubmit} loading={mutation.isPending}>
            {t('checkin.save')}
          </Button>
        </Stack>
      </Card>
    </Stack>
  );
}
