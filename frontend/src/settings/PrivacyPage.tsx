import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Text, Title } from '@mantine/core';
import { IconDownload, IconTrash } from '@tabler/icons-react';
import { Panel, Button, Modal, showToast } from '../design-system/components';
import { getApiAccountExport, usePostApiAccountDelete } from '../api/generated/account/account';
import { useAuth } from '../auth/AuthContext';

function downloadJson(data: unknown, filename: string) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export default function PrivacyPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { logout } = useAuth();
  const [isExporting, setIsExporting] = useState(false);
  const [deleteOpened, setDeleteOpened] = useState(false);

  const deleteMutation = usePostApiAccountDelete();

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const data = await getApiAccountExport();
      downloadJson(data, `peakform-export-${new Date().toISOString().slice(0, 10)}.json`);
      showToast({ tone: 'positive', message: t('privacy.exportSuccess') });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('privacy.exportError') });
    } finally {
      setIsExporting(false);
    }
  };

  const handleDelete = async () => {
    try {
      await deleteMutation.mutateAsync();
      setDeleteOpened(false);
      showToast({ tone: 'positive', message: t('privacy.deleteSuccess') });
      logout();
      navigate('/login');
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('privacy.deleteError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('privacy.title')}
      </Title>

      <Panel>
        <Stack gap="sm">
          <Text fw={600} fz={14} c="var(--color-text)">
            {t('privacy.exportSection')}
          </Text>
          <Text className="ds-body" c="var(--color-text-muted)">
            {t('privacy.exportDescription')}
          </Text>
          <Group>
            <Button leftSection={<IconDownload size={16} />} loading={isExporting} onClick={() => void handleExport()}>
              {t('privacy.exportButton')}
            </Button>
          </Group>
        </Stack>
      </Panel>

      <Panel>
        <Stack gap="sm">
          <Text fw={600} fz={14} c="var(--color-danger)">
            {t('privacy.deleteSection')}
          </Text>
          <Text className="ds-body" c="var(--color-text-muted)">
            {t('privacy.deleteDescription')}
          </Text>
          <Group>
            <Button color="red" variant="light" leftSection={<IconTrash size={16} />} onClick={() => setDeleteOpened(true)}>
              {t('privacy.deleteButton')}
            </Button>
          </Group>
        </Stack>
      </Panel>

      <Modal opened={deleteOpened} onClose={() => setDeleteOpened(false)} title={t('privacy.deleteConfirmTitle')}>
        <Stack gap="md">
          <Text className="ds-body">{t('privacy.deleteConfirmBody')}</Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setDeleteOpened(false)}>
              {t('common.cancel')}
            </Button>
            <Button color="red" loading={deleteMutation.isPending} onClick={() => void handleDelete()}>
              {t('privacy.deleteButton')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
