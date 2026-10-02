import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { isAxiosError } from 'axios';
import { Alert, Anchor, FileInput, Group, List, MultiSelect, Progress, SimpleGrid, Stack, Text, TextInput } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconArchive, IconLink, IconUpload } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, Button, Modal, SegmentedControl, Skeleton, showToast } from '../design-system/components';
import type { BadgeTone } from '../design-system/components';
import { axiosInstance } from '../api/mutator';
import {
  useGetApiIntegrationsStravaArchiveImports,
  getGetApiIntegrationsStravaArchiveImportsQueryKey,
  getPostApiIntegrationsStravaArchiveImportsLinkMutationOptions,
  getPostApiIntegrationsStravaArchiveImportsImportIdConfirmMutationOptions,
  getPostApiIntegrationsStravaArchiveImportsImportIdCancelMutationOptions,
} from '../api/generated/strava-archive-imports/strava-archive-imports';
import { SportType, StravaArchiveImportStatus, type StravaArchiveImportDto } from '../api/generated/models';

const RUNNING_STATUSES: StravaArchiveImportStatus[] = [
  StravaArchiveImportStatus.Pending,
  StravaArchiveImportStatus.Downloading,
  StravaArchiveImportStatus.Analyzing,
  StravaArchiveImportStatus.Importing,
];

// Mirrors StravaArchiveLink on the backend — only a quick client-side hint, the server decides.
const LINK_PATTERN = /^https:\/\/(email\.strava\.com\/ls\/click|s3([.-][a-z0-9-]+)?\.amazonaws\.com\/strava\.portability\/)/i;

const IMPORTABLE_SPORTS: SportType[] = [SportType.Running, SportType.Cycling, SportType.Swimming, SportType.Strength, SportType.Other];

function statusTone(status?: StravaArchiveImportStatus): BadgeTone {
  if (status === StravaArchiveImportStatus.Succeeded || status === StravaArchiveImportStatus.PreviewReady) return 'positive';
  if (status === StravaArchiveImportStatus.Failed) return 'danger';
  return 'neutral';
}

