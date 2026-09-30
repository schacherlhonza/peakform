import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Divider, Group, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { IconArrowLeft, IconChecklist } from '@tabler/icons-react';
import { useAuth } from '../auth/AuthContext';
import { Panel, CardHeader, Badge, Button, Skeleton, EmptyState, showToast } from '../design-system/components';
import {
  useGetApiAthletesAthleteUserIdDuplicateCandidates,
  getGetApiAthletesAthleteUserIdDuplicateCandidatesQueryKey,
  getPostApiDuplicateCandidatesIdMergeMutationOptions,
  getPostApiDuplicateCandidatesIdDismissMutationOptions,
  getPostApiMergeDecisionsIdRevertMutationOptions,
  useGetApiAthletesAthleteUserIdMergeDecisions,
  getGetApiAthletesAthleteUserIdMergeDecisionsQueryKey,
} from '../api/generated/duplicate-review/duplicate-review';
import { MergeDecisionOutcome, type CompletedActivityDto, type DuplicateCandidateDto } from '../api/generated/models';

function ActivitySummary({ activity }: { activity: CompletedActivityDto }) {
  const { t } = useTranslation();
  return (
    <Stack gap={2}>
      <Text className="ds-body" fw={600}>
        {activity.title || t(`sport.${activity.sport}`)}
      </Text>
      <Text className="ds-metadata">{activity.startedAtUtc ? new Date(activity.startedAtUtc).toLocaleString('cs-CZ') : '—'}</Text>
      <Text className="ds-metadata">
        {t(`sport.${activity.sport}`)} · {Math.round((activity.durationSeconds ?? 0) / 60)} min
        {activity.distanceMeters ? ` · ${(activity.distanceMeters / 1000).toFixed(1)} km` : ''}
      </Text>
      <Text className="ds-metadata">{t(`integrations.provider.${activity.source}`, { defaultValue: activity.source })}</Text>
    </Stack>
  );
}

// Weights mirror backend/src/TrainCoach.Application/Integrations/Matching/ActivityMatchingService.cs
// (Score method) — kept in sync manually since the breakdown JSON itself only carries raw points,
// not the denominators.
const SCORE_WEIGHTS = { sport: 40, time: 30, duration: 15, distance: 15 } as const;

function ScoreBreakdown({ scoringBreakdownJson }: { scoringBreakdownJson?: string | null }) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(false);

  let breakdown: Record<string, number> | null = null;
  try {
    breakdown = scoringBreakdownJson ? JSON.parse(scoringBreakdownJson) : null;
  } catch {
    breakdown = null;
  }

  if (!breakdown) return null;

  if (breakdown.disqualifiedBySport) {
    return <Text className="ds-metadata">{t('duplicateReview.breakdown.disqualifiedBySport')}</Text>;
  }

  return (
    <Stack gap={4}>
      <Button size="xs" variant="subtle" onClick={() => setExpanded((v) => !v)}>
        {t('duplicateReview.scoreBreakdown')}
      </Button>
      {expanded && (
        <Stack gap={2} mt={4}>
          <Text className="ds-metadata">
            {t('duplicateReview.breakdown.sport')}: {Math.round(breakdown.sport ?? 0)}/{SCORE_WEIGHTS.sport}
          </Text>
          <Text className="ds-metadata">
            {t('duplicateReview.breakdown.time')}: {Math.round(breakdown.time ?? 0)}/{SCORE_WEIGHTS.time}
          </Text>
          <Text className="ds-metadata">
            {t('duplicateReview.breakdown.duration')}: {Math.round(breakdown.duration ?? 0)}/{SCORE_WEIGHTS.duration}
          </Text>
          <Text className="ds-metadata">
            {breakdown.hasDistanceBoth
              ? `${t('duplicateReview.breakdown.distance')}: ${Math.round(breakdown.distance ?? 0)}/${SCORE_WEIGHTS.distance}`
              : t('duplicateReview.breakdown.distanceExcluded')}
          </Text>
          {!!breakdown.deviceBonus && (
            <Text className="ds-metadata">
              {t('duplicateReview.breakdown.deviceBonus')}: +{Math.round(breakdown.deviceBonus)}
            </Text>
          )}
        </Stack>
      )}
    </Stack>
  );
}

