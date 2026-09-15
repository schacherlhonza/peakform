import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Alert, Badge, Button, Card, FileInput, Group, Select, Stack, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconCheck, IconFileImport, IconUpload } from '@tabler/icons-react';
import {
  getPostApiImportPreviewMutationOptions,
  getPostApiImportImportedFileIdConfirmMutationOptions,
} from '../api/generated/import/import';
import { ImportFileType, ImportRowStatus } from '../api/generated/models';

const fileTypeOptions = Object.values(ImportFileType).map((value) => ({ value, label: value }));

function rowColor(status?: string): string | undefined {
  if (status === ImportRowStatus.Valid) return 'var(--mantine-color-green-0)';
  if (status === ImportRowStatus.Warning) return 'var(--mantine-color-yellow-0)';
  if (status === ImportRowStatus.Error) return 'var(--mantine-color-red-0)';
  return undefined;
}

function badgeColor(status?: string): string {
  if (status === ImportRowStatus.Valid) return 'green';
  if (status === ImportRowStatus.Warning) return 'yellow';
  if (status === ImportRowStatus.Error) return 'red';
  return 'gray';
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
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  const handleConfirm = async () => {
    if (!preview?.importedFileId) return;
    try {
      await confirmMutation.mutateAsync({ importedFileId: preview.importedFileId });
      notifications.show({ color: 'green', message: t('import.confirmed') });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nav.import')}</Title>

      <Card withBorder radius="md" p="lg">
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
      </Card>

      {preview && (
        <Card withBorder radius="md" p="lg">
          <Stack gap="md">
            <Text size="sm" c="dimmed">
              {t('import.reviewNotice')}
            </Text>

            <Group gap="xs">
              <Badge variant="light">{t('import.rowsTotal', { count: preview.rowsTotal ?? 0 })}</Badge>
              <Badge color="green" variant="light">
                {t('import.rowsValid', { count: preview.rowsValid ?? 0 })}
              </Badge>
              <Badge color="yellow" variant="light">
                {t('import.rowsWithWarnings', { count: preview.rowsWithWarnings ?? 0 })}
              </Badge>
              <Badge color="red" variant="light">
                {t('import.rowsWithErrors', { count: preview.rowsWithErrors ?? 0 })}
              </Badge>
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
                  <Table.Tr key={row.rowNumber} bg={rowColor(row.status)}>
                    <Table.Td>{row.rowNumber}</Table.Td>
                    <Table.Td>{row.date ?? '—'}</Table.Td>
                    <Table.Td>{row.sport ? t(`sport.${row.sport}`) : '—'}</Table.Td>
                    <Table.Td>{row.distanceKm != null ? `${row.distanceKm} km` : '—'}</Table.Td>
                    <Table.Td>{row.durationMinutes != null ? `${row.durationMinutes} min` : '—'}</Table.Td>
                    <Table.Td>
                      <Badge size="sm" color={badgeColor(row.status)} variant="light">
                        {t(`import.rowStatus.${row.status}`)}
                      </Badge>
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
              <Alert color="green" icon={<IconCheck size={18} />} title={t('import.confirmed')}>
                {t('import.confirmSummary', {
                  imported: confirmResult.rowsImported ?? 0,
                  skippedDuplicate: confirmResult.rowsSkippedDuplicate ?? 0,
                  skippedInvalid: confirmResult.rowsSkippedInvalid ?? 0,
                })}
              </Alert>
            )}
          </Stack>
        </Card>
      )}
    </Stack>
  );
}
