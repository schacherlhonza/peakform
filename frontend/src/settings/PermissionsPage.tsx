import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Avatar, Checkbox, Group, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import { Panel, Badge, Button, Modal, Skeleton, EmptyState, showToast } from '../design-system/components';
import {
  useGetApiRelationships,
  getGetApiRelationshipsQueryKey,
  getPutApiRelationshipsIdPermissionsMutationOptions,
  getPostApiRelationshipsIdRevokeMutationOptions,
  getPostApiRelationshipsIdRespondMutationOptions,
} from '../api/generated/relationships/relationships';
import { PermissionScope, RelationshipStatus } from '../api/generated/models';
import type { CoachAthleteRelationshipDto } from '../api/generated/models';

const scopes = Object.values(PermissionScope);

function PendingInviteRow({
  relationship,
  onRespond,
  isResponding,
}: {
  relationship: CoachAthleteRelationshipDto;
  onRespond: (relationship: CoachAthleteRelationshipDto, accept: boolean) => void;
  isResponding: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Panel>
      <Group justify="space-between" wrap="nowrap">
        <Group gap="sm" wrap="nowrap" style={{ minWidth: 0 }}>
          <Avatar radius="xl" color="brand">
            {relationship.coachName?.[0] ?? '?'}
          </Avatar>
          <div style={{ minWidth: 0 }}>
            <Text fw={500} truncate>
              {t('relationships.invitedFrom', { name: relationship.coachName })}
            </Text>
            <Text size="xs" c="dimmed" truncate>
              {relationship.coachEmail}
            </Text>
            {relationship.inviteNote ? (
              <Text size="xs" c="dimmed" mt={4}>
                {relationship.inviteNote}
              </Text>
            ) : null}
          </div>
        </Group>
        <Group gap="xs" wrap="nowrap">
          <Button size="xs" variant="default" disabled={isResponding} onClick={() => onRespond(relationship, false)}>
            {t('relationships.decline')}
          </Button>
          <Button size="xs" disabled={isResponding} onClick={() => onRespond(relationship, true)}>
            {t('relationships.accept')}
          </Button>
        </Group>
      </Group>
    </Panel>
  );
}

function PermissionsSkeleton() {
  return (
    <Stack gap="md">
      {[0, 1].map((i) => (
        <Skeleton key={i} height={140} radius="var(--radius-panel)" />
      ))}
    </Stack>
  );
}

export default function PermissionsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [pendingRevoke, setPendingRevoke] = useState<CoachAthleteRelationshipDto | null>(null);

  const relationshipsQuery = useGetApiRelationships();
  const allRelationships = relationshipsQuery.data ?? [];
  const relationships = allRelationships.filter((r) => r.status === RelationshipStatus.Active);
  const pendingInvites = allRelationships.filter((r) => r.status === RelationshipStatus.PendingInvite);

  const permissionMutation = useMutation(getPutApiRelationshipsIdPermissionsMutationOptions());
  const revokeMutation = useMutation(getPostApiRelationshipsIdRevokeMutationOptions());
  const respondMutation = useMutation(getPostApiRelationshipsIdRespondMutationOptions());

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiRelationshipsQueryKey() });

  const handleRespond = async (relationship: CoachAthleteRelationshipDto, accept: boolean) => {
    if (!relationship.id) return;
    try {
      await respondMutation.mutateAsync({ id: relationship.id, data: { accept } });
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleToggle = async (relationshipId: string, scope: PermissionScope, granted: boolean) => {
    try {
      await permissionMutation.mutateAsync({ id: relationshipId, data: { scope, granted } });
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const confirmRevoke = async () => {
    if (!pendingRevoke?.id) return;
    try {
      await revokeMutation.mutateAsync({ id: pendingRevoke.id, data: {} });
      setPendingRevoke(null);
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('nav.permissions')}
      </Title>

      {!relationshipsQuery.isLoading && pendingInvites.length > 0 ? (
        <Stack gap="md">
          <Text className="ds-eyebrow">{t('relationships.pendingInvites')}</Text>
          {pendingInvites.map((rel) => (
            <PendingInviteRow
              key={rel.id}
              relationship={rel}
              isResponding={respondMutation.isPending && respondMutation.variables?.id === rel.id}
              onRespond={(relationship, accept) => void handleRespond(relationship, accept)}
            />
          ))}
        </Stack>
      ) : null}

      {relationshipsQuery.isLoading ? (
        <PermissionsSkeleton />
      ) : relationships.length === 0 ? (
        <Panel>
          <EmptyState icon={<IconLock size={28} stroke={1.6} />} title={t('settings.noCoaches')} />
        </Panel>
      ) : (
        <Stack gap="md">
          {relationships.map((rel) => {
            const isTogglingScope = (scope: PermissionScope) =>
              permissionMutation.isPending && permissionMutation.variables?.id === rel.id && permissionMutation.variables?.data?.scope === scope;
            return (
              <Panel key={rel.id}>
                <Group justify="space-between" mb="sm" align="flex-start">
                  <Group gap="sm">
                    <Avatar radius="xl" color="brand">
                      {rel.coachName?.[0] ?? '?'}
                    </Avatar>
                    <div>
                      <Text fw={500}>{rel.coachName}</Text>
                      <Text size="xs" c="dimmed">
                        {rel.coachEmail}
                      </Text>
                    </div>
                    <Badge tone="positive">{t(`relationships.status.${rel.status}`)}</Badge>
                  </Group>
                  <Button
                    size="xs"
                    variant="subtle"
                    color="red"
                    loading={revokeMutation.isPending && revokeMutation.variables?.id === rel.id}
                    onClick={() => setPendingRevoke(rel)}
                  >
                    {t('relationships.revoke')}
                  </Button>
                </Group>

                <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="xs">
                  {scopes.map((scope) => (
                    <Checkbox
                      key={scope}
                      label={t(`permissions.scope.${scope}`)}
                      checked={rel.grantedScopes?.includes(scope) ?? false}
                      disabled={isTogglingScope(scope)}
                      onChange={(e) => void handleToggle(rel.id!, scope, e.currentTarget.checked)}
                    />
                  ))}
                </SimpleGrid>
              </Panel>
            );
          })}
        </Stack>
      )}

      <Modal opened={pendingRevoke !== null} onClose={() => setPendingRevoke(null)} title={t('relationships.revoke')}>
        <Stack gap="md">
          <Text className="ds-body">{t('relationships.revokeConfirm')}</Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPendingRevoke(null)}>
              {t('common.cancel')}
            </Button>
            <Button color="red" loading={revokeMutation.isPending} onClick={() => void confirmRevoke()}>
              {t('relationships.revoke')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
