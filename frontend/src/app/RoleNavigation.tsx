import {
  IconBell,
  IconCalendar,
  IconClipboardList,
  IconFileText,
  IconFlag,
  IconHeartbeat,
  IconLayoutDashboard,
  IconSalad,
  IconUsers,
} from '@tabler/icons-react';
import type { Icon } from '@tabler/icons-react';
import { AppRole } from '../api/generated/models';

export interface NavLinkDef {
  to: string;
  labelKey: string;
  icon: Icon;
}

/**
 * Primary sidebar navigation per role — see docs/DESIGN_SYSTEM.md §5 and MIGRATION.md for the
 * mapping decisions (coach has no "Kalendář" row for IDOR-adjacent reasons; the equivalent
 * lives at /athletes/:athleteId; athlete's "Tréninky" is merged into "Kalendář").
 */
export function getPrimaryNavLinks(role: AppRole | undefined): NavLinkDef[] {
  if (role === AppRole.Coach) {
    return [
      { to: '/dashboard', labelKey: 'nav.dashboard', icon: IconLayoutDashboard },
      { to: '/athletes', labelKey: 'nav.athletes', icon: IconUsers },
      { to: '/templates', labelKey: 'nav.templates', icon: IconClipboardList },
      { to: '/notifications', labelKey: 'nav.notifications', icon: IconBell },
      { to: '/reports', labelKey: 'nav.reports', icon: IconFileText },
    ];
  }
  return [
    { to: '/dashboard', labelKey: 'nav.dashboard', icon: IconLayoutDashboard },
    { to: '/calendar', labelKey: 'nav.calendar', icon: IconCalendar },
    { to: '/wellness', labelKey: 'nav.wellness', icon: IconHeartbeat },
    { to: '/reports', labelKey: 'nav.reports', icon: IconFileText },
    { to: '/races', labelKey: 'nav.races', icon: IconFlag },
    { to: '/nutrition', labelKey: 'nav.nutrition', icon: IconSalad },
  ];
}

export function isNavLinkActive(pathname: string, to: string): boolean {
  return pathname === to || pathname.startsWith(`${to}/`);
}
