import { useCallback, useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  useGetApiIntegrationsSyncStatus,
  getGetApiIntegrationsSyncStatusQueryKey,
  getPostApiIntegrationsSyncAllMutationOptions,
} from '../../api/generated/integration-connections/integration-connections';
import { SyncRunStatus, type ProviderSyncStatusDto, type SynchronizationRunDto } from '../../api/generated/models';

const POLL_INTERVAL_MS = 1500;

/** Mirrors the backend's ActiveRunTimeout — a queued/running run older than this was lost (e.g. a
 * server restart drained the in-process queue), so it must not keep the spinner and polling alive. */
const ACTIVE_RUN_TIMEOUT_MS = 10 * 60 * 1000;

const AUTO_SYNC_FLAG_KEY = 'peakform.autoSyncDoneFor';

const STATUS_QUERY_KEY = getGetApiIntegrationsSyncStatusQueryKey();

/** Forget that this tab already ran the post-login sync — called on login/logout so the next
 * sign-in syncs again even within the same browser tab. */
export function clearAutoSyncFlag() {
  try {
    sessionStorage.removeItem(AUTO_SYNC_FLAG_KEY);
  } catch {
    // Storage unavailable (private mode) — the backend throttle still prevents over-syncing.
  }
}

function claimAutoSync(userId: string): boolean {
  try {
    if (sessionStorage.getItem(AUTO_SYNC_FLAG_KEY) === userId) return false;
    sessionStorage.setItem(AUTO_SYNC_FLAG_KEY, userId);
  } catch {
    // Fall through — without storage we still sync, the backend skips recently synced providers.
  }
  return true;
}

export function isRunActive(run?: SynchronizationRunDto): boolean {
  if (!run || (run.status !== SyncRunStatus.Pending && run.status !== SyncRunStatus.Running)) return false;
  return !run.startedAtUtc || Date.now() - new Date(run.startedAtUtc).getTime() < ACTIVE_RUN_TIMEOUT_MS;
}

const anyActive = (statuses?: ProviderSyncStatusDto[]) => (statuses ?? []).some((s) => isRunActive(s.latestRun));

/**
 * App-wide sync state behind the header indicator: polls the per-provider status only while a run
 * is queued or running, fires the post-login "sync everything" once per tab session, and refreshes
 * every other cached query once a sync wave finishes so pages show the newly imported data.
 */
export function useSyncCenter(userId: string | undefined, enabled: boolean) {
  const queryClient = useQueryClient();

  const statusQuery = useGetApiIntegrationsSyncStatus({
    query: {
      enabled,
      refetchInterval: (query) => (anyActive(query.state.data) ? POLL_INTERVAL_MS : false),
    },
  });
  const syncAllMutation = useMutation(getPostApiIntegrationsSyncAllMutationOptions());
  const { mutateAsync } = syncAllMutation;

  const statuses = statusQuery.data ?? [];
  const hasActiveRun = anyActive(statuses);

  const syncAll = useCallback(
    async (automatic = false) => {
      const result = await mutateAsync({ params: { automatic } });
      queryClient.setQueryData(STATUS_QUERY_KEY, result);
    },
    [mutateAsync, queryClient],
  );

  const wasActive = useRef(false);
  useEffect(() => {
    if (wasActive.current && !hasActiveRun) {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] !== STATUS_QUERY_KEY[0],
      });
    }
    wasActive.current = hasActiveRun;
  }, [hasActiveRun, queryClient]);

  useEffect(() => {
    if (!enabled || !userId || !claimAutoSync(userId)) return;
    // Silent on failure — the indicator's status list already shows per-provider errors, and a
    // toast right after login for a background nicety would be noise.
    syncAll(true).catch(() => undefined);
  }, [enabled, userId, syncAll]);

  const failedCount = statuses.filter((s) => s.latestRun?.status === SyncRunStatus.Failed).length;
  const doneCount = statuses.filter((s) => !isRunActive(s.latestRun)).length;

  return {
    statuses,
    isLoading: statusQuery.isLoading,
    isError: statusQuery.isError,
    isSyncing: syncAllMutation.isPending || hasActiveRun,
    isStarting: syncAllMutation.isPending,
    failedCount,
    doneCount,
    syncAll,
  };
}
