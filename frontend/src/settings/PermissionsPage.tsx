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
} from '../api/generated/relationships/relationships';
import { PermissionScope, RelationshipStatus } from '../api/generated/models';
import type { CoachAthleteRelationshipDto } from '../api/generated/models';

const scopes = Object.values(PermissionScope);

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
  const relationships = (relationshipsQuery.data ?? []).filter((r) => r.status === RelationshipStatus.Active);

  const permissionMutation = useMutation(getPutApiRelationshipsIdPermissionsMutationOptions());
  const revokeMutation = useMutation(getPostApiRelationshipsIdRevokeMutationOptions());

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiRelationshipsQueryKey() });

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
