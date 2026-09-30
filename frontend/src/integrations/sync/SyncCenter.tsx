import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionIcon, Group, Indicator, Loader, Popover, Progress, Stack, Text, Tooltip } from '@mantine/core';
import {
  IconAlertTriangle,
  IconArrowRight,
  IconCircleCheck,
  IconCircleX,
  IconClock,
  IconMinus,
  IconPlugConnected,
  IconRefresh,
} from '@tabler/icons-react';
import { Badge, Button, EmptyState, Skeleton, showToast, type BadgeTone } from '../../design-system/components';
import { SyncRunStatus, type ProviderSyncStatusDto } from '../../api/generated/models';
import { isRunActive, useSyncCenter } from './useSyncCenter';
import classes from './SyncCenter.module.css';

type RowState = 'queued' | 'running' | 'done' | 'partial' | 'failed' | 'never';

function rowState(status: ProviderSyncStatusDto): RowState {
  const run = status.latestRun;
  if (!run) return 'never';
  if (isRunActive(run)) return run.status === SyncRunStatus.Pending ? 'queued' : 'running';
  if (run.status === SyncRunStatus.Succeeded) return run.errorMessage ? 'partial' : 'done';
  return 'failed';
}

const STATE_TONE: Record<RowState, BadgeTone> = {
  queued: 'neutral',
  running: 'info',
  done: 'positive',
  partial: 'warning',
  failed: 'danger',
  never: 'neutral',
};

function StateIcon({ state }: { state: RowState }) {
  switch (state) {
    case 'running':
      return <Loader size={18} color="var(--color-info)" />;
    case 'queued':
      return <IconClock size={20} stroke={1.8} color="var(--color-text-muted)" />;
    case 'done':
      return <IconCircleCheck size={20} stroke={1.8} color="var(--color-accent)" />;
    case 'partial':
      return <IconAlertTriangle size={20} stroke={1.8} color="var(--color-warning)" />;
    case 'failed':
      return <IconCircleX size={20} stroke={1.8} color="var(--color-danger)" />;
    default:
      return <IconMinus size={20} stroke={1.8} color="var(--color-text-subtle)" />;
  }
}

/** Ticks every second while `active` — drives the elapsed timer and "před X min" labels. The
 * returned `refresh` resyncs immediately (on open), so the first frame isn't a stale timestamp. */
function useNow(active: boolean) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return;
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, [active]);
  return [now, () => setNow(Date.now())] as const;
}

const relativeFormat = new Intl.RelativeTimeFormat('cs', { numeric: 'auto' });

function formatRelative(iso: string, now: number): string {
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000);
  const abs = Math.abs(seconds);
  if (abs < 45) return relativeFormat.format(0, 'second');
  if (abs < 3600) return relativeFormat.format(Math.round(seconds / 60), 'minute');
  if (abs < 86_400) return relativeFormat.format(Math.round(seconds / 3600), 'hour');
  return relativeFormat.format(Math.round(seconds / 86_400), 'day');
}

function formatElapsed(fromIso: string, now: number): string {
  const total = Math.max(0, Math.floor((now - new Date(fromIso).getTime()) / 1000));
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
}

function ProviderRow({ status, now }: { status: ProviderSyncStatusDto; now: number }) {
  const { t } = useTranslation();
  const state = rowState(status);
  const run = status.latestRun;

  let detail: string;
  if (state === 'running' && run?.startedAtUtc) {
    detail = t('sync.detail.running', {
      elapsed: formatElapsed(run.startedAtUtc, now),
    });
  } else if (state === 'queued') {
    detail = t('sync.detail.queued');
  } else if ((state === 'done' || state === 'partial') && run) {
    const parts = [
      run.itemsCreated ? t('sync.detail.created', { count: run.itemsCreated }) : null,
      run.itemsUpdated ? t('sync.detail.updated', { count: run.itemsUpdated }) : null,
      run.itemsFlaggedForReview ? t('sync.detail.flagged', { count: run.itemsFlaggedForReview }) : null,
    ].filter(Boolean);
    const summary = parts.length > 0 ? parts.join(' · ') : t('sync.detail.noNewData');
    const when = run.finishedAtUtc ? ` · ${formatRelative(run.finishedAtUtc, now)}` : '';
    detail = state === 'partial' ? `${summary}${when} — ${run.errorMessage}` : `${summary}${when}`;
  } else if (state === 'failed') {
    detail = run?.errorMessage ?? t('common.unknownError');
  } else {
    detail = t('sync.detail.never');
  }

  return (
    <div className={`${classes.row} ${state === 'running' ? classes.rowRunning : ''}`}>
      <div className={classes.rowIcon}>
        <StateIcon state={state} />
      </div>
      <div className={classes.rowBody}>
        <Group justify="space-between" gap="xs" wrap="nowrap">
          <Text fz={13} fw={700} c="var(--color-text)" truncate>
            {t(`integrations.provider.${status.provider}`)}
          </Text>
          <Badge tone={STATE_TONE[state]}>{t(`sync.state.${state}`)}</Badge>
        </Group>
        <Text className="ds-metadata" lineClamp={2} title={detail}>
          {detail}
        </Text>
      </div>
    </div>
  );
}