function CandidateCard({ candidate, athleteUserId }: { candidate: DuplicateCandidateDto; athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const mergeMutation = useMutation(getPostApiDuplicateCandidatesIdMergeMutationOptions());
  const dismissMutation = useMutation(getPostApiDuplicateCandidatesIdDismissMutationOptions());

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdDuplicateCandidatesQueryKey(athleteUserId) });

  const handleMerge = async (survivingActivityId: string) => {
    try {
      await mergeMutation.mutateAsync({ id: candidate.id!, data: { survivingActivityId } });
      showToast({ tone: 'positive', message: t('duplicateReview.merged') });
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('duplicateReview.mergeError') });
    }
  };

  const handleDismiss = async () => {
    try {
      await dismissMutation.mutateAsync({ id: candidate.id! });
      showToast({ tone: 'positive', message: t('duplicateReview.dismissed') });
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('duplicateReview.dismissError') });
    }
  };

  const busy = mergeMutation.isPending || dismissMutation.isPending;

  return (
    <Panel>
      <CardHeader kicker={t('duplicateReview.confidence', { score: candidate.confidenceScore })} />
      <ScoreBreakdown scoringBreakdownJson={candidate.scoringBreakdownJson} />
      <SimpleGrid cols={{ base: 1, sm: 2 }} mb="md" mt="sm">
        <Stack gap="xs">
          <ActivitySummary activity={candidate.activityA!} />
          <Button size="xs" variant="light" loading={busy} onClick={() => void handleMerge(candidate.activityA!.id!)}>
            {t('duplicateReview.keep')}
          </Button>
        </Stack>
        <Stack gap="xs">
          <ActivitySummary activity={candidate.activityB!} />
          <Button size="xs" variant="light" loading={busy} onClick={() => void handleMerge(candidate.activityB!.id!)}>
            {t('duplicateReview.keep')}
          </Button>
        </Stack>
      </SimpleGrid>
      <Group justify="flex-end">
        <Button size="xs" variant="subtle" color="red" loading={busy} onClick={() => void handleDismiss()}>
          {t('duplicateReview.differentActivity')}
        </Button>
      </Group>
    </Panel>
  );
}

function outcomeTone(outcome?: string) {
  if (outcome === MergeDecisionOutcome.Merged) return 'positive' as const;
  if (outcome === MergeDecisionOutcome.Reverted) return 'neutral' as const;
  return 'neutral' as const;
}

function MergeHistorySection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const decisionsQuery = useGetApiAthletesAthleteUserIdMergeDecisions(athleteUserId);
  const revertMutation = useMutation(getPostApiMergeDecisionsIdRevertMutationOptions());

  const handleRevert = async (id: string) => {
    try {
      await revertMutation.mutateAsync({ id });
      showToast({ tone: 'positive', message: t('duplicateReview.reverted') });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdMergeDecisionsQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('duplicateReview.revertError') });
    }
  };

  if (decisionsQuery.isLoading) {
    return <Skeleton height={80} />;
  }

  const decisions = decisionsQuery.data ?? [];
  if (decisions.length === 0) {
    return null;
  }

  return (
    <Stack gap="xs">
      <Text className="ds-eyebrow">{t('duplicateReview.history')}</Text>
      {decisions.map((d) => (
        <Group key={d.id} justify="space-between" gap="xs" className="ds-list-row">
          <Stack gap={0}>
            <Text className="ds-metadata">{d.decidedAtUtc ? new Date(d.decidedAtUtc).toLocaleString('cs-CZ') : '—'}</Text>
            <Text className="ds-metadata">{t(`duplicateReview.kind.${d.kind}`)}</Text>
          </Stack>
          <Badge tone={outcomeTone(d.outcome)}>{t(`duplicateReview.outcome.${d.outcome}`)}</Badge>
          {d.outcome === MergeDecisionOutcome.Merged && !d.revertedAtUtc && (
            <Button size="xs" variant="subtle" loading={revertMutation.isPending} onClick={() => void handleRevert(d.id!)}>
              {t('duplicateReview.revert')}
            </Button>
          )}
          {d.revertedAtUtc && (
            <Text className="ds-metadata">
              {t('duplicateReview.revertedOn')}: {new Date(d.revertedAtUtc).toLocaleString('cs-CZ')}
            </Text>
          )}
        </Group>
      ))}
    </Stack>
  );
}

export default function DuplicateReviewPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  const candidatesQuery = useGetApiAthletesAthleteUserIdDuplicateCandidates(athleteUserId);

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Title className="ds-page-title" order={2}>
          {t('duplicateReview.title')}
        </Title>
        <Button component={Link} to="/settings/integrations" variant="subtle" leftSection={<IconArrowLeft size={16} />}>
          {t('duplicateReview.back')}
        </Button>
      </Group>

      {candidatesQuery.isLoading ? (
        <Stack gap="md">
          <Skeleton height={140} />
          <Skeleton height={140} />
        </Stack>
      ) : (candidatesQuery.data?.length ?? 0) === 0 ? (
        <EmptyState icon={<IconChecklist size={28} stroke={1.6} />} title={t('duplicateReview.empty')} />
      ) : (
        <Stack gap="md">
          {candidatesQuery.data!.map((candidate) => (
            <CandidateCard key={candidate.id} candidate={candidate} athleteUserId={athleteUserId} />
          ))}
        </Stack>
      )}

      <Divider />
      <MergeHistorySection athleteUserId={athleteUserId} />
    </Stack>
  );
}
