import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Avatar, Group, Stack, Text, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconUserPlus, IconUsers } from '@tabler/icons-react';
import { Panel, Badge, Button, Modal, FormField, Skeleton, EmptyState, showToast } from '../design-system/components';
import { useGetApiRelationships, getPostApiRelationshipsInviteMutationOptions, getGetApiRelationshipsQueryKey } from '../api/generated/relationships/relationships';
import { RelationshipStatus } from '../api/generated/models';
import type { CoachAthleteRelationshipDto } from '../api/generated/models';
import classes from './AthleteListPage.module.css';

const inviteSchema = z.object({
  athleteEmail: z.string().min(1).email(),
  note: z.string().optional(),
});
type InviteFormValues = z.infer<typeof inviteSchema>;

function RosterSkeleton() {
  return (
    <div className={classes.roster}>
      {[0, 1, 2].map((i) => (
        <Skeleton key={i} height={104} radius="var(--radius-panel)" />
      ))}
    </div>
  );
}

function PendingInviteCard({ relationship }: { relationship: CoachAthleteRelationshipDto }) {
  const { t } = useTranslation();
  return (
    <Panel>
      <Group wrap="nowrap">
        <Avatar radius="xl" color="gray">
          {relationship.athleteName?.[0] ?? '?'}
        </Avatar>
        <div style={{ minWidth: 0 }}>
          <Text fw={700} fz={14} truncate>
            {relationship.athleteName}
          </Text>
          <Text className="ds-metadata" truncate>
            {relationship.athleteEmail}
          </Text>
        </div>
        <Badge tone="warning">{t(`relationships.status.${relationship.status}`)}</Badge>
      </Group>
    </Panel>
  );
}

function ActiveAthleteCard({ relationship }: { relationship: CoachAthleteRelationshipDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const goToDetail = () => navigate(`/athletes/${relationship.athleteUserId}`);

  return (
    <Panel
      className={classes.card}
      role="button"
      tabIndex={0}
      onClick={goToDetail}
      onKeyDown={(e: React.KeyboardEvent) => {
        if (e.key === 'Enter' || e.key === ' ') goToDetail();
      }}
    >
      <Group wrap="nowrap">
        <Avatar radius="xl" color="brand">
          {relationship.athleteName?.[0] ?? '?'}
        </Avatar>
        <div style={{ minWidth: 0 }}>
          <Text fw={700} fz={14} truncate>
            {relationship.athleteName}
          </Text>
          <Text className="ds-metadata" truncate>
            {relationship.athleteEmail}
          </Text>
        </div>
        <Badge tone="positive">{t(`relationships.status.${relationship.status}`)}</Badge>
      </Group>
    </Panel>
  );
}

export function AthleteListPage() {
  const { t } = useTranslation();
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
      showToast({ tone: 'positive', message: t('relationships.sendInvite') });
      reset();
      close();
      await queryClient.invalidateQueries({ queryKey: getGetApiRelationshipsQueryKey() });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Title className="ds-page-title" order={2}>
          {t('nav.athletes')}
        </Title>
        <Button leftSection={<IconUserPlus size={16} />} onClick={open}>
          {t('relationships.invite')}
        </Button>
      </Group>

      {relationshipsQuery.isLoading ? (
        <RosterSkeleton />
      ) : relationshipsQuery.isError ? (
        <EmptyState
          icon={<IconUsers size={28} stroke={1.6} />}
          title={t('common.error')}
          description={t('common.unknownError')}
          action={
            <Button variant="default" onClick={() => relationshipsQuery.refetch()}>
              {t('common.back')}
            </Button>
          }
        />
      ) : (
        <Stack gap="xl">
          {pending.length > 0 && (
            <Stack gap="sm">
              <Text className="ds-eyebrow">{t('relationships.pendingInvites')}</Text>
              <div className={classes.roster}>
                {pending.map((rel) => (
                  <PendingInviteCard key={rel.id} relationship={rel} />
                ))}
              </div>
            </Stack>
          )}

          <Stack gap="sm">
            <Text className="ds-eyebrow">{t('nav.athletes')}</Text>
            {active.length === 0 ? (
              <EmptyState
                icon={<IconUsers size={28} stroke={1.6} />}
                title={t('dashboard.noAthletes')}
                action={<Button onClick={open}>{t('relationships.invite')}</Button>}
              />
            ) : (
              <div className={classes.roster}>
                {active.map((rel) => (
                  <ActiveAthleteCard key={rel.id} relationship={rel} />
                ))}
              </div>
            )}
          </Stack>
        </Stack>
      )}

      <Modal opened={opened} onClose={close} title={t('relationships.invite')}>
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            <FormField label={t('relationships.inviteEmail')} error={errors.athleteEmail?.message}>
              <TextInput {...register('athleteEmail')} />
            </FormField>
            <FormField label={t('relationships.inviteNote')}>
              <TextInput {...register('note')} />
            </FormField>
            <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
              {t('relationships.sendInvite')}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
