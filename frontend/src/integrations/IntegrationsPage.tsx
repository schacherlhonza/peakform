import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, Button, Skeleton, EmptyState, showToast } from '../design-system/components';
import type { BadgeTone } from '../design-system/components';
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
  connectionsLoading: boolean;
  connectionsError: boolean;
  onRetryConnections: () => void;
  history?: SynchronizationRunDto[];
  historyLoading: boolean;
  historyError: boolean;
  onConnectDemo: () => void;
  onDisconnect: () => void;
  onSync: () => void;
  connectPending: boolean;
  disconnectPending: boolean;
  syncPending: boolean;
}

function statusTone(status?: string): BadgeTone {
  if (status === SyncRunStatus.Succeeded) return 'positive';
  if (status === SyncRunStatus.Failed) return 'danger';
  return 'neutral';
}

/** Card-shaped skeleton — mirrors the connected layout (status badge + action button + history rows)
 * so the page never collapses behind a single blocking spinner (docs/DESIGN_SYSTEM.md §9). */
function ProviderCardSkeleton({ provider }: { provider: IntegrationProviderType }) {
  const { t } = useTranslation();
  return (
    <Panel>
      <CardHeader kicker={t(`integrations.provider.${provider}`)} right={<Skeleton height={20} width={78} radius="xl" />} />
      <Skeleton height={12} width={160} mb="sm" />
      <Skeleton height={30} width={110} radius="var(--radius-button)" mb="md" />
      <Stack gap={6}>
        <Skeleton height={9} width={96} />
        <Skeleton height={18} />
        <Skeleton height={18} />
      </Stack>
    </Panel>
  );
}

function ProviderCard({
  provider,
  isDemo,
  connection,
  connectionsLoading,
  connectionsError,
  onRetryConnections,
  history,
  historyLoading,
  historyError,
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
        showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
      }
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    } finally {
      setAuthorizing(false);
    }
  };

  if (connectionsLoading) {
    return <ProviderCardSkeleton provider={provider} />;
  }

  if (connectionsError) {
    return (
      <Panel>
        <CardHeader kicker={t(`integrations.provider.${provider}`)} />
        <EmptyState
          icon={<IconAlertTriangle size={28} stroke={1.6} />}
          title={t('common.error')}
          description={t('common.unknownError')}
          action={
            <Button variant="default" onClick={onRetryConnections}>
              {t('common.back')}
            </Button>
          }
        />
      </Panel>
    );
  }

  return (
    <Panel>
      <CardHeader
        kicker={t(`integrations.provider.${provider}`)}
        right={<Badge tone={connected ? 'positive' : 'neutral'}>{t(connected ? 'integrations.connected' : 'integrations.notConnected')}</Badge>}
      />

      {connected && (
        <Text className="ds-metadata" mb="sm">
          {t('integrations.lastSynced')}:{' '}
          {connection?.lastSyncedAtUtc ? new Date(connection.lastSyncedAtUtc).toLocaleString('cs-CZ') : t('integrations.neverSynced')}
        </Text>
      )}

      <Group gap="xs" mb={connected ? 'md' : 0}>
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

      {connected && (
        <Stack gap={0}>
          <Text className="ds-eyebrow" mb={4}>
            {t('integrations.syncHistory')}
          </Text>
          {historyLoading ? (
            <Stack gap={6}>
              <Skeleton height={18} />
              <Skeleton height={18} />
            </Stack>
          ) : historyError ? (
            <Text className="ds-body">{t('common.unknownError')}</Text>
          ) : (history?.length ?? 0) > 0 ? (
            history!.slice(0, 5).map((run) => (
              <Group key={run.id} justify="space-between" gap="xs" className="ds-list-row">
                <Text className="ds-metadata">{run.startedAtUtc ? new Date(run.startedAtUtc).toLocaleString('cs-CZ') : '—'}</Text>
                <Badge tone={statusTone(run.status)}>{run.status}</Badge>
                <Text className="ds-metadata">+{run.itemsCreated ?? 0}</Text>
              </Group>
            ))
          ) : (
            <Text className="ds-body">{t('integrations.neverSynced')}</Text>
          )}
        </Stack>
      )}
    </Panel>
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
      showToast({ tone: 'positive', message: t('integrations.connected') });
      await invalidateConnections();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleDisconnect = async (provider: IntegrationProviderType) => {
    try {
      await disconnectMutation.mutateAsync({ provider });
      await invalidateConnections();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleSync = async (provider: IntegrationProviderType) => {
    try {
      await syncMutation.mutateAsync({ provider });
      showToast({ tone: 'positive', message: t('integrations.syncStarted') });
      await invalidateConnections();
      await queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsProviderSyncHistoryQueryKey(provider) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('nav.integrations')}
      </Title>
      <SimpleGrid cols={{ base: 1, sm: 3 }}>
        <ProviderCard
          provider={IntegrationProviderType.Strava}
          isDemo={false}
          connection={findConnection(IntegrationProviderType.Strava)}
          connectionsLoading={connectionsQuery.isLoading}
          connectionsError={connectionsQuery.isError}
          onRetryConnections={() => void connectionsQuery.refetch()}
          history={stravaHistory.data}
          historyLoading={stravaConnected && stravaHistory.isLoading}
          historyError={stravaConnected && stravaHistory.isError}
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
          connectionsLoading={connectionsQuery.isLoading}
          connectionsError={connectionsQuery.isError}
          onRetryConnections={() => void connectionsQuery.refetch()}
          history={garminHistory.data}
          historyLoading={garminConnected && garminHistory.isLoading}
          historyError={garminConnected && garminHistory.isError}
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
          connectionsLoading={connectionsQuery.isLoading}
          connectionsError={connectionsQuery.isError}
          onRetryConnections={() => void connectionsQuery.refetch()}
          history={mySasyHistory.data}
          historyLoading={mySasyConnected && mySasyHistory.isLoading}
          historyError={mySasyConnected && mySasyHistory.isError}
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
