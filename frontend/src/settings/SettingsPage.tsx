import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Card, Group, Stack, Text, Title, UnstyledButton } from '@mantine/core';
import { IconAbc, IconChevronRight, IconHeartbeat, IconLink, IconLock } from '@tabler/icons-react';
import type { Icon } from '@tabler/icons-react';
import { useAuth } from '../auth/AuthContext';
import { AppRole } from '../api/generated/models';

interface SettingsLink {
  to: string;
  icon: Icon;
  label: string;
}

export default function SettingsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const isCoach = user?.role === AppRole.Coach;

  const links: SettingsLink[] = isCoach
    ? [{ to: '/settings/abbreviations', icon: IconAbc, label: t('nav.abbreviations') }]
    : [
        { to: '/settings/heart-rate-zones', icon: IconHeartbeat, label: t('settings.heartRateZones') },
        { to: '/settings/integrations', icon: IconLink, label: t('nav.integrations') },
        { to: '/settings/permissions', icon: IconLock, label: t('nav.permissions') },
      ];

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nav.settings')}</Title>
      <Stack gap="sm">
        {links.map((link) => (
          <UnstyledButton key={link.to} onClick={() => navigate(link.to)}>
            <Card withBorder radius="md" p="md">
              <Group justify="space-between">
                <Group gap="sm">
                  <link.icon size={20} />
                  <Text fw={500}>{link.label}</Text>
                </Group>
                <IconChevronRight size={16} />
              </Group>
            </Card>
          </UnstyledButton>
        ))}
      </Stack>
    </Stack>
  );
}
