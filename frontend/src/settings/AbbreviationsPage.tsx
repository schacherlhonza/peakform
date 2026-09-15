import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ActionIcon, Button, Card, Group, Loader, Modal, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconEdit, IconTrash } from '@tabler/icons-react';
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

export default function AbbreviationsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editOpened, { open: openEdit, close: closeEdit }] = useDisclosure();
  const [editing, setEditing] = useState<CustomAbbreviationDto | null>(null);

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
      notifications.show({ color: 'green', message: t('common.add') });
      createForm.reset({ abbreviation: '', fullText: '', description: '' });
      await invalidate();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const onEdit = editForm.handleSubmit(async (values) => {
    if (!editing?.id) return;
    try {
      await updateMutation.mutateAsync({ id: editing.id, data: values });
      notifications.show({ color: 'green', message: t('common.save') });
      closeEdit();
      setEditing(null);
      await invalidate();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  const handleDelete = async (id?: string) => {
    if (!id) return;
    if (!window.confirm(t('settings.abbreviationDeleteConfirm'))) return;
    try {
      await deleteMutation.mutateAsync({ id });
      await invalidate();
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  };

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nav.abbreviations')}</Title>

      <Card withBorder radius="md" p="lg">
        <form onSubmit={onCreate}>
          <Group align="end" wrap="wrap">
            <TextInput
              label={t('settings.abbreviation')}
              w={140}
              error={createForm.formState.errors.abbreviation?.message}
              {...createForm.register('abbreviation')}
            />
            <TextInput
              label={t('settings.fullText')}
              w={240}
              error={createForm.formState.errors.fullText?.message}
              {...createForm.register('fullText')}
            />
            <TextInput label={t('settings.abbreviationDescription')} w={280} {...createForm.register('description')} />
            <Button type="submit" loading={createForm.formState.isSubmitting || createMutation.isPending}>
              {t('common.add')}
            </Button>
          </Group>
        </form>
      </Card>

      {abbreviationsQuery.isLoading ? (
        <Loader />
      ) : abbreviations.length === 0 ? (
        <Card withBorder radius="md" p="xl">
          <Text c="dimmed" ta="center">
            {t('settings.noAbbreviations')}
          </Text>
        </Card>
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
                    <ActionIcon
                      variant="subtle"
                      onClick={() => {
                        setEditing(a);
                        openEdit();
                      }}
                    >
                      <IconEdit size={16} />
                    </ActionIcon>
                    <ActionIcon variant="subtle" color="red" onClick={() => void handleDelete(a.id)}>
                      <IconTrash size={16} />
                    </ActionIcon>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

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
            <TextInput
              label={t('settings.abbreviation')}
              error={editForm.formState.errors.abbreviation?.message}
              {...editForm.register('abbreviation')}
            />
            <TextInput
              label={t('settings.fullText')}
              error={editForm.formState.errors.fullText?.message}
              {...editForm.register('fullText')}
            />
            <TextInput label={t('settings.abbreviationDescription')} {...editForm.register('description')} />
            <Button type="submit" loading={editForm.formState.isSubmitting || updateMutation.isPending} fullWidth mt="sm">
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
