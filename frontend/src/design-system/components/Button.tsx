import { Button as MantineButton, type ButtonProps } from '@mantine/core';

export type { ButtonProps };

/**
 * Re-export of Mantine's Button — its visual theming (height, radius, accent fill, hover) is
 * applied globally via theme-extend.ts. This wrapper exists so features import from the
 * design system rather than reaching into Mantine directly (docs/DESIGN_SYSTEM.md §11).
 */
export const Button = MantineButton;
