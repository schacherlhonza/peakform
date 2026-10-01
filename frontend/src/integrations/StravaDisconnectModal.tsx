import { useTranslation } from 'react-i18next';
import { Group, List, Stack, Text } from '@mantine/core';
import { Button, Modal } from '../design-system/components';
import type { StravaDisconnectImpactDto } from '../api/generated/models';

/**
 * Confirmation before disconnecting Strava: the Strava API terms require deleting everything
 * obtained through the API once access ends, so the athlete sees what goes and what stays.
 */
export function StravaDisconnectModal({
  impact,
  onCancel,
  onConfirm,
  pending,
}: {
  impact: StravaDisconnectImpactDto | null;
  onCancel: () => void;
  onConfirm: () => void;
  pending: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Modal opened={impact !== null} onClose={onCancel} title={t('integrations.stravaDisconnect.title')}>
      {impact && (
        <Stack gap="md">
          <Text className="ds-body">{t('integrations.stravaDisconnect.intro')}</Text>
          <List size="sm" spacing="xs">
            <List.Item>{t('integrations.stravaDisconnect.deleted', { count: impact.activitiesDeleted ?? 0 })}</List.Item>
            <List.Item>{t('integrations.stravaDisconnect.sourcesRemoved', { count: impact.sourcesRemoved ?? 0 })}</List.Item>
            <List.Item>{t('integrations.stravaDisconnect.keptFromArchive', { count: impact.keptFromArchive ?? 0 })}</List.Item>
          </List>
          <Text className="ds-metadata">{t('integrations.stravaDisconnect.archiveNote')}</Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={onCancel} disabled={pending}>
              {t('common.cancel')}
            </Button>
            <Button color="red" onClick={onConfirm} loading={pending}>
              {t('integrations.stravaDisconnect.confirm')}
            </Button>
          </Group>
        </Stack>
      )}
    </Modal>
  );
}