/**
 * Header sync button + status panel. Athletes only (integrations are athlete-owned). Also owns the
 * automatic post-login sync, since it's mounted exactly once for the whole signed-in app.
 */
export function SyncCenter({ userId }: { userId: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [opened, setOpened] = useState(false);
  const { statuses, isLoading, isError, isSyncing, isStarting, failedCount, doneCount, syncAll } = useSyncCenter(userId, true);
  const [now, refreshNow] = useNow(opened);

  const total = statuses.length;
  const progress = total > 0 ? (doneCount / total) * 100 : 0;

  const handleSyncAll = async () => {
    try {
      await syncAll(false);
    } catch {
      showToast({
        tone: 'danger',
        title: t('common.error'),
        message: t('sync.startError'),
      });
    }
  };

  const summary = isSyncing
    ? t('sync.summary.running', { done: doneCount, total })
    : failedCount > 0
      ? t('sync.summary.failed', { count: failedCount })
      : t('sync.summary.idle');

  const buttonLabel = isSyncing ? t('sync.buttonRunning') : failedCount > 0 ? t('sync.buttonFailed') : t('sync.button');

  return (
    <Popover opened={opened} onChange={setOpened} position="bottom-end" width={360} shadow="md" withinPortal>
      <Popover.Target>
        <Tooltip label={buttonLabel} withArrow openDelay={300} disabled={opened}>
          <Indicator
            disabled={!isSyncing && failedCount === 0}
            processing={isSyncing}
            color={isSyncing ? 'var(--color-info)' : 'var(--color-danger)'}
            size={9}
            offset={6}
          >
            <ActionIcon
              variant="subtle"
              aria-label={buttonLabel}
              onClick={() => {
                if (!opened) refreshNow();
                setOpened(!opened);
              }}
            >
              <IconRefresh size={18} stroke={1.8} className={isSyncing ? classes.spinning : undefined} />
            </ActionIcon>
          </Indicator>
        </Tooltip>
      </Popover.Target>

      <Popover.Dropdown className={classes.dropdown}>
        <Stack gap="sm">
          <div>
            <Text className="ds-eyebrow">{t('sync.title')}</Text>
            <Text fz={14} fw={700} c="var(--color-text)">
              {summary}
            </Text>
          </div>

          {total > 0 && (
            <Progress
              value={isSyncing ? Math.max(progress, 8) : 100}
              animated={isSyncing}
              striped={isSyncing}
              size="sm"
              radius="xl"
              color={isSyncing ? 'var(--color-info)' : failedCount > 0 ? 'var(--color-danger)' : 'var(--color-accent)'}
              aria-label={summary}
            />
          )}

          {isLoading ? (
            <Stack gap={8}>
              <Skeleton height={44} />
              <Skeleton height={44} />
            </Stack>
          ) : isError ? (
            <Text className="ds-body">{t('common.unknownError')}</Text>
          ) : total === 0 ? (
            <EmptyState
              icon={<IconPlugConnected size={26} stroke={1.6} />}
              title={t('sync.emptyTitle')}
              description={t('sync.emptyDescription')}
            />
          ) : (
            <Stack gap={6}>
              {statuses.map((status) => (
                <ProviderRow key={status.provider} status={status} now={now} />
              ))}
            </Stack>
          )}

          <Group justify="space-between" gap="xs" className={classes.footer}>
            <Button
              size="xs"
              variant="subtle"
              rightSection={<IconArrowRight size={14} />}
              onClick={() => {
                setOpened(false);
                navigate('/settings/integrations');
              }}
            >
              {t('sync.manage')}
            </Button>
            {total > 0 && (
              <Button
                size="xs"
                leftSection={<IconRefresh size={14} className={isSyncing ? classes.spinning : undefined} />}
                onClick={() => void handleSyncAll()}
                disabled={isSyncing}
                loading={isStarting}
              >
                {isSyncing ? t('sync.buttonRunning') : t('sync.syncAll')}
              </Button>
            )}
          </Group>
        </Stack>
      </Popover.Dropdown>
    </Popover>
  );
}
