import { Box, type BoxProps } from '@mantine/core';
import type { ComponentPropsWithoutRef } from 'react';
import classes from './Panel.module.css';

export interface PanelProps extends BoxProps, Omit<ComponentPropsWithoutRef<'div'>, 'className' | 'style' | 'color'> {
  /** Removes the default padding entirely — use when a child controls its own spacing. */
  noPadding?: boolean;
  /** Use the mobile padding scale (17px) even outside a mobile breakpoint. */
  compact?: boolean;
  children?: React.ReactNode;
}

/**
 * The base surface for nearly every card/section in the app — see docs/DESIGN_SYSTEM.md §6.
 * Gradient background, subtle border, 22px radius, deep soft shadow. Avoid nesting many full
 * Panels inside each other; inner sections should use `--color-surface-inset` or a divider
 * instead of another Panel.
 */
export function Panel({ noPadding, compact, className, children, ...rest }: PanelProps) {
  const cls = [classes.panel, noPadding && classes.noPadding, compact && classes.compact, className].filter(Boolean).join(' ');
  return (
    <Box className={cls} {...rest}>
      {children}
    </Box>
  );
}
