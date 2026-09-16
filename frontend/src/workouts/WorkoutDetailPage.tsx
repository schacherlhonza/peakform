import { useState } from 'react';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import { Group, NumberInput, Select, Stack, Table, Text, Textarea, Title } from '@mantine/core';
import { IconClipboardX, IconPlus, IconTrash } from '@tabler/icons-react';
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
import { Panel, CardHeader, Badge, Button, IconButton, FormField, MetricStrip, EmptyState, Skeleton, showToast } from '../design-system/components';
import classes from './WorkoutDetailPage.module.css';

const segmentTypeOptions = Object.values(WorkoutSegmentType).map((v) => ({ value: v, label: v }));

interface SegmentFormValues {
  segments: WorkoutSegmentDto[];
}

function targetLabel(s: WorkoutSegmentDto): string {
  if (s.intensityTargetType === IntensityTargetType.Rpe && s.targetRpe) return `RPE ${s.targetRpe}`;
  if (s.intensityTargetType === IntensityTargetType.Pace && s.targetPaceSecondsPerKmMin) {
    return `${s.targetPaceSecondsPerKmMin}–${s.targetPaceSecondsPerKmMax ?? ''} s/km`;
  }
  if (s.intensityTargetType === IntensityTargetType.Power && s.targetPowerWatts) return `${s.targetPowerWatts} W`;
  return '—';
}

function WorkoutDetailSkeleton() {
  return (
    <Stack gap="lg">
      <Skeleton height={28} width={280} />
      <Skeleton height={140} radius="var(--radius-panel)" />
      <Skeleton height={200} radius="var(--radius-panel)" />
    </Stack>
  );
}

