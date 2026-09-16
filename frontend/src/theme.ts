import { createTheme, type MantineColorsTuple } from '@mantine/core';
import { themeComponents } from './design-system/theme-extend';

/**
 * Honza Performance palette — see docs/DESIGN_SYSTEM.md §2.
 * `brand` is the single lime accent ramp (index 5 == --color-accent exactly).
 * `dark` replaces Mantine's stock near-black ramp with our own green-black surfaces so
 * `--mantine-color-dark-*` (used internally by AppShell, Paper, Menu, etc.) agrees with tokens.css.
 */
const brand: MantineColorsTuple = [
  '#f5ffe0',
  '#ecffc4',
  '#e0ffa0',
  '#d8ff6c', // --color-accent-hover
  '#cdf95f',
  '#c7f34d', // --color-accent (primary shade)
  '#c7f34d',
  '#99ca2f', // --color-accent-deep
  '#7ea726',
  '#5c7d1c',
];

const dark: MantineColorsTuple = [
  '#f4faf7', // --color-text
  '#c9dcd3',
  '#91a79e', // --color-text-muted
  '#668078', // --color-text-subtle
  '#3a5148',
  '#274039', // SegmentedControl active segment (docs/DESIGN_SYSTEM.md §6)
  '#1b352e', // --color-surface-3 — Mantine's default body/AppShell background slot
  '#152a24', // --color-surface-2 — Mantine's default Paper/Card background slot
  '#10231d', // --color-surface-inset
  '#07110f', // --color-bg
];

export const theme = createTheme({
  primaryColor: 'brand',
  primaryShade: 5,
  autoContrast: true,
  colors: { brand, dark },
  fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  defaultRadius: 'md',
  headings: { fontWeight: '700' },
  components: themeComponents,
  other: {
    radiusPanel: '22px',
    radiusModal: '24px',
    shadowPanel: 'var(--shadow-panel)',
    shadowModal: 'var(--shadow-modal)',
    shadowAccent: 'var(--shadow-accent)',
  },
});
