import { Group, Text } from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';
import type { ReactNode } from 'react';

export interface FormFieldProps {
  label: string;
  unit?: string;
  error?: string;
  children: ReactNode;
}

/**
 * Layout wrapper for form inputs — label above the field, unit right-aligned, error rendered
 * as text + icon (never a bare red border) per docs/DESIGN_SYSTEM.md §6. The input itself
 * (TextInput/NumberInput/Select/...) is already themed globally via theme-extend.ts; this
 * component only owns the surrounding chrome.
 */
export function FormField({ label, unit, error, children }: FormFieldProps) {
  return (
    <div>
      <Group justify="space-between" mb={4}>
        <Text fz={12} fw={600} c="var(--color-text-muted)">
          {label}
        </Text>
        {unit && (
          <Text fz={11} c="var(--color-text-subtle)">
            {unit}
          </Text>
        )}
      </Group>
      {children}
      {error && (
        <Group gap={4} mt={4} wrap="nowrap">
          <IconAlertCircle size={13} color="var(--color-danger)" aria-hidden stroke={1.8} />
          <Text fz={12} c="var(--color-danger)">
            {error}
          </Text>
        </Group>
      )}
    </div>
  );
}
