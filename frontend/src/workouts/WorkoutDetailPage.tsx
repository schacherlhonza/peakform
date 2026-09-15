import { useState } from 'react';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Card,
  Group,
  Loader,
  NumberInput,
  Select,
  Stack,
  Table,
  Text,
  Textarea,
  Title,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import {
  useGetApiWorkoutsId,
  getGetApiWorkoutsIdQueryKey,
  getPutApiWorkoutsIdMutationOptions,
} from '../api/generated/training-plans/training-plans';
import {
  useGetApiWorkoutsWorkoutIdComments,
  getGetApiWorkoutsWorkoutIdCommentsQueryKey,
  getPostApiCommentsMutationOptions,
} from '../api/generated/comments/comments';
import { IntensityTargetType, WorkoutSegmentType, type WorkoutSegmentDto } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { AppRole } from '../api/generated/models';

const segmentTypeOptions = Object.values(WorkoutSegmentType).map((v) => ({ value: v, label: v }));

interface SegmentFormValues {
  segments: WorkoutSegmentDto[];
}

export function WorkoutDetailPage() {
  const { workoutId } = useParams<{ workoutId: string }>();
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [editingStructure, setEditingStructure] = useState(false);
  const [commentText, setCommentText] = useState('');

  const workoutQuery = useGetApiWorkoutsId(workoutId ?? '', { query: { enabled: !!workoutId } });
  const commentsQuery = useGetApiWorkoutsWorkoutIdComments(workoutId ?? '', { query: { enabled: !!workoutId } });

  const updateMutation = useMutation(getPutApiWorkoutsIdMutationOptions());
  const commentMutation = useMutation(getPostApiCommentsMutationOptions());

  const { control, register, handleSubmit, reset } = useForm<SegmentFormValues>({
    values: { segments: workoutQuery.data?.segments ?? [] },
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'segments' });

  if (!workoutId) return null;
  if (workoutQuery.isLoading) return <Loader />;
  if (workoutQuery.isError || !workoutQuery.data) {
    return <Text c="dimmed">{t('workout.notFound')}</Text>;
  }

  const workout = workoutQuery.data;
  const isCoach = user?.role === AppRole.Coach;

  const saveStructure = handleSubmit(async (values) => {
    try {
      await updateMutation.mutateAsync({
        id: workoutId,
        data: {
          date: workout.date,
          sport: workout.sport,
          title: workout.title,
          coachDescription: workout.coachDescription,
          isRestDay: workout.isRestDay ?? false,
          plannedDistanceMeters: workout.plannedDistanceMeters,
          plannedDurationSeconds: workout.plannedDurationSeconds,
          plannedElevationGainMeters: workout.plannedElevationGainMeters,
          segments: values.segments,
        },
      });
      notifications.show({ color: 'green', message: 'Struktura tréninku byla uložena.' });
      setEditingStructure(false);
      await queryClient.invalidateQueries({ queryKey: getGetApiWorkoutsIdQueryKey(workoutId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const submitComment = async () => {
    if (!commentText.trim()) return;
    try {
      await commentMutation.mutateAsync({ data: { plannedWorkoutId: workoutId, text: commentText } });
      setCommentText('');
      await queryClient.invalidateQueries({ queryKey: getGetApiWorkoutsWorkoutIdCommentsQueryKey(workoutId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <div>
        <Group gap="xs">
          <Badge variant="light">{t(`sport.${workout.sport}`)}</Badge>
          {workout.isRestDay && <Badge color="gray">{t('calendar.restDay')}</Badge>}
        </Group>
        <Title order={2}>{workout.isRestDay ? t('calendar.restDay') : workout.title}</Title>
        <Text c="dimmed" size="sm">
          {workout.date}
        </Text>
      </div>

      <Card withBorder radius="md" p="lg">
        <Text style={{ whiteSpace: 'pre-wrap' }}>{workout.coachDescription}</Text>
        <Group mt="sm" gap="lg">
          {workout.plannedDistanceMeters != null && (
            <Text size="sm">
              {t('workout.plannedDistance')}: {(workout.plannedDistanceMeters / 1000).toFixed(1)} km
            </Text>
          )}
          {workout.plannedDurationSeconds != null && (
            <Text size="sm">
              {t('workout.plannedDuration')}: {Math.round(workout.plannedDurationSeconds / 60)} min
            </Text>
          )}
          {workout.plannedElevationGainMeters != null && (
            <Text size="sm">
              {t('workout.plannedElevation')}: {workout.plannedElevationGainMeters} m
            </Text>
          )}
        </Group>
      </Card>

      <Card withBorder radius="md" p="lg">
        <Group justify="space-between" mb="sm">
          <Title order={4}>{t('workout.structure')}</Title>
          {isCoach && (
            <Button
              size="xs"
              variant="light"
              onClick={() => {
                if (editingStructure) {
                  void saveStructure();
                } else {
                  reset({ segments: workout.segments ?? [] });
                  setEditingStructure(true);
                }
              }}
            >
              {editingStructure ? t('common.save') : t('workout.editStructure')}
            </Button>
          )}
        </Group>

        {!editingStructure ? (
          (workout.segments?.length ?? 0) === 0 ? (
            <Text c="dimmed" size="sm">
              {t('workout.noSegments')}
            </Text>
          ) : (
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('workout.segmentOrder')}</Table.Th>
                  <Table.Th>{t('workout.segmentType')}</Table.Th>
                  <Table.Th>Vzdálenost</Table.Th>
                  <Table.Th>Doba</Table.Th>
                  <Table.Th>Cíl</Table.Th>
                  <Table.Th>{t('common.notes')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {[...(workout.segments ?? [])]
                  .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
                  .map((s) => (
                    <Table.Tr key={s.id ?? s.order}>
                      <Table.Td>{s.order}</Table.Td>
                      <Table.Td>{t(`segmentType.${s.type}`)}</Table.Td>
                      <Table.Td>{s.distanceMeters ? `${s.distanceMeters} m` : '—'}</Table.Td>
                      <Table.Td>{s.durationSeconds ? `${s.durationSeconds} s` : '—'}</Table.Td>
                      <Table.Td>
                        {s.intensityTargetType === IntensityTargetType.Rpe && s.targetRpe ? `RPE ${s.targetRpe}` : ''}
                        {s.intensityTargetType === IntensityTargetType.Pace && s.targetPaceSecondsPerKmMin
                          ? `${s.targetPaceSecondsPerKmMin}–${s.targetPaceSecondsPerKmMax ?? ''} s/km`
                          : ''}
                        {s.intensityTargetType === IntensityTargetType.Power && s.targetPowerWatts ? `${s.targetPowerWatts} W` : ''}
                        {s.intensityTargetType === IntensityTargetType.Free ? '—' : ''}
                      </Table.Td>
                      <Table.Td>{s.notes}</Table.Td>
                    </Table.Tr>
                  ))}
              </Table.Tbody>
            </Table>
          )
        ) : (
          <Stack gap="xs">
            {fields.map((field, index) => (
              <Group key={field.id} align="end" wrap="wrap">
                <Controller
                  control={control}
                  name={`segments.${index}.order`}
                  render={({ field: f }) => (
                    <NumberInput label={t('workout.segmentOrder')} w={90} value={f.value ?? undefined} onChange={(v) => f.onChange(Number(v))} />
                  )}
                />
                <Controller
                  control={control}
                  name={`segments.${index}.type`}
                  render={({ field: f }) => <Select label={t('workout.segmentType')} data={segmentTypeOptions} w={160} {...f} />}
                />
                <Controller
                  control={control}
                  name={`segments.${index}.distanceMeters`}
                  render={({ field: f }) => (
                    <NumberInput label="Vzdálenost (m)" w={130} value={f.value ?? undefined} onChange={(v) => f.onChange(v === '' ? null : Number(v))} />
                  )}
                />
                <Controller
                  control={control}
                  name={`segments.${index}.durationSeconds`}
                  render={({ field: f }) => (
                    <NumberInput label="Doba (s)" w={110} value={f.value ?? undefined} onChange={(v) => f.onChange(v === '' ? null : Number(v))} />
                  )}
                />
                <Textarea label={t('common.notes')} w={200} {...register(`segments.${index}.notes`)} />
                <Button color="red" variant="subtle" onClick={() => remove(index)}>
                  <IconTrash size={16} />
                </Button>
              </Group>
            ))}
            <Button
              variant="light"
              leftSection={<IconPlus size={16} />}
              onClick={() => append({ order: fields.length + 1, type: WorkoutSegmentType.Main, intensityTargetType: IntensityTargetType.Free })}
            >
              {t('workout.addSegment')}
            </Button>
          </Stack>
        )}
      </Card>

      <Card withBorder radius="md" p="lg">
        <Title order={4} mb="sm">
          {t('workout.comments')}
        </Title>
        <Stack gap="sm">
          {(commentsQuery.data?.length ?? 0) === 0 ? (
            <Text c="dimmed" size="sm">
              {t('workout.noComments')}
            </Text>
          ) : (
            commentsQuery.data?.map((c) => (
              <Card key={c.id} withBorder radius="sm" p="sm" bg="gray.0">
                <Group gap="xs" mb={4}>
                  <Text size="sm" fw={500}>
                    {c.authorName}
                  </Text>
                  <Badge size="xs" variant="light">
                    {c.authorRole === 'Coach' ? t('auth.roleCoach') : t('auth.roleAthlete')}
                  </Badge>
                  <Text size="xs" c="dimmed">
                    {c.createdAtUtc && new Date(c.createdAtUtc).toLocaleString('cs-CZ')}
                  </Text>
                </Group>
                <Text size="sm">{c.text}</Text>
              </Card>
            ))
          )}
          <Group align="end">
            <Textarea
              flex={1}
              placeholder={t('workout.addComment')}
              value={commentText}
              onChange={(e) => setCommentText(e.currentTarget.value)}
            />
            <Button onClick={submitComment} loading={commentMutation.isPending}>
              {t('workout.addComment')}
            </Button>
          </Group>
        </Stack>
      </Card>
    </Stack>
  );
}
