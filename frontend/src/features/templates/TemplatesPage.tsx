import { useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Select, Stack, Text, Textarea, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconClipboardList, IconPlus, IconTrash } from '@tabler/icons-react';
import { Panel, CardHeader, Button, IconButton, Modal, FormField, Skeleton, EmptyState, showToast } from '../../design-system/components';
import {
  useGetApiWorkoutTemplates,
  getGetApiWorkoutTemplatesQueryKey,
  getPostApiWorkoutTemplatesMutationOptions,
  getDeleteApiWorkoutTemplatesIdMutationOptions,
} from '../../api/generated/workout-templates/workout-templates';
import { SportType } from '../../api/generated/models';
import type { WorkoutTemplateDto } from '../../api/generated/models';

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
  const [createOpened, { open: openCreate, close: closeCreate }] = useDisclosure();
  const [pendingDelete, setPendingDelete] = useState<WorkoutTemplateDto | null>(null);

  const templatesQuery = useGetApiWorkoutTemplates();
  const templates = templatesQuery.data ?? [];

  const createMutation = useMutation(getPostApiWorkoutTemplatesMutationOptions());
  const deleteMutation = useMutation(getDeleteApiWorkoutTemplatesIdMutationOptions());
  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiWorkoutTemplatesQueryKey() });

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', sport: SportType.Running, description: '' },
  });

  const onCreate = form.handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({ data: { name: values.name, sport: values.sport, description: values.description, segments: [] } });
      showToast({ tone: 'positive', message: t('templates.created') });
      form.reset({ name: '', sport: SportType.Running, description: '' });
      closeCreate();
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
          <Stack gap="sm">
            {templates.map((tpl) => (
              <div key={tpl.id} className="ds-list-row">
                <CardHeader
                  kicker={tpl.sport ? t(`sport.${tpl.sport}`) : ''}
                  title={tpl.name}
                  right={
                    <IconButton
                      icon={<IconTrash size={16} />}
                      label={t('common.delete')}
                      color="red"
                      onClick={() => setPendingDelete(tpl)}
                    />
                  }
                />
                {tpl.description && <Text className="ds-body">{tpl.description}</Text>}
              </div>
            ))}
          </Stack>
        )}
      </Panel>

      <Modal opened={createOpened} onClose={closeCreate} title={t('templates.createTemplate')}>
        <form onSubmit={onCreate}>
          <Stack gap="sm">
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
            <FormField label={t('templates.description')}>
              <Textarea minRows={2} {...form.register('description')} />
            </FormField>
            <Button type="submit" loading={form.formState.isSubmitting || createMutation.isPending} fullWidth mt="sm">
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
