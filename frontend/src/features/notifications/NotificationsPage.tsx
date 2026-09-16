import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Text, Title } from '@mantine/core';
import { IconBell, IconCheck } from '@tabler/icons-react';
import { Panel, Badge, IconButton, Skeleton, EmptyState, Button } from '../../design-system/components';
import {
  useGetApiNotifications,
  getGetApiNotificationsQueryKey,
  getPostApiNotificationsIdReadMutationOptions,
} from '../../api/generated/notifications/notifications';

function NotificationsSkeleton() {
  return (
    <Stack gap="sm">
      {[0, 1, 2, 3].map((i) => (
        <Skeleton key={i} height={56} radius="var(--radius-md)" />
      ))}
    </Stack>
  );
}

export function NotificationsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const notificationsQuery = useGetApiNotifications();
  const notifications = notificationsQuery.data ?? [];
  const unreadCount = notifications.filter((n) => !n.readAtUtc).length;

  const readMutation = useMutation(getPostApiNotificationsIdReadMutationOptions());
  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiNotificationsQueryKey() });

  const markAsRead = async (id?: string) => {
    if (!id) return;
    await readMutation.mutateAsync({ id });
    await invalidate();
  };

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="center">
        <Title className="ds-page-title" order={2}>
          {t('notifications.title')}
        </Title>
        {unreadCount > 0 && <Badge tone="info">{t('notifications.unread', { count: unreadCount })}</Badge>}
      </Group>

      <Panel>
        {notificationsQuery.isLoading ? (
          <NotificationsSkeleton />
        ) : notificationsQuery.isError ? (
          <EmptyState
            icon={<IconBell size={28} stroke={1.6} />}
            title={t('common.error')}
            description={t('common.unknownError')}
            action={
              <Button variant="default" onClick={() => notificationsQuery.refetch()}>
                {t('common.back')}
              </Button>
            }
          />
        ) : notifications.length === 0 ? (
          <EmptyState icon={<IconBell size={28} stroke={1.6} />} title={t('notifications.empty')} description={t('notifications.emptyAction')} />
        ) : (
          <Stack gap={0}>
            {notifications.map((n) => (
              <div key={n.id} className="ds-list-row">
                <Group justify="space-between" align="flex-start" wrap="nowrap">
                  <div style={{ minWidth: 0 }}>
                    <Group gap={6} mb={2}>
                      {!n.readAtUtc && <span aria-hidden style={{ width: 6, height: 6, borderRadius: '50%', background: 'var(--color-accent)', display: 'inline-block' }} />}
                      <Text className="ds-eyebrow">{n.type ? t(`notifications.types.${n.type}`) : ''}</Text>
                    </Group>
                    <Text fw={600} fz={14}>
                      {n.title}
                    </Text>
                    {n.body && (
                      <Text className="ds-body" mt={2}>
                        {n.body}
                      </Text>
                    )}
                  </div>
                  {!n.readAtUtc && (
                    <IconButton
                      icon={<IconCheck size={16} />}
                      label={t('notifications.markAsRead')}
                      onClick={() => void markAsRead(n.id)}
                    />
                  )}
                </Group>
              </div>
            ))}
          </Stack>
        )}
      </Panel>
    </Stack>
  );
}

export default NotificationsPage;
