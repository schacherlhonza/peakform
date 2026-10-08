import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Text, Textarea } from '@mantine/core';
import { IconMessageCircle } from '@tabler/icons-react';
import { Badge, Button, CardHeader, EmptyState, Panel, showToast } from '../design-system/components';
import {
  getGetApiRacesRaceIdCommentsQueryKey,
  getGetApiWorkoutsWorkoutIdCommentsQueryKey,
  getPostApiCommentsMutationOptions,
  useGetApiRacesRaceIdComments,
  useGetApiWorkoutsWorkoutIdComments,
} from '../api/generated/comments/comments';
import { CommentAuthorRole } from '../api/generated/models';

type Target = { workoutId: string; raceId?: never } | { raceId: string; workoutId?: never };

/** Coach ↔ athlete conversation about one planned workout or one race. */
export function CommentThread(target: Target) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [text, setText] = useState('');
  const workoutQuery = useGetApiWorkoutsWorkoutIdComments(target.workoutId ?? '', { query: { enabled: !!target.workoutId } });
  const raceQuery = useGetApiRacesRaceIdComments(target.raceId ?? '', { query: { enabled: !!target.raceId } });
  const comments = (target.raceId ? raceQuery.data : workoutQuery.data) ?? [];
  const mutation = useMutation(getPostApiCommentsMutationOptions());

  const submit = async () => {
    if (!text.trim()) return;
    try {
      await mutation.mutateAsync({ data: target.raceId ? { raceId: target.raceId, text } : { plannedWorkoutId: target.workoutId, text } });
      setText('');
      await queryClient.invalidateQueries({
        queryKey: target.raceId ? getGetApiRacesRaceIdCommentsQueryKey(target.raceId) : getGetApiWorkoutsWorkoutIdCommentsQueryKey(target.workoutId!),
      });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Panel>
      <CardHeader kicker={t('workout.comments')} />
      <Stack gap={0}>
        {comments.length === 0 ? (
          <EmptyState icon={<IconMessageCircle size={24} stroke={1.6} />} title={t('workout.noComments')} />
        ) : (
          comments.map((c) => (
            <div key={c.id} className="ds-list-row">
              <Group gap="xs" mb={4}>
                <Text fz={13} fw={600}>
                  {c.authorName}
                </Text>
                <Badge tone={c.authorRole === CommentAuthorRole.Coach ? 'info' : 'neutral'}>
                  {c.authorRole === CommentAuthorRole.Coach ? t('auth.roleCoach') : t('auth.roleAthlete')}
                </Badge>
                <Text className="ds-metadata">{c.createdAtUtc && new Date(c.createdAtUtc).toLocaleString('cs-CZ')}</Text>
              </Group>
              <Text className="ds-body" style={{ whiteSpace: 'pre-wrap' }}>
                {c.text}
              </Text>
            </div>
          ))
        )}
        <Group align="end" mt="sm">
          <Textarea flex={1} autosize minRows={1} maxRows={6} placeholder={t('workout.addComment')} value={text} onChange={(e) => setText(e.currentTarget.value)} />
          <Button onClick={() => void submit()} loading={mutation.isPending} disabled={!text.trim()}>
            {t('workout.addComment')}
          </Button>
        </Group>
      </Stack>
    </Panel>
  );
}
