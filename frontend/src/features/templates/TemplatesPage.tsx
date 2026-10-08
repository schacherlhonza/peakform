import { useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Select, Stack, Text, Textarea, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconClipboardList, IconPlus } from '@tabler/icons-react';
import { Panel, Button, Modal, FormField, Skeleton, EmptyState, showToast } from '../../design-system/components';
import { SegmentEditor } from '../../workouts/segments/SegmentEditor';
import { TemplatesTable } from './TemplatesTable';
import { normalizeSegments } from '../../workouts/segments/segmentFormat';
import {
  useGetApiWorkoutTemplates,
  getGetApiWorkoutTemplatesQueryKey,
  getPostApiWorkoutTemplatesMutationOptions,
  getPutApiWorkoutTemplatesIdMutationOptions,
  getDeleteApiWorkoutTemplatesIdMutationOptions,
} from '../../api/generated/workout-templates/workout-templates';
import { SportType } from '../../api/generated/models';
import type { WorkoutSegmentDto, WorkoutTemplateDto } from '../../api/generated/models';

const schema = z.object({
  name: z.string().min(1),
  sport: z.nativeEnum(SportType),
  description: z.string().optional(),
});
type FormValues = z.infer<typeof schema>;

function TemplatesSkeleton() {
  return (
    <Stack gap="md">
      {[0, 1, 2].map((i) => (
        <Skeleton key={i} height={72} radius="var(--radius-panel)" />
      ))}
    </Stack>
  );
}

export function TemplatesPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure();
  const [editingTemplate, setEditingTemplate] = useState<WorkoutTemplateDto | null>(null);
  const [pendingDelete, setPendingDelete] = useState<WorkoutTemplateDto | null>(null);
  const [segments, setSegments] = useState<WorkoutSegmentDto[]>([]);

  const templatesQuery = useGetApiWorkoutTemplates();
  const templates = templatesQuery.data ?? [];

  const createMutation = useMutation(getPostApiWorkoutTemplatesMutationOptions());
  const updateMutation = useMutation(getPutApiWorkoutTemplatesIdMutationOptions());
  const deleteMutation = useMutation(getDeleteApiWorkoutTemplatesIdMutationOptions());
  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiWorkoutTemplatesQueryKey() });

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', sport: SportType.Running, description: '' },
  });

  const openCreate = () => {
    setEditingTemplate(null);
    form.reset({ name: '', sport: SportType.Running, description: '' });
    setSegments([]);
    openModal();
  };

  const openEdit = (tpl: WorkoutTemplateDto) => {
    setEditingTemplate(tpl);
    form.reset({ name: tpl.name ?? '', sport: tpl.sport ?? SportType.Running, description: tpl.description ?? '' });
    setSegments(tpl.segments ?? []);
    openModal();
  };

  const openDuplicate = (tpl: WorkoutTemplateDto) => {
    setEditingTemplate(null);
    form.reset({ name: t('templates.copyName', { name: tpl.name ?? '' }), sport: tpl.sport ?? SportType.Running, description: tpl.description ?? '' });
    setSegments((tpl.segments ?? []).map((s) => ({ ...s, id: null })));
    openModal();
  };

  const onSubmit = form.handleSubmit(async (values) => {
    const data = { name: values.name, sport: values.sport, description: values.description ?? '', segments: normalizeSegments(segments) };
    try {
      if (editingTemplate?.id) {
        await updateMutation.mutateAsync({ id: editingTemplate.id, data });
      } else {
        await createMutation.mutateAsync({ data });
      }
      showToast({ tone: 'positive', message: editingTemplate ? t('templates.saved') : t('templates.created') });
      closeModal();
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const confirmDelete = async () => {
    if (!pendingDelete?.id) return;
    try {
      await deleteMutation.mutateAsync({ id: pendingDelete.id });
      showToast({ tone: 'positive', message: t('templates.deleted') });
      setPendingDelete(null);
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title className="ds-page-title" order={2}>
            {t('templates.title')}
          </Title>
          <Text className="ds-body">{t('templates.subtitle')}</Text>
        </div>
        <Button leftSection={<IconPlus size={16} />} onClick={openCreate}>
          {t('templates.createTemplate')}
        </Button>
      </Group>

      <Panel>
        {templatesQuery.isLoading ? (
          <TemplatesSkeleton />
        ) : templatesQuery.isError ? (
          <EmptyState
            icon={<IconClipboardList size={28} stroke={1.6} />}
            title={t('common.error')}
            description={t('common.unknownError')}
            action={
              <Button variant="default" onClick={() => templatesQuery.refetch()}>
                {t('common.back')}
              </Button>
            }
          />
        ) : templates.length === 0 ? (
          <EmptyState icon={<IconClipboardList size={28} stroke={1.6} />} title={t('templates.empty')} description={t('templates.emptyAction')} />
        ) : (
          <TemplatesTable templates={templates} onEdit={openEdit} onDuplicate={openDuplicate} onDelete={setPendingDelete} />
        )}
      </Panel>

      <Modal opened={modalOpened} onClose={closeModal} size="xl" title={editingTemplate ? t('templates.editTemplate') : t('templates.createTemplate')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <Group grow align="flex-start">
              <FormField label={t('templates.name')} error={form.formState.errors.name?.message}>
                <TextInput {...form.register('name')} />
              </FormField>
              <Controller
                control={form.control}
                name="sport"
                render={({ field }) => (
                  <FormField label={t('templates.sport')}>
                    <Select data={Object.values(SportType).map((v) => ({ value: v, label: t(`sport.${v}`) }))} {...field} />
                  </FormField>
                )}
              />
            </Group>
            <FormField label={t('templates.description')}>
              <Textarea autosize minRows={2} maxRows={6} {...form.register('description')} />
            </FormField>
            <Text className="ds-eyebrow" mt="xs">
              {t('workout.structure')}
            </Text>
            <SegmentEditor value={segments} onChange={setSegments} />
            <Button type="submit" loading={form.formState.isSubmitting || createMutation.isPending || updateMutation.isPending} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal opened={pendingDelete !== null} onClose={() => setPendingDelete(null)} title={t('templates.deleteConfirmTitle')}>
        <Stack gap="md">
          <Text className="ds-body">{t('templates.deleteConfirmBody')}</Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPendingDelete(null)}>
              {t('common.cancel')}
            </Button>
            <Button color="red" loading={deleteMutation.isPending} onClick={() => void confirmDelete()}>
              {t('common.delete')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}

export default TemplatesPage;
