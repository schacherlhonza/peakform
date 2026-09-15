import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Avatar, Badge, Button, Card, Checkbox, Group, Loader, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  useGetApiRelationships,
  getGetApiRelationshipsQueryKey,
  getPutApiRelationshipsIdPermissionsMutationOptions,
  getPostApiRelationshipsIdRevokeMutationOptions,
} from '../api/generated/relationships/relationships';
import { PermissionScope, RelationshipStatus } from '../api/generated/models';

const scopes = Object.values(PermissionScope);

export default function PermissionsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

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
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleRevoke = async (relationshipId: string) => {
    if (!window.confirm(t('relationships.revokeConfirm'))) return;
    try {
      await revokeMutation.mutateAsync({ id: relationshipId, data: {} });
      await invalidate();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nav.permissions')}</Title>

      {relationshipsQuery.isLoading ? (
        <Loader />
      ) : relationships.length === 0 ? (
        <Card withBorder radius="md" p="xl">
          <Text c="dimmed" ta="center">
            {t('settings.noCoaches')}
          </Text>
        </Card>
      ) : (
        <Stack gap="md">
          {relationships.map((rel) => {
            const isTogglingScope = (scope: PermissionScope) =>
              permissionMutation.isPending && permissionMutation.variables?.id === rel.id && permissionMutation.variables?.data?.scope === scope;
            return (
              <Card key={rel.id} withBorder radius="md" p="lg">
                <Group justify="space-between" mb="sm">
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
                    <Badge color="green" variant="light">
                      {t(`relationships.status.${rel.status}`)}
                    </Badge>
                  </Group>
                  <Button
                    size="xs"
                    variant="subtle"
                    color="red"
                    loading={revokeMutation.isPending && revokeMutation.variables?.id === rel.id}
                    onClick={() => void handleRevoke(rel.id!)}
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
              </Card>
            );
          })}
        </Stack>
      )}
    </Stack>
  );
}
