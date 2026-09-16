import { useTranslation } from 'react-i18next';
import { Stack, Text } from '@mantine/core';
import { IconBellRinging } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, EmptyState, Skeleton } from '../../design-system/components';
import type { NotificationDto } from '../../api/generated/models';
import { NotificationType } from '../../api/generated/models';

export interface AttentionQueueData {
  notifications: NotificationDto[];
  isLoading: boolean;
}

const toneByType: Record<string, 'danger' | 'warning' | 'info' | 'positive'> = {
  [NotificationType.HealthFlagRaised]: 'danger',
  [NotificationType.AccessRevoked]: 'danger',
  [NotificationType.CommentAdded]: 'warning',
  [NotificationType.PlanUpdated]: 'info',
  [NotificationType.ReportReady]: 'info',
  [NotificationType.PermissionChanged]: 'info',
  [NotificationType.InviteReceived]: 'info',
  [NotificationType.InviteAccepted]: 'positive',
};

/**
 * Sorted queue of issues needing coach attention — docs/DESIGN_SYSTEM.md §7. Derived from real
 * unread notifications (client-composed, no new endpoint); severe types (health flags, revoked
 * access) surface first. Per-athlete "overdue check-in" detection is deferred — it would need a
 * query per roster athlete beyond this session's scope; see MIGRATION.md.
 */
export function AttentionQueue({ data }: { data: AttentionQueueData }) {
  const { t } = useTranslation();

  const severityRank: Record<string, number> = { danger: 0, warning: 1, info: 2, positive: 3 };
  const unread = data.notifications
    .filter((n) => !n.readAtUtc)
    .sort((a, b) => severityRank[toneByType[a.type ?? ''] ?? 'info'] - severityRank[toneByType[b.type ?? ''] ?? 'info'])
    .slice(0, 6);

  return (
    <Panel>
      <CardHeader kicker={t('notifications.title')} />
      {data.isLoading ? (
        <Skeleton height={80} />
      ) : unread.length === 0 ? (
        <EmptyState icon={<IconBellRinging size={28} stroke={1.6} />} title={t('notifications.empty')} description={t('notifications.emptyAction')} />
      ) : (
        <Stack gap={0}>
          {unread.map((n) => (
            <div key={n.id} className="ds-list-row">
              <Badge tone={toneByType[n.type ?? ''] ?? 'neutral'}>{n.type ? t(`notifications.types.${n.type}`) : ''}</Badge>
              <Text fz={13} fw={600} mt={4}>
                {n.title}
              </Text>
            </div>
          ))}
        </Stack>
      )}
    </Panel>
  );
}
