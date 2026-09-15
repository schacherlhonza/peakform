import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Badge, Button, Card, Group, Loader, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  useGetApiIntegrations,
  useGetApiIntegrationsProviderSyncHistory,
  getApiIntegrationsProviderAuthorizeUrl,
  getGetApiIntegrationsQueryKey,
  getGetApiIntegrationsProviderSyncHistoryQueryKey,
  getPostApiIntegrationsProviderConnectDemoMutationOptions,
  getDeleteApiIntegrationsProviderMutationOptions,
  getPostApiIntegrationsProviderSyncMutationOptions,
} from '../api/generated/integration-connections/integration-connections';
import {
  IntegrationConnectionStatus,
  IntegrationProviderType,
  SyncRunStatus,
  type IntegrationConnectionDto,
  type SynchronizationRunDto,
} from '../api/generated/models';

interface ProviderCardProps {
  provider: IntegrationProviderType;
  isDemo: boolean;
  connection?: IntegrationConnectionDto;
  history?: SynchronizationRunDto[];
  onConnectDemo: () => void;
  onDisconnect: () => void;
  onSync: () => void;
  connectPending: boolean;
  disconnectPending: boolean;
  syncPending: boolean;
}

function statusColor(status?: string): string {
  if (status === SyncRunStatus.Succeeded) return 'green';
  if (status === SyncRunStatus.Failed) return 'red';
  return 'gray';
}

function ProviderCard({
  provider,
  isDemo,
  connection,
  history,
  onConnectDemo,
  onDisconnect,
  onSync,
  connectPending,
  disconnectPending,
  syncPending,
}: ProviderCardProps) {
  const { t } = useTranslation();
  const [authorizing, setAuthorizing] = useState(false);
  const connected = connection?.status === IntegrationConnectionStatus.Connected;

  const handleConnectStrava = async () => {
    setAuthorizing(true);
    try {
      const result = await getApiIntegrationsProviderAuthorizeUrl(provider);
      if (result.url) {
        window.location.href = result.url;
      } else {
        notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
      }
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    } finally {
      setAuthorizing(false);
    }
  };

  return (
    <Card withBorder radius="md" p="lg">
      <Stack gap="sm">
        <Group justify="space-between">
          <Text fw={600}>{t(`integrations.provider.${provider}`)}</Text>
          <Badge color={connected ? 'green' : 'gray'} variant="light">
            {t(connected ? 'integrations.connected' : 'integrations.notConnected')}
          </Badge>
        </Group>

        {connected && (
          <Text size="xs" c="dimmed">
            {t('integrations.lastSynced')}:{' '}
            {connection?.lastSyncedAtUtc ? new Date(connection.lastSyncedAtUtc).toLocaleString('cs-CZ') : t('integrations.neverSynced')}
          </Text>
        )}

        <Group gap="xs">
          {!connected && isDemo && (
            <Button size="xs" onClick={onConnectDemo} loading={connectPending}>
              {t('integrations.connectDemo')}
            </Button>
          )}
          {!connected && !isDemo && (
            <Button size="xs" onClick={() => void handleConnectStrava()} loading={authorizing}>
              {t('integrations.connectStrava')}
            </Button>
          )}
          {connected && (
            <>
              <Button size="xs" variant="light" onClick={onSync} loading={syncPending}>
                {t('integrations.syncNow')}
              </Button>
              <Button size="xs" variant="subtle" color="red" onClick={onDisconnect} loading={disconnectPending}>
                {t('integrations.disconnect')}
              </Button>
            </>
          )}
        </Group>

        {connected && (history?.length ?? 0) > 0 && (
          <Stack gap={4} mt="xs">
            <Text size="xs" fw={500} c="dimmed">
              {t('integrations.syncHistory')}
            </Text>
            {history!.slice(0, 5).map((run) => (
              <Group key={run.id} justify="space-between" gap="xs">
                <Text size="xs" c="dimmed">
                  {run.startedAtUtc ? new Date(run.startedAtUtc).toLocaleString('cs-CZ') : '—'}
                </Text>
                <Badge size="xs" variant="light" color={statusColor(run.status)}>
                  {run.status}
                </Badge>
                <Text size="xs" c="dimmed">
                  +{run.itemsCreated ?? 0}
                </Text>
              </Group>
            ))}
          </Stack>
        )}
      </Stack>
    </Card>
  );
}

