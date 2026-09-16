import { Badge as MantineBadge } from '@mantine/core';
import type { ReactNode } from 'react';

export type BadgeTone = 'positive' | 'info' | 'warning' | 'danger' | 'neutral';

export interface BadgeProps {
  tone: BadgeTone;
  icon?: ReactNode;
  children: ReactNode;
}

const toneStyle: Record<BadgeTone, { bg: string; fg: string; border: string }> = {
  positive: { bg: 'rgba(199, 243, 77, 0.14)', fg: 'var(--color-accent)', border: 'rgba(199, 243, 77, 0.3)' },
  info: { bg: 'rgba(114, 168, 255, 0.14)', fg: 'var(--color-info)', border: 'rgba(114, 168, 255, 0.3)' },
  warning: { bg: 'rgba(255, 154, 97, 0.14)', fg: 'var(--color-warning)', border: 'rgba(255, 154, 97, 0.3)' },
  danger: { bg: 'rgba(255, 126, 114, 0.14)', fg: 'var(--color-danger)', border: 'rgba(255, 126, 114, 0.3)' },
  neutral: { bg: 'var(--color-surface-2)', fg: 'var(--color-text-muted)', border: 'var(--color-border)' },
};

/**
 * Status pill — color is always paired with text (never the sole signal), per
 * docs/DESIGN_SYSTEM.md §6/§9. `tone` maps to a fixed semantic meaning app-wide:
 * positive=readiness/success, info=plan, warning=load, danger=critical/error, neutral=draft.
 */
export function Badge({ tone, icon, children }: BadgeProps) {
  const { bg, fg, border } = toneStyle[tone];
  return (
    <MantineBadge
      variant="light"
      leftSection={icon}
      styles={{
        root: { backgroundColor: bg, color: fg, border: `1px solid ${border}` },
        label: { display: 'flex', alignItems: 'center', gap: 4 },
      }}
    >
      {children}
    </MantineBadge>
  );
}