function formatBytes(bytes: number): string {
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(1)} GB`;
  return `${Math.round(bytes / 1024 ** 2)} MB`;
}

/** ProblemDetails.detail carries the user-facing Czech message for 4xx (see GlobalExceptionHandler). */
function errorMessage(error: unknown, fallback: string): string {
  if (isAxiosError(error)) {
    const data = error.response?.data as { detail?: string } | string | undefined;
    if (typeof data === 'string' && data) return data;
    if (data && typeof data === 'object' && data.detail) return data.detail;
  }
  return fallback;
}

function Stat({ label, value }: { label: string; value?: number | null }) {
  return (
    <Stack gap={0}>
      <Text className="ds-eyebrow">{label}</Text>
      <Text fw={600}>{value ?? 0}</Text>
    </Stack>
  );
}

function StartImportModal({ opened, onClose, onStarted }: { opened: boolean; onClose: () => void; onStarted: () => void }) {
  const { t } = useTranslation();
  const [source, setSource] = useState<'link' | 'upload'>('link');
  const [url, setUrl] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [fromDate, setFromDate] = useState<string | null>(null);
  const [toDate, setToDate] = useState<string | null>(null);
  const [sports, setSports] = useState<string[]>([]);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);

  const linkMutation = useMutation(getPostApiIntegrationsStravaArchiveImportsLinkMutationOptions());
  const uploadMutation = useMutation({
    mutationFn: async (archive: File) => {
      // Fields before the file: the server streams the upload and starts the import at the file part.
      const form = new FormData();
      if (fromDate) form.append('fromDate', fromDate);
      if (toDate) form.append('toDate', toDate);
      sports.forEach((s) => form.append('sports', s));
      form.append('file', archive);
      const response = await axiosInstance.post<StravaArchiveImportDto>('/api/integrations/strava/archive-imports/upload', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (e) => setUploadProgress(e.total ? Math.round((e.loaded / e.total) * 100) : null),
      });
      return response.data;
    },
    onSettled: () => setUploadProgress(null),
  });

  const linkInvalid = url.trim() !== '' && !LINK_PATTERN.test(url.trim());
  const canSubmit = source === 'link' ? url.trim() !== '' && !linkInvalid : file !== null;
  const pending = linkMutation.isPending || uploadMutation.isPending;

  const handleSubmit = async () => {
    try {
      if (source === 'link') {
        await linkMutation.mutateAsync({
          data: { url: url.trim(), fromDate, toDate, sports: sports.length ? (sports as SportType[]) : null },
        });
      } else if (file) {
        await uploadMutation.mutateAsync(file);
      }
      showToast({ tone: 'positive', message: t('integrations.stravaArchive.started') });
      setUrl('');
      setFile(null);
      onStarted();
    } catch (error) {
      showToast({ tone: 'danger', title: t('common.error'), message: errorMessage(error, t('common.unknownError')) });
    }
  };

  return (
    <Modal opened={opened} onClose={onClose} title={t('integrations.stravaArchive.modalTitle')} size="lg">
      <Stack gap="md">
        <List type="ordered" spacing="xs" size="sm">
          <List.Item>
            {t('integrations.stravaArchive.step1')}{' '}
            <Anchor href="https://www.strava.com/athlete/delete_your_account" target="_blank" rel="noopener noreferrer">
              {t('integrations.stravaArchive.step1Link')}
            </Anchor>
          </List.Item>
          <List.Item>{t('integrations.stravaArchive.step2')}</List.Item>
        </List>

        <SegmentedControl
          value={source}
          onChange={(v) => setSource(v as 'link' | 'upload')}
          data={[
            { value: 'link', label: t('integrations.stravaArchive.sourceLink') },
            { value: 'upload', label: t('integrations.stravaArchive.sourceUpload') },
          ]}
        />

        {source === 'link' ? (
          <TextInput
            label={t('integrations.stravaArchive.linkLabel')}
            placeholder={t('integrations.stravaArchive.linkPlaceholder')}
            leftSection={<IconLink size={16} />}
            value={url}
            onChange={(e) => setUrl(e.currentTarget.value)}
            error={linkInvalid ? t('integrations.stravaArchive.linkInvalid') : undefined}
            autoComplete="off"
          />
        ) : (
          <FileInput
            label={t('integrations.stravaArchive.fileLabel')}
            placeholder={t('integrations.stravaArchive.filePlaceholder')}
            accept=".zip,application/zip"
            leftSection={<IconUpload size={16} />}
            value={file}
            onChange={setFile}
          />
        )}

        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <DateInput label={t('integrations.stravaArchive.fromDate')} value={fromDate} onChange={setFromDate} clearable maxDate={toDate ?? undefined} />
          <DateInput label={t('integrations.stravaArchive.toDate')} value={toDate} onChange={setToDate} clearable minDate={fromDate ?? undefined} />
        </SimpleGrid>

        <MultiSelect
          label={t('integrations.stravaArchive.sports')}
          placeholder={sports.length ? undefined : t('integrations.stravaArchive.sportsPlaceholder')}
          data={IMPORTABLE_SPORTS.map((s) => ({ value: s, label: t(`sport.${s}`) }))}
          value={sports}
          onChange={setSports}
          clearable
        />

        <Text className="ds-metadata">{t('integrations.stravaArchive.linkSecretNote')}</Text>

        {uploadProgress !== null && <Progress value={uploadProgress} animated />}

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button onClick={() => void handleSubmit()} disabled={!canSubmit} loading={pending}>
            {t('integrations.stravaArchive.submit')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function ImportStatusView({ item, onNewImport }: { item: StravaArchiveImportDto; onNewImport: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const confirmMutation = useMutation(getPostApiIntegrationsStravaArchiveImportsImportIdConfirmMutationOptions());
  const cancelMutation = useMutation(getPostApiIntegrationsStravaArchiveImportsImportIdCancelMutationOptions());

  const status = item.status!;
  const total = item.previewTotal ?? 0;
  const running = RUNNING_STATUSES.includes(status);
  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsStravaArchiveImportsQueryKey() });

  const handle = async (action: 'confirm' | 'cancel') => {
    try {
      if (action === 'confirm') {
        await confirmMutation.mutateAsync({ importId: item.id! });
        showToast({ tone: 'positive', message: t('integrations.stravaArchive.confirmed') });
      } else {
        await cancelMutation.mutateAsync({ importId: item.id! });
      }
      await invalidate();
    } catch (error) {
      showToast({ tone: 'danger', title: t('common.error'), message: errorMessage(error, t('common.unknownError')) });
    }
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Text className="ds-metadata">
          {item.originalFileName ?? t('integrations.stravaArchive.sourceLink')} · {new Date(item.createdAtUtc!).toLocaleString('cs-CZ')}
        </Text>
        <Badge tone={statusTone(status)}>{t(`integrations.stravaArchive.status.${status}`)}</Badge>
      </Group>

      {status === StravaArchiveImportStatus.Downloading && (
        <Stack gap={4}>
          <Progress value={item.sizeBytes ? ((item.downloadedBytes ?? 0) / item.sizeBytes) * 100 : 100} animated striped={!item.sizeBytes} />
          <Text className="ds-metadata">
            {item.sizeBytes
              ? t('integrations.stravaArchive.downloaded', { downloaded: formatBytes(item.downloadedBytes ?? 0), total: formatBytes(item.sizeBytes) })
              : t('integrations.stravaArchive.downloadedUnknown', { downloaded: formatBytes(item.downloadedBytes ?? 0) })}
          </Text>
        </Stack>
      )}

      {(status === StravaArchiveImportStatus.Analyzing || status === StravaArchiveImportStatus.Importing || status === StravaArchiveImportStatus.Pending) && (
        <Stack gap={4}>
          <Progress value={total ? ((item.itemsProcessed ?? 0) / total) * 100 : 0} animated />
          <Text className="ds-metadata">
            {t('integrations.stravaArchive.progress', { processed: item.itemsProcessed ?? 0, total: total || '…' })}
          </Text>
        </Stack>
      )}

      {status === StravaArchiveImportStatus.PreviewReady && (
        <>
          <Text className="ds-eyebrow">{t('integrations.stravaArchive.previewTitle')}</Text>
          <SimpleGrid cols={{ base: 2, sm: 4 }}>
            <Stat label={t('integrations.stravaArchive.previewTotal')} value={item.previewTotal} />
            <Stat label={t('integrations.stravaArchive.previewInFilter')} value={item.previewInFilter} />
            <Stat label={t('integrations.stravaArchive.previewCreate')} value={item.previewWouldCreate} />
            <Stat label={t('integrations.stravaArchive.previewMerge')} value={item.previewWouldMerge} />
            <Stat label={t('integrations.stravaArchive.previewReview')} value={item.previewWouldReview} />
            <Stat label={t('integrations.stravaArchive.previewAlready')} value={item.previewAlreadyImported} />
            <Stat label={t('integrations.stravaArchive.previewStreams')} value={item.previewStreamsToAdd} />
            {(item.itemsFailed ?? 0) > 0 && <Stat label={t('integrations.stravaArchive.previewFailed')} value={item.itemsFailed} />}
          </SimpleGrid>
          <Group justify="flex-end">
            <Button variant="subtle" color="red" onClick={() => void handle('cancel')} loading={cancelMutation.isPending}>
              {t('integrations.stravaArchive.cancel')}
            </Button>
            <Button
              onClick={() => void handle('confirm')}
              loading={confirmMutation.isPending}
              disabled={(item.previewWouldCreate ?? 0) + (item.previewWouldMerge ?? 0) + (item.previewWouldReview ?? 0) + (item.previewStreamsToAdd ?? 0) === 0}
            >
              {(item.previewWouldCreate ?? 0) + (item.previewWouldMerge ?? 0) + (item.previewWouldReview ?? 0) > 0
                ? t('integrations.stravaArchive.confirm', {
                    count: (item.previewWouldCreate ?? 0) + (item.previewWouldMerge ?? 0) + (item.previewWouldReview ?? 0),
                  })
                : t('integrations.stravaArchive.confirmStreamsOnly', { count: item.previewStreamsToAdd ?? 0 })}
            </Button>
          </Group>
        </>
      )}

      {status === StravaArchiveImportStatus.Succeeded && (
        <>
          <SimpleGrid cols={{ base: 2, sm: 6 }}>
            <Stat label={t('integrations.stravaArchive.resultCreated')} value={item.itemsCreated} />
            <Stat label={t('integrations.stravaArchive.resultMerged')} value={item.itemsMerged} />
            <Stat label={t('integrations.stravaArchive.resultReview')} value={item.itemsFlaggedForReview} />
            <Stat label={t('integrations.stravaArchive.resultSkipped')} value={item.itemsSkippedDuplicate} />
            <Stat label={t('integrations.stravaArchive.resultFailed')} value={item.itemsFailed} />
            <Stat label={t('integrations.stravaArchive.resultStreams')} value={item.itemsStreamsAdded} />
          </SimpleGrid>
          {(item.itemsFlaggedForReview ?? 0) > 0 && (
            <Group>
              <Button component={Link} to="/integrations/duplicates" size="xs" variant="light">
                {t('integrations.stravaArchive.reviewLink')}
              </Button>
            </Group>
          )}
        </>
      )}

      {status === StravaArchiveImportStatus.Failed && item.errorMessage && (
        <Alert color="red" variant="light">
          {item.errorMessage}
        </Alert>
      )}

      {running && status !== StravaArchiveImportStatus.Importing && (
        <Group justify="flex-end">
          <Button variant="subtle" color="red" size="xs" onClick={() => void handle('cancel')} loading={cancelMutation.isPending}>
            {t('integrations.stravaArchive.cancel')}
          </Button>
        </Group>
      )}

      {!running && status !== StravaArchiveImportStatus.PreviewReady && (
        <Group justify="flex-end">
          <Button variant="light" size="xs" onClick={onNewImport}>
            {t('integrations.stravaArchive.newImport')}
          </Button>
        </Group>
      )}
    </Stack>
  );
}

/**
 * Bulk import of the athlete's Strava "Download your data" archive (docs/integrations/strava-archive-import.md):
 * start from the emailed link or an uploaded ZIP → background download + dry-run preview → confirm.
 * Polls only while a download/analysis/import is actually running.
 */
export default function StravaArchiveImportPanel() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [dismissedId, setDismissedId] = useState<string | null>(null);

  const importsQuery = useGetApiIntegrationsStravaArchiveImports({
    query: {
      refetchInterval: (query) => {
        const latestStatus = query.state.data?.[0]?.status;
        return latestStatus && RUNNING_STATUSES.includes(latestStatus) ? 2000 : false;
      },
    },
  });

  const latest = importsQuery.data?.[0];
  const showLatest = latest && latest.id !== dismissedId;

  return (
    <Panel>
      <CardHeader
        kicker={t('integrations.stravaArchive.kicker')}
        title={t('integrations.stravaArchive.title')}
        right={<IconArchive size={20} stroke={1.6} />}
      />

      {importsQuery.isLoading ? (
        <Skeleton height={60} />
      ) : showLatest ? (
        <ImportStatusView item={latest} onNewImport={() => setDismissedId(latest.id ?? null)} />
      ) : (
        <Stack gap="sm" align="flex-start">
          <Text className="ds-body">{t('integrations.stravaArchive.intro')}</Text>
          <Button onClick={() => setModalOpen(true)} leftSection={<IconArchive size={16} />}>
            {t('integrations.stravaArchive.startButton')}
          </Button>
        </Stack>
      )}

      <StartImportModal
        opened={modalOpen}
        onClose={() => setModalOpen(false)}
        onStarted={() => {
          setModalOpen(false);
          setDismissedId(null);
          void queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsStravaArchiveImportsQueryKey() });
        }}
      />
    </Panel>
  );
}