export default function IntegrationsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const connectionsQuery = useGetApiIntegrations();
  const connections = connectionsQuery.data ?? [];
  const findConnection = (provider: IntegrationProviderType) => connections.find((c) => c.provider === provider);

  const stravaConnected = findConnection(IntegrationProviderType.Strava)?.status === IntegrationConnectionStatus.Connected;
  const garminConnected = findConnection(IntegrationProviderType.GarminDemoProvider)?.status === IntegrationConnectionStatus.Connected;
  const mySasyConnected = findConnection(IntegrationProviderType.MySasyDemoProvider)?.status === IntegrationConnectionStatus.Connected;

  const stravaHistory = useGetApiIntegrationsProviderSyncHistory(IntegrationProviderType.Strava, {
    query: { enabled: stravaConnected },
  });
  const garminHistory = useGetApiIntegrationsProviderSyncHistory(IntegrationProviderType.GarminDemoProvider, {
    query: { enabled: garminConnected },
  });
  const mySasyHistory = useGetApiIntegrationsProviderSyncHistory(IntegrationProviderType.MySasyDemoProvider, {
    query: { enabled: mySasyConnected },
  });

  const connectDemoMutation = useMutation(getPostApiIntegrationsProviderConnectDemoMutationOptions());
  const disconnectMutation = useMutation(getDeleteApiIntegrationsProviderMutationOptions());
  const syncMutation = useMutation(getPostApiIntegrationsProviderSyncMutationOptions());

  const invalidateConnections = () => queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsQueryKey() });

  const handleConnectDemo = async (provider: IntegrationProviderType) => {
    try {
      await connectDemoMutation.mutateAsync({ provider });
      notifications.show({ color: 'green', message: t('integrations.connected') });
      await invalidateConnections();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleDisconnect = async (provider: IntegrationProviderType) => {
    try {
      await disconnectMutation.mutateAsync({ provider });
      await invalidateConnections();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleSync = async (provider: IntegrationProviderType) => {
    try {
      await syncMutation.mutateAsync({ provider });
      notifications.show({ color: 'green', message: t('integrations.syncStarted') });
      await invalidateConnections();
      await queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsProviderSyncHistoryQueryKey(provider) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  if (connectionsQuery.isLoading) return <Loader />;

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nav.integrations')}</Title>
      <SimpleGrid cols={{ base: 1, sm: 3 }}>
        <ProviderCard
          provider={IntegrationProviderType.Strava}
          isDemo={false}
          connection={findConnection(IntegrationProviderType.Strava)}
          history={stravaHistory.data}
          onConnectDemo={() => void handleConnectDemo(IntegrationProviderType.Strava)}
          onDisconnect={() => void handleDisconnect(IntegrationProviderType.Strava)}
          onSync={() => void handleSync(IntegrationProviderType.Strava)}
          connectPending={connectDemoMutation.isPending && connectDemoMutation.variables?.provider === IntegrationProviderType.Strava}
          disconnectPending={disconnectMutation.isPending && disconnectMutation.variables?.provider === IntegrationProviderType.Strava}
          syncPending={syncMutation.isPending && syncMutation.variables?.provider === IntegrationProviderType.Strava}
        />
        <ProviderCard
          provider={IntegrationProviderType.GarminDemoProvider}
          isDemo
          connection={findConnection(IntegrationProviderType.GarminDemoProvider)}
          history={garminHistory.data}
          onConnectDemo={() => void handleConnectDemo(IntegrationProviderType.GarminDemoProvider)}
          onDisconnect={() => void handleDisconnect(IntegrationProviderType.GarminDemoProvider)}
          onSync={() => void handleSync(IntegrationProviderType.GarminDemoProvider)}
          connectPending={
            connectDemoMutation.isPending && connectDemoMutation.variables?.provider === IntegrationProviderType.GarminDemoProvider
          }
          disconnectPending={
            disconnectMutation.isPending && disconnectMutation.variables?.provider === IntegrationProviderType.GarminDemoProvider
          }
          syncPending={syncMutation.isPending && syncMutation.variables?.provider === IntegrationProviderType.GarminDemoProvider}
        />
        <ProviderCard
          provider={IntegrationProviderType.MySasyDemoProvider}
          isDemo
          connection={findConnection(IntegrationProviderType.MySasyDemoProvider)}
          history={mySasyHistory.data}
          onConnectDemo={() => void handleConnectDemo(IntegrationProviderType.MySasyDemoProvider)}
          onDisconnect={() => void handleDisconnect(IntegrationProviderType.MySasyDemoProvider)}
          onSync={() => void handleSync(IntegrationProviderType.MySasyDemoProvider)}
          connectPending={
            connectDemoMutation.isPending && connectDemoMutation.variables?.provider === IntegrationProviderType.MySasyDemoProvider
          }
          disconnectPending={
            disconnectMutation.isPending && disconnectMutation.variables?.provider === IntegrationProviderType.MySasyDemoProvider
          }
          syncPending={syncMutation.isPending && syncMutation.variables?.provider === IntegrationProviderType.MySasyDemoProvider}
        />
      </SimpleGrid>
    </Stack>
  );
}
