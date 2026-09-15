import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Badge, Button, Card, Group, Loader, Modal, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconUserPlus } from '@tabler/icons-react';
import { useGetApiRelationships, getPostApiRelationshipsInviteMutationOptions, getGetApiRelationshipsQueryKey } from '../api/generated/relationships/relationships';
import { RelationshipStatus } from '../api/generated/models';

const inviteSchema = z.object({
  athleteEmail: z.string().min(1).email(),
  note: z.string().optional(),
});
type InviteFormValues = z.infer<typeof inviteSchema>;

export function AthleteListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [opened, { open, close }] = useDisclosure();

  const relationshipsQuery = useGetApiRelationships();
  const relationships = relationshipsQuery.data ?? [];
  const active = relationships.filter((r) => r.status === RelationshipStatus.Active);
  const pending = relationships.filter((r) => r.status === RelationshipStatus.PendingInvite);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<InviteFormValues>({ resolver: zodResolver(inviteSchema) });

  const inviteMutation = useMutation(getPostApiRelationshipsInviteMutationOptions());

  const onSubmit = handleSubmit(async (values) => {
    try {
      await inviteMutation.mutateAsync({ data: values });
      notifications.show({ color: 'green', message: 'Pozvání bylo odesláno.' });
      reset();
      close();
      await queryClient.invalidateQueries({ queryKey: getGetApiRelationshipsQueryKey() });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Title order={2}>{t('nav.athletes')}</Title>
        <Button leftSection={<IconUserPlus size={16} />} onClick={open}>
          {t('relationships.invite')}
        </Button>
      </Group>

      {relationshipsQuery.isLoading ? (
        <Loader />
      ) : (
        <Stack gap="xl">
          {pending.length > 0 && (
            <Stack gap="xs">
              <Text fw={500} size="sm" c="dimmed">
                {t('relationships.pendingInvites')}
              </Text>
              <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
                {pending.map((rel) => (
                  <Card key={rel.id} withBorder radius="md" p="md">
                    <Group>
                      <Avatar radius="xl" color="gray">
                        {rel.athleteName?.[0] ?? '?'}
                      </Avatar>
                      <div>
                        <Text fw={500}>{rel.athleteName}</Text>
                        <Text size="xs" c="dimmed">
                          {rel.athleteEmail}
                        </Text>
                      </div>
                      <Badge ml="auto" color="yellow" variant="light">
                        {t(`relationships.status.${rel.status}`)}
                      </Badge>
                    </Group>
                  </Card>
                ))}
              </SimpleGrid>
            </Stack>
          )}

          <Stack gap="xs">
            <Text fw={500} size="sm" c="dimmed">
              {t('nav.athletes')}
            </Text>
            {active.length === 0 ? (
              <Card withBorder radius="md" p="xl">
                <Text c="dimmed" ta="center">
                  {t('dashboard.noAthletes')}
                </Text>
              </Card>
            ) : (
              <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
                {active.map((rel) => (
                  <Card
                    key={rel.id}
                    withBorder
                    radius="md"
                    p="lg"
                    style={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/athletes/${rel.athleteUserId}`)}
                  >
                    <Group>
                      <Avatar radius="xl" color="brand">
                        {rel.athleteName?.[0] ?? '?'}
                      </Avatar>
                      <div>
                        <Text fw={500}>{rel.athleteName}</Text>
                        <Text size="xs" c="dimmed">
                          {rel.athleteEmail}
                        </Text>
                      </div>
                      <Badge ml="auto" color="green" variant="light">
                        {t(`relationships.status.${rel.status}`)}
                      </Badge>
                    </Group>
                  </Card>
                ))}
              </SimpleGrid>
            )}
          </Stack>
        </Stack>
      )}

      <Modal opened={opened} onClose={close} title={t('relationships.invite')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <TextInput
              label={t('relationships.inviteEmail')}
              error={errors.athleteEmail?.message}
              {...register('athleteEmail')}
            />
            <TextInput label={t('relationships.inviteNote')} {...register('note')} />
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('relationships.sendInvite')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