/**
 * WorkoutEditor domain component — panel sections, structured segments, sticky save action.
 * See docs/DESIGN_SYSTEM.md §7.
 */
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
  if (workoutQuery.isLoading) return <WorkoutDetailSkeleton />;
  if (workoutQuery.isError || !workoutQuery.data) {
    return (
      <Panel>
        <EmptyState icon={<IconClipboardX size={28} stroke={1.6} />} title={t('workout.notFound')} />
      </Panel>
    );
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
      showToast({ tone: 'positive', message: t('workout.structure') + ' ✓' });
      setEditingStructure(false);
      await queryClient.invalidateQueries({ queryKey: getGetApiWorkoutsIdQueryKey(workoutId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const submitComment = async () => {
    if (!commentText.trim()) return;
    try {
      await commentMutation.mutateAsync({ data: { plannedWorkoutId: workoutId, text: commentText } });
      setCommentText('');
      await queryClient.invalidateQueries({ queryKey: getGetApiWorkoutsWorkoutIdCommentsQueryKey(workoutId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <div>
        <Group gap="xs" mb={4}>
          <Badge tone="info">{t(`sport.${workout.sport}`)}</Badge>
          {workout.isRestDay && <Badge tone="neutral">{t('calendar.restDay')}</Badge>}
        </Group>
        <Title className="ds-section-title" order={2}>
          {workout.isRestDay ? t('calendar.restDay') : workout.title}
        </Title>
        <Text className="ds-metadata">{workout.date}</Text>
      </div>

      <Panel>
        {workout.coachDescription && <Text className="ds-body">{workout.coachDescription}</Text>}
        {(workout.plannedDistanceMeters != null || workout.plannedDurationSeconds != null || workout.plannedElevationGainMeters != null) && (
          <MetricStrip
            metrics={[
              { label: t('workout.plannedDistance'), value: workout.plannedDistanceMeters != null ? `${(workout.plannedDistanceMeters / 1000).toFixed(1)} km` : '—' },
              { label: t('workout.plannedDuration'), value: workout.plannedDurationSeconds != null ? `${Math.round(workout.plannedDurationSeconds / 60)} min` : '—' },
              { label: t('workout.plannedElevation'), value: workout.plannedElevationGainMeters != null ? `${workout.plannedElevationGainMeters} m` : '—' },
            ]}
          />
        )}
      </Panel>

      <Panel noPadding={editingStructure}>
        <div style={{ padding: editingStructure ? '22px 24px 0' : undefined }}>
          <CardHeader
            kicker={t('workout.structure')}
            right={
              isCoach && (
                <Button
                  variant={editingStructure ? 'filled' : 'default'}
                  size="compact-sm"
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
              )
            }
          />
        </div>

        {!editingStructure ? (
          <div style={{ padding: '0 24px 22px' }}>
            {(workout.segments?.length ?? 0) === 0 ? (
              <EmptyState icon={<IconClipboardX size={24} stroke={1.6} />} title={t('workout.noSegments')} />
            ) : (
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th className="ds-eyebrow">{t('workout.segmentOrder')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('workout.segmentType')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('workout.distance')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('workout.duration')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('workout.target')}</Table.Th>
                    <Table.Th className="ds-eyebrow">{t('common.notes')}</Table.Th>
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
                        <Table.Td>{s.intensityTargetType === IntensityTargetType.Free ? '—' : targetLabel(s)}</Table.Td>
                        <Table.Td>{s.notes}</Table.Td>
                      </Table.Tr>
                    ))}
                </Table.Tbody>
              </Table>
            )}
          </div>
        ) : (
          <>
            <Stack gap="xs" p="24px">
              {fields.map((field, index) => (
                <div key={field.id} className={classes.segmentRow}>
                  <Controller
                    control={control}
                    name={`segments.${index}.order`}
                    render={({ field: f }) => (
                      <FormField label={t('workout.segmentOrder')}>
                        <NumberInput w={90} value={f.value ?? undefined} onChange={(v) => f.onChange(Number(v))} />
                      </FormField>
                    )}
                  />
                  <Controller
                    control={control}
                    name={`segments.${index}.type`}
                    render={({ field: f }) => (
                      <FormField label={t('workout.segmentType')}>
                        <Select data={segmentTypeOptions} w={160} {...f} />
                      </FormField>
                    )}
                  />
                  <Controller
                    control={control}
                    name={`segments.${index}.distanceMeters`}
                    render={({ field: f }) => (
                      <FormField label={t('workout.distance')} unit="m">
                        <NumberInput w={130} value={f.value ?? undefined} onChange={(v) => f.onChange(v === '' ? null : Number(v))} />
                      </FormField>
                    )}
                  />
                  <Controller
                    control={control}
                    name={`segments.${index}.durationSeconds`}
                    render={({ field: f }) => (
                      <FormField label={t('workout.duration')} unit="s">
                        <NumberInput w={110} value={f.value ?? undefined} onChange={(v) => f.onChange(v === '' ? null : Number(v))} />
                      </FormField>
                    )}
                  />
                  <FormField label={t('common.notes')}>
                    <Textarea w={200} {...register(`segments.${index}.notes`)} />
                  </FormField>
                  <IconButton icon={<IconTrash size={16} />} label={t('common.delete')} color="red" onClick={() => remove(index)} />
                </div>
              ))}
              <Button
                variant="default"
                leftSection={<IconPlus size={16} />}
                onClick={() => append({ order: fields.length + 1, type: WorkoutSegmentType.Main, intensityTargetType: IntensityTargetType.Free })}
              >
                {t('workout.addSegment')}
              </Button>
            </Stack>
            <div className={classes.stickyBar}>
              <Button onClick={() => void saveStructure()}>{t('common.save')}</Button>
            </div>
          </>
        )}
      </Panel>

      <Panel>
        <CardHeader kicker={t('workout.comments')} />
        <Stack gap={0}>
          {(commentsQuery.data?.length ?? 0) === 0 ? (
            <EmptyState icon={<IconClipboardX size={24} stroke={1.6} />} title={t('workout.noComments')} />
          ) : (
            commentsQuery.data?.map((c) => (
              <div key={c.id} className="ds-list-row">
                <Group gap="xs" mb={4}>
                  <Text fz={13} fw={600}>
                    {c.authorName}
                  </Text>
                  <Badge tone="neutral">{c.authorRole === 'Coach' ? t('auth.roleCoach') : t('auth.roleAthlete')}</Badge>
                  <Text className="ds-metadata">{c.createdAtUtc && new Date(c.createdAtUtc).toLocaleString('cs-CZ')}</Text>
                </Group>
                <Text className="ds-body">{c.text}</Text>
              </div>
            ))
          )}
          <Group align="end" mt="sm">
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
      </Panel>
    </Stack>
  );
}
