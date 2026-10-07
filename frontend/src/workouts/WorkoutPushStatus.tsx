import { useTranslation } from 'react-i18next';
import { Group, Stack, Text } from '@mantine/core';
import { IconDeviceWatch } from '@tabler/icons-react';
import { Badge, type BadgeTone } from '../design-system/components';
import { useGetApiWorkoutsIdPushStatus } from '../api/generated/training-plans/training-plans';
import { WorkoutPushStatus as PushStatus } from '../api/generated/models';

const tone: Record<PushStatus, BadgeTone> = {
  [PushStatus.Pushed]: 'positive',
  [PushStatus.Removed]: 'neutral',
  [PushStatus.Failed]: 'danger',
};

/**
 * Whether the workout reached the athlete's watch calendar (intervals.icu → Garmin), why not, and what
 * won't arrive as planned. Nothing when the athlete doesn't push workouts. Pushing runs in the
 * background, so this polls briefly after edits.
 */
export function WorkoutPushStatusLine({ workoutId }: { workoutId: string }) {
  const { t } = useTranslation();
  const query = useGetApiWorkoutsIdPushStatus(workoutId, { query: { refetchInterval: 10_000 } });
  const records = query.data ?? [];
  if (records.length === 0) return null;

  return (
    <Stack gap={4} mt={6}>
      {records.map((r) => (
        <Stack key={r.provider} gap={2}>
          <Group gap={6}>
            <IconDeviceWatch size={14} aria-hidden />
            {r.status && <Badge tone={tone[r.status]}>{t(`workoutPush.status.${r.status}`)}</Badge>}
            {r.status === PushStatus.Pushed && r.pushedAtUtc && (
              <Text className="ds-metadata">{new Date(r.pushedAtUtc).toLocaleString('cs-CZ')}</Text>
            )}
          </Group>
          {r.error && (
            <Text fz={12} c="var(--color-danger)">
              {r.error}
            </Text>
          )}
          {(r.warnings ?? []).map((w) => (
            <Text key={w} fz={12} c="var(--color-warning)">
              {t(`workoutPush.warning.${w}`, { defaultValue: w })}
            </Text>
          ))}
        </Stack>
      ))}
    </Stack>
  );
}
