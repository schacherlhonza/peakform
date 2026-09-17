import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Group, Stack, Text, Title, UnstyledButton } from '@mantine/core';
import { IconAbc, IconChevronRight, IconHeartbeat, IconLink, IconLock, IconShieldLock } from '@tabler/icons-react';
import type { Icon } from '@tabler/icons-react';
import { Panel } from '../design-system/components';
import { useAuth } from '../auth/AuthContext';
import { AppRole } from '../api/generated/models';
import classes from './SettingsPage.module.css';

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

  const links: SettingsLink[] = [
    ...(isCoach
      ? [{ to: '/settings/abbreviations', icon: IconAbc, label: t('nav.abbreviations') }]
      : [
          { to: '/settings/heart-rate-zones', icon: IconHeartbeat, label: t('settings.heartRateZones') },
          { to: '/settings/integrations', icon: IconLink, label: t('nav.integrations') },
          { to: '/settings/permissions', icon: IconLock, label: t('nav.permissions') },
        ]),
    { to: '/settings/privacy', icon: IconShieldLock, label: t('nav.privacy') },
  ];

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('nav.settings')}
      </Title>
      <Panel>
        <Stack gap={0}>
          {links.map((link) => (
            <UnstyledButton
              key={link.to}
              className={`${classes.row} ds-list-row`}
              onClick={() => navigate(link.to)}
            >
              <Group justify="space-between" wrap="nowrap">
                <Group gap="sm">
                  <link.icon size={20} stroke={1.6} color="var(--color-text-muted)" />
                  <Text fw={600} fz={14} c="var(--color-text)">
                    {link.label}
                  </Text>
                </Group>
                <IconChevronRight size={16} color="var(--color-text-subtle)" />
              </Group>
            </UnstyledButton>
          ))}
        </Stack>
      </Panel>
    </Stack>
  );
}
