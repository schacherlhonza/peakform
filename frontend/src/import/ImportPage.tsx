import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Alert, FileInput, Group, Select, Stack, Table, Text, Title } from '@mantine/core';
import { IconCheck, IconFileImport, IconUpload } from '@tabler/icons-react';
import { Panel, CardHeader, Button, Badge, Skeleton, showToast } from '../design-system/components';
import type { BadgeTone } from '../design-system/components';
import {
  getPostApiImportPreviewMutationOptions,
  getPostApiImportImportedFileIdConfirmMutationOptions,
} from '../api/generated/import/import';
import { ImportFileType, ImportRowStatus } from '../api/generated/models';

const fileTypeOptions = Object.values(ImportFileType).map((value) => ({ value, label: value }));

function statusTone(status?: string): BadgeTone {
  if (status === ImportRowStatus.Valid) return 'positive';
  if (status === ImportRowStatus.Warning) return 'warning';
  if (status === ImportRowStatus.Error) return 'danger';
  return 'neutral';
}

// Row tint mirrors Badge.tsx's `toneStyle` bg colors (same rgba hues) at a lower opacity —
// a full-width row needs a subtler fill than a small pill to stay readable against the dark
// surface. DuplicateSkipped (neutral) rows are left untinted, matching prior behavior.
function rowBackground(status?: string): string | undefined {
  switch (statusTone(status)) {
    case 'positive':
      return 'rgba(199, 243, 77, 0.08)';
    case 'warning':
      return 'rgba(255, 154, 97, 0.08)';
    case 'danger':
      return 'rgba(255, 126, 114, 0.08)';
    default:
      return undefined;
  }
}

function PreviewSkeleton() {
  return (
    <Stack gap="sm">
      <Skeleton height={20} width={240} radius="var(--radius-panel)" />
      <Skeleton height={220} radius="var(--radius-panel)" />
    </Stack>
  );
}

export default function ImportPage() {
  const { t } = useTranslation();
  const [file, setFile] = useState<File | null>(null);
  const [fileType, setFileType] = useState<ImportFileType>(ImportFileType.StandardCsvTemplate);

  const previewMutation = useMutation(getPostApiImportPreviewMutationOptions());
  const confirmMutation = useMutation(getPostApiImportImportedFileIdConfirmMutationOptions());

  const preview = previewMutation.data;
  const confirmResult = confirmMutation.data;

  const handlePreview = async () => {
    if (!file) return;
    confirmMutation.reset();
    try {
      await previewMutation.mutateAsync({ data: { file, fileType } });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleConfirm = async () => {
    if (!preview?.importedFileId) return;
    try {
      await confirmMutation.mutateAsync({ importedFileId: preview.importedFileId });
      showToast({ tone: 'positive', message: t('import.confirmed') });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('nav.import')}
      </Title>

      <Panel>
        <CardHeader kicker={t('nav.import')} title={t('import.selectFile')} />
        <Stack gap="sm">
          <Group align="end" wrap="wrap">
            <FileInput
              label={t('import.selectFile')}
              placeholder={t('import.selectFile')}
              w={280}
              accept=".csv"
              value={file}
              onChange={setFile}
              leftSection={<IconUpload size={16} />}
            />
            <Select
              label={t('import.fileType')}
              w={220}
              data={fileTypeOptions}
              value={fileType}
              onChange={(v) => v && setFileType(v as ImportFileType)}
              allowDeselect={false}
            />
            <Button
              leftSection={<IconFileImport size={16} />}
              disabled={!file}
              loading={previewMutation.isPending}
              onClick={() => void handlePreview()}
            >
              {t('import.previewButton')}
            </Button>
          </Group>
        </Stack>
      </Panel>

      {previewMutation.isPending && (
        <Panel>
          <PreviewSkeleton />
        </Panel>
      )}

      {!previewMutation.isPending && preview && (
        <Panel>
          <CardHeader kicker={t('import.status')} title={t('import.reviewNotice')} />
          <Stack gap="md">
            <Group gap="xs">
              <Badge tone="neutral">{t('import.rowsTotal', { count: preview.rowsTotal ?? 0 })}</Badge>
              <Badge tone="positive">{t('import.rowsValid', { count: preview.rowsValid ?? 0 })}</Badge>
              <Badge tone="warning">{t('import.rowsWithWarnings', { count: preview.rowsWithWarnings ?? 0 })}</Badge>
              <Badge tone="danger">{t('import.rowsWithErrors', { count: preview.rowsWithErrors ?? 0 })}</Badge>
            </Group>

            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>#</Table.Th>
                  <Table.Th>{t('common.date')}</Table.Th>
                  <Table.Th>{t('workout.detail')}</Table.Th>
                  <Table.Th>Vzdálenost</Table.Th>
                  <Table.Th>Doba</Table.Th>
                  <Table.Th>{t('import.status')}</Table.Th>
                  <Table.Th>{t('import.messages')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {(preview.rows ?? []).map((row) => (
                  <Table.Tr key={row.rowNumber} bg={rowBackground(row.status)}>
                    <Table.Td>{row.rowNumber}</Table.Td>
                    <Table.Td>{row.date ?? '—'}</Table.Td>
                    <Table.Td>{row.sport ? t(`sport.${row.sport}`) : '—'}</Table.Td>
                    <Table.Td>{row.distanceKm != null ? `${row.distanceKm} km` : '—'}</Table.Td>
                    <Table.Td>{row.durationMinutes != null ? `${row.durationMinutes} min` : '—'}</Table.Td>
                    <Table.Td>
                      <Badge tone={statusTone(row.status)}>{t(`import.rowStatus.${row.status}`)}</Badge>
                    </Table.Td>
                    <Table.Td>
                      {(row.messages ?? []).map((m, i) => (
                        <Text key={i} size="xs" c="dimmed">
                          {m}
                        </Text>
                      ))}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>

            {!confirmResult && (
              <Group justify="flex-end">
                <Button loading={confirmMutation.isPending} onClick={() => void handleConfirm()}>
                  {t('import.confirmButton')}
                </Button>
              </Group>
            )}

            {confirmResult && (
              <Alert
                icon={<IconCheck size={18} />}
                title={t('import.confirmed')}
                styles={{
                  root: { backgroundColor: 'rgba(199, 243, 77, 0.14)', borderColor: 'rgba(199, 243, 77, 0.3)' },
                  title: { color: 'var(--color-accent)' },
                  message: { color: 'var(--color-text-muted)' },
                  icon: { color: 'var(--color-accent)' },
                }}
              >
                {t('import.confirmSummary', {
                  imported: confirmResult.rowsImported ?? 0,
                  skippedDuplicate: confirmResult.rowsSkippedDuplicate ?? 0,
                  skippedInvalid: confirmResult.rowsSkippedInvalid ?? 0,
                })}
              </Alert>
            )}
          </Stack>
        </Panel>
      )}
    </Stack>
  );
}
