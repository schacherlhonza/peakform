import { Stack, Text } from '@mantine/core';
import type { ReactNode } from 'react';

export interface EmptyStateProps {
  icon: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
}

/**
 * Explained empty state — never show a bare "no data" or a fake zero. Used whenever a data
 * area has nothing to show, including a missing readiness score (docs/DESIGN_SYSTEM.md §9, §7).
 */
export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <Stack align="center" gap={6} py="lg" ta="center">
      <div style={{ color: 'var(--color-text-subtle)' }}>{icon}</div>
      <Text fw={700} fz={14} c="var(--color-text)">
        {title}
      </Text>
      {description && (
        <Text className="ds-body" maw={320}>
          {description}
        </Text>
      )}
      {action && <div>{action}</div>}
    </Stack>
  );
}
