import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Text, TextInput } from '@mantine/core';
import { Panel, CardHeader, Button, FormField, showToast } from '../design-system/components';
import {
  useGetApiAthletesAthleteUserIdThresholds,
  getGetApiAthletesAthleteUserIdThresholdsQueryKey,
  getPutApiAthletesAthleteUserIdThresholdsMutationOptions,
} from '../api/generated/heart-rate-zones/heart-rate-zones';
import { formatClock } from '../activities/activityFormat';
import { parseClock } from '../workouts/segments/segmentFormat';

/**
 * Running threshold pace. Saved to PeakForm and written to intervals.icu with the zones — intervals.icu
 * won't export run workouts with pace targets to Garmin without it.
 */
export function ThresholdPacePanel({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const query = useGetApiAthletesAthleteUserIdThresholds(athleteUserId, { query: { enabled: !!athleteUserId } });
  const mutation = useMutation(getPutApiAthletesAthleteUserIdThresholdsMutationOptions());
  const saved = query.data?.thresholdPaceSecondsPerKm ?? null;
  // Text being edited; null shows the saved value.
  const [draft, setDraft] = useState<string | null>(null);
  const text = draft ?? (saved ? formatClock(saved) : '');
  const seconds = text.trim() ? parseClock(text) : null;
  const invalid = text.trim() !== '' && (seconds === null || seconds < 120 || seconds > 900);

  const save = async () => {
    if (invalid) return;
    try {
      await mutation.mutateAsync({ athleteUserId, data: { thresholdPaceSecondsPerKm: seconds } });
      setDraft(null);
      showToast({ tone: 'positive', message: t('settings.thresholdPaceSaved') });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdThresholdsQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Panel>
      <CardHeader kicker={t('settings.thresholdPace')} />
      <Stack gap="sm">
        <Text className="ds-body">{t('settings.thresholdPaceHint')}</Text>
        <Group align="flex-end" gap="sm">
          <FormField label={t('settings.thresholdPace')} unit="min:s /km" error={invalid ? t('settings.thresholdPaceInvalid') : undefined}>
            <TextInput w={140} placeholder="4:30" value={text} onChange={(e) => setDraft(e.currentTarget.value)} />
          </FormField>
          <Button onClick={() => void save()} loading={mutation.isPending} disabled={invalid || draft === null}>
            {t('common.save')}
          </Button>
        </Group>
      </Stack>
    </Panel>
  );
}
