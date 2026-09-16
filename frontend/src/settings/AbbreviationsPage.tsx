import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconAbc, IconEdit, IconTrash } from '@tabler/icons-react';
import { Panel, Button, IconButton, Modal, FormField, Skeleton, EmptyState, showToast } from '../design-system/components';
import {
  useGetApiAbbreviations,
  getGetApiAbbreviationsQueryKey,
  getPostApiAbbreviationsMutationOptions,
  getPutApiAbbreviationsIdMutationOptions,
  getDeleteApiAbbreviationsIdMutationOptions,
} from '../api/generated/custom-abbreviations/custom-abbreviations';
import type { CustomAbbreviationDto } from '../api/generated/models';

const schema = z.object({
  abbreviation: z.string().min(1),
  fullText: z.string().min(1),
  description: z.string().optional(),
});
type FormValues = z.infer<typeof schema>;

function AbbreviationsSkeleton() {
  return (
    <Stack gap="sm">
      {[0, 1, 2].map((i) => (
        <Skeleton key={i} height={44} radius="var(--radius-panel)" />
      ))}
    </Stack>
  );
}

export default function AbbreviationsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editOpened, { open: openEdit, close: closeEdit }] = useDisclosure();
  const [editing, setEditing] = useState<CustomAbbreviationDto | null>(null);
  const [pendingDelete, setPendingDelete] = useState<CustomAbbreviationDto | null>(null);

  const abbreviationsQuery = useGetApiAbbreviations();
  const abbreviations = abbreviationsQuery.data ?? [];

  const createMutation = useMutation(getPostApiAbbreviationsMutationOptions());
  const updateMutation = useMutation(getPutApiAbbreviationsIdMutationOptions());
  const deleteMutation = useMutation(getDeleteApiAbbreviationsIdMutationOptions());

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetApiAbbreviationsQueryKey() });

  const createForm = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { abbreviation: '', fullText: '', description: '' },
  });

  const editForm = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: editing
      ? { abbreviation: editing.abbreviation ?? '', fullText: editing.fullText ?? '', description: editing.description ?? '' }
      : { abbreviation: '', fullText: '', description: '' },
  });

  const onCreate = createForm.handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({ data: values });
      showToast({ tone: 'positive', message: t('common.add') });
      createForm.reset({ abbreviation: '', fullText: '', description: '' });
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const onEdit = editForm.handleSubmit(async (values) => {
    if (!editing?.id) return;
    try {
      await updateMutation.mutateAsync({ id: editing.id, data: values });
      showToast({ tone: 'positive', message: t('common.save') });
      closeEdit();
      setEditing(null);
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const confirmDelete = async () => {
    if (!pendingDelete?.id) return;
    try {
      await deleteMutation.mutateAsync({ id: pendingDelete.id });
      setPendingDelete(null);
      await invalidate();
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('nav.abbreviations')}
      </Title>

      <Panel>
        <form onSubmit={onCreate}>
          <Group align="end" wrap="wrap">
            <div style={{ width: 140 }}>
              <FormField label={t('settings.abbreviation')} error={createForm.formState.errors.abbreviation?.message}>
                <TextInput {...createForm.register('abbreviation')} />
              </FormField>
            </div>
            <div style={{ width: 240 }}>
              <FormField label={t('settings.fullText')} error={createForm.formState.errors.fullText?.message}>
                <TextInput {...createForm.register('fullText')} />
              </FormField>
            </div>
            <div style={{ width: 280 }}>
              <FormField label={t('settings.abbreviationDescription')}>
                <TextInput {...createForm.register('description')} />
              </FormField>
            </div>
            <Button type="submit" loading={createForm.formState.isSubmitting || createMutation.isPending}>
              {t('common.add')}
            </Button>
          </Group>
        </form>
      </Panel>

      <Panel>
        {abbreviationsQuery.isLoading ? (
          <AbbreviationsSkeleton />
        ) : abbreviations.length === 0 ? (
          <EmptyState icon={<IconAbc size={28} stroke={1.6} />} title={t('settings.noAbbreviations')} />
        ) : (
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t('settings.abbreviation')}</Table.Th>
                <Table.Th>{t('settings.fullText')}</Table.Th>
                <Table.Th>{t('settings.abbreviationDescription')}</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {abbreviations.map((a) => (
                <Table.Tr key={a.id}>
                  <Table.Td fw={500}>{a.abbreviation}</Table.Td>
                  <Table.Td>{a.fullText}</Table.Td>
                  <Table.Td>{a.description}</Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end">
                      <IconButton
                        icon={<IconEdit size={16} />}
                        label={t('common.edit')}
                        onClick={() => {
                          setEditing(a);
                          openEdit();
                        }}
                      />
                      <IconButton
                        icon={<IconTrash size={16} />}
                        label={t('common.delete')}
                        color="red"
                        onClick={() => setPendingDelete(a)}
                      />
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Panel>

      <Modal
        opened={editOpened}
        onClose={() => {
          closeEdit();
          setEditing(null);
        }}
        title={t('common.edit')}
      >
        <form onSubmit={onEdit}>
          <Stack gap="sm">
            <FormField label={t('settings.abbreviation')} error={editForm.formState.errors.abbreviation?.message}>
              <TextInput {...editForm.register('abbreviation')} />
            </FormField>
            <FormField label={t('settings.fullText')} error={editForm.formState.errors.fullText?.message}>
              <TextInput {...editForm.register('fullText')} />
            </FormField>
            <FormField label={t('settings.abbreviationDescription')}>
              <TextInput {...editForm.register('description')} />
            </FormField>
            <Button type="submit" loading={editForm.formState.isSubmitting || updateMutation.isPending} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal opened={pendingDelete !== null} onClose={() => setPendingDelete(null)} title={t('common.delete')}>
        <Stack gap="md">
          <Text className="ds-body">{t('settings.abbreviationDeleteConfirm')}</Text>
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
