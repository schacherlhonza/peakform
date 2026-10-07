import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Stack, Switch, Text } from '@mantine/core';
import { showToast } from '../design-system/components';
import {
  getGetApiIntegrationsQueryKey,
  getPutApiIntegrationsProviderPushPlannedWorkoutsMutationOptions,
} from '../api/generated/integration-connections/integration-connections';
import type { IntegrationConnectionDto, IntegrationProviderType } from '../api/generated/models';

/**
 * The athlete's consent to put coach-planned workouts on their intervals.icu calendar — and from there
 * on the Garmin watch. Off by default; the onward upload to Garmin is the athlete's own intervals.icu
 * setting, which PeakForm can't see, so the hint spells it out.
 */
export function WorkoutPushSetting({ provider, connection }: { provider: IntegrationProviderType; connection: IntegrationConnectionDto | undefined }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const mutation = useMutation(getPutApiIntegrationsProviderPushPlannedWorkoutsMutationOptions());
  const enabled = connection?.pushPlannedWorkouts ?? false;

  const toggle = async (next: boolean) => {
    try {
      await mutation.mutateAsync({ provider, data: { enabled: next } });
      showToast({ tone: 'positive', message: t(next ? 'integrations.workoutPushOn' : 'integrations.workoutPushOff') });
      await queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsQueryKey() });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap={4} mb="sm">
      <Switch
        checked={enabled}
        disabled={mutation.isPending}
        onChange={(e) => void toggle(e.currentTarget.checked)}
        label={t('integrations.workoutPushLabel')}
      />
      <Text className="ds-metadata">{t('integrations.workoutPushHint')}</Text>
    </Stack>
  );
}
