import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { isAxiosError } from 'axios';
import { Group, Stack, Text } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconHistory } from '@tabler/icons-react';
import { Button, Modal, showToast } from '../design-system/components';
import {
  getPostApiIntegrationsProviderHistoryMutationOptions,
  getGetApiIntegrationsProviderSyncHistoryQueryKey,
  getGetApiIntegrationsSyncStatusQueryKey,
} from '../api/generated/integration-connections/integration-connections';
import type { IntegrationProviderType } from '../api/generated/models';

function isoYearsAgo(years: number): string {
  const d = new Date();
  d.setFullYear(d.getFullYear() - years);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * One-off pull of older history from intervals.icu (activities + wellness, then the device files
 * for charts and maps in the background) — the regular sync only goes 30 days back on first connect.
 */
export function HistoryBackfillButton({ provider }: { provider: IntegrationProviderType }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [opened, setOpened] = useState(false);
  const [fromDate, setFromDate] = useState<string | null>(isoYearsAgo(1));
  const mutation = useMutation(getPostApiIntegrationsProviderHistoryMutationOptions());

  const handleSubmit = async () => {
    if (!fromDate) return;
    try {
      await mutation.mutateAsync({ provider, data: { fromDate } });
      showToast({ tone: 'positive', message: t('integrations.history.started') });
      setOpened(false);
      await queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsProviderSyncHistoryQueryKey(provider) });
      await queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsSyncStatusQueryKey() });
    } catch (error) {
      const detail = isAxiosError(error) ? (error.response?.data as { detail?: string } | undefined)?.detail : undefined;
      showToast({ tone: 'danger', title: t('common.error'), message: detail ?? t('common.unknownError') });
    }
  };

  return (
    <>
      <Button size="xs" variant="subtle" leftSection={<IconHistory size={14} />} onClick={() => setOpened(true)}>
        {t('integrations.history.button')}
      </Button>
      <Modal opened={opened} onClose={() => setOpened(false)} title={t('integrations.history.title')}>
        <Stack gap="md">
          <Text className="ds-body">{t('integrations.history.description')}</Text>
          <DateInput
            label={t('integrations.history.fromDate')}
            value={fromDate}
            onChange={setFromDate}
            maxDate={new Date()}
            minDate={new Date(2000, 0, 1)}
          />
          <Text className="ds-metadata">{t('integrations.history.hint')}</Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setOpened(false)}>
              {t('common.cancel')}
            </Button>
            <Button onClick={() => void handleSubmit()} loading={mutation.isPending} disabled={!fromDate}>
              {t('integrations.history.submit')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </>
  );
}
