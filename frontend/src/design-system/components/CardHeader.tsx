import { Group, Text } from '@mantine/core';
import type { ReactNode } from 'react';

export interface CardHeaderProps {
  /** Uppercase kicker label, e.g. "DNEŠNÍ TRÉNINK". */
  kicker: string;
  /** Optional larger title shown below the kicker. */
  title?: ReactNode;
  /** Right-aligned content — a StatusBadge, a text button, etc. */
  right?: ReactNode;
}

/** Flex header used at the top of most Panels — see docs/DESIGN_SYSTEM.md §6. */
export function CardHeader({ kicker, title, right }: CardHeaderProps) {
  return (
    <Group justify="space-between" align="flex-start" wrap="nowrap" gap="sm" mb={title ? 'xs' : 'sm'}>
      <div>
        <Text className="ds-eyebrow">{kicker}</Text>
        {title && <Text className="ds-card-headline">{title}</Text>}
      </div>
      {right && <div>{right}</div>}
    </Group>
  );
}
