import { AppShell, Burger, Group, NavLink, Text, Menu, Avatar, UnstyledButton } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  IconLayoutDashboard,
  IconCalendar,
  IconUsers,
  IconFlag,
  IconSalad,
  IconHeartbeat,
  IconFileText,
  IconSettings,
  IconLogout,
  IconChevronDown,
} from '@tabler/icons-react';
import { AppRole } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';

export function AppLayout() {
  const [opened, { toggle, close }] = useDisclosure();
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const isCoach = user?.role === AppRole.Coach;

  const links = isCoach
    ? [
        { to: '/dashboard', label: t('nav.dashboard'), icon: IconLayoutDashboard },
        { to: '/athletes', label: t('nav.athletes'), icon: IconUsers },
        { to: '/calendar', label: t('nav.calendar'), icon: IconCalendar },
        { to: '/reports', label: t('nav.reports'), icon: IconFileText },
        { to: '/settings', label: t('nav.settings'), icon: IconSettings },
      ]
    : [
        { to: '/dashboard', label: t('nav.dashboard'), icon: IconLayoutDashboard },
        { to: '/calendar', label: t('nav.calendar'), icon: IconCalendar },
        { to: '/races', label: t('nav.races'), icon: IconFlag },
        { to: '/nutrition', label: t('nav.nutrition'), icon: IconSalad },
        { to: '/wellness', label: t('nav.wellness'), icon: IconHeartbeat },
        { to: '/reports', label: t('nav.reports'), icon: IconFileText },
        { to: '/settings', label: t('nav.settings'), icon: IconSettings },
      ];

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{ width: 240, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group>
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
            <Text fw={700} size="lg" c="brand.7">
              {t('app.name')}
            </Text>
          </Group>
          <Menu shadow="md" width={200} position="bottom-end">
            <Menu.Target>
              <UnstyledButton>
                <Group gap={8}>
                  <Avatar radius="xl" color="brand" size={32}>
                    {isCoach ? 'T' : 'S'}
                  </Avatar>
                  <IconChevronDown size={16} />
                </Group>
              </UnstyledButton>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Item
                leftSection={<IconLogout size={16} />}
                onClick={() => {
                  logout();
                  navigate('/login');
                }}
              >
                {t('common.logout')}
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="sm">
        {links.map((link) => (
          <NavLink
            key={link.to}
            label={link.label}
            leftSection={<link.icon size={18} />}
            active={location.pathname.startsWith(link.to)}
            onClick={() => {
              navigate(link.to);
              close();
            }}
          />
        ))}
      </AppShell.Navbar>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  );
}
