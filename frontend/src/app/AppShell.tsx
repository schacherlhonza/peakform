import { AppShell as MantineAppShell, Avatar, Group, Menu, ScrollArea, Text, UnstyledButton } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Burger } from '@mantine/core';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { IconBell, IconChevronDown, IconLogout, IconSettings } from '@tabler/icons-react';
import { useAuth } from '../auth/AuthContext';
import { AppRole } from '../api/generated/models';
import { IconButton } from '../design-system/components';
import { getPrimaryNavLinks, isNavLinkActive } from './RoleNavigation';
import classes from './AppShell.module.css';

/**
 * Application shell — fixed sidebar on desktop, off-canvas on mobile, role-based navigation.
 * Replaces the old layout/AppLayout.tsx (docs/DESIGN_SYSTEM.md §4, §5). Mantine's AppShell is
 * kept as the behavioral container (collapse/burger mechanics); all visual chrome is our own.
 */
export function AppShell() {
  const [opened, { toggle, close }] = useDisclosure();
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const links = getPrimaryNavLinks(user?.role);
  const isCoach = user?.role === AppRole.Coach;
  const initial = isCoach ? 'T' : 'S';

  const go = (to: string) => {
    navigate(to);
    close();
  };

  return (
    <MantineAppShell
      header={{ height: { base: 87, sm: 108 } }}
      navbar={{ width: { base: 246, lg: 205 }, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding={0}
    >
      <MantineAppShell.Header className={classes.header}>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap={10} wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" color="var(--color-text)" aria-label={t('common.close')} />
            <div className={classes.logo} aria-hidden>
              HP
            </div>
            <Text fw={800} fz={16} c="var(--color-text)">
              {t('app.name')}
            </Text>
          </Group>
          <Group gap={6} wrap="nowrap">
            <IconButton icon={<IconBell size={18} stroke={1.8} />} label={t('nav.notifications')} onClick={() => go('/notifications')} />
            <Menu shadow="md" width={200} position="bottom-end">
              <Menu.Target>
                <UnstyledButton className={classes.userButton}>
                  <Group gap={8} wrap="nowrap">
                    <Avatar radius="xl" size={32} color="brand">
                      {initial}
                    </Avatar>
                    <IconChevronDown size={14} color="var(--color-text-muted)" />
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
        </Group>
      </MantineAppShell.Header>

      <MantineAppShell.Navbar className={classes.navbar}>
        <ScrollArea className={classes.navScroll} type="never">
          <nav className={classes.navList} aria-label={t('app.name')}>
            {links.map((link) => {
              const active = isNavLinkActive(location.pathname, link.to);
              const Icon = link.icon;
              return (
                <button
                  key={link.to}
                  type="button"
                  className={active ? `${classes.navRow} ${classes.navRowActive}` : classes.navRow}
                  onClick={() => go(link.to)}
                  aria-current={active ? 'page' : undefined}
                >
                  <Icon size={20} stroke={1.8} />
                  <span>{t(link.labelKey)}</span>
                </button>
              );
            })}
          </nav>
        </ScrollArea>

        <div className={classes.utilityZone}>
          <button
            type="button"
            className={isNavLinkActive(location.pathname, '/settings') ? `${classes.navRow} ${classes.navRowActive}` : classes.navRow}
            onClick={() => go('/settings')}
            aria-current={isNavLinkActive(location.pathname, '/settings') ? 'page' : undefined}
          >
            <IconSettings size={20} stroke={1.8} />
            <span>{t('nav.settings')}</span>
          </button>
          <Group gap={8} px="xs" wrap="nowrap">
            <Avatar radius="xl" size={30} color="brand">
              {initial}
            </Avatar>
            <Text fz={12} fw={700} c="var(--color-text)" truncate>
              {isCoach ? t('auth.roleCoach') : t('auth.roleAthlete')}
            </Text>
          </Group>
        </div>
      </MantineAppShell.Navbar>

      <MantineAppShell.Main className={classes.main}>
        <div className={classes.content}>
          <Outlet />
        </div>
      </MantineAppShell.Main>
    </MantineAppShell>
  );
}
