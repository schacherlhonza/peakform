import {
  ActionIcon,
  Badge,
  Checkbox,
  Modal,
  NumberInput,
  PasswordInput,
  Select,
  SegmentedControl,
  Skeleton,
  Textarea,
  TextInput,
  Button,
  type MantineThemeComponents,
} from '@mantine/core';
import { DateInput } from '@mantine/dates';

/**
 * Mantine `.extend()` theming — see docs/DESIGN_SYSTEM.md §6. Where a component exposes
 * instance CSS variables (Button/ActionIcon/Badge/Skeleton), we set those so Mantine's own
 * compiled stylesheet keeps handling :hover/:focus natively. Where it doesn't (SegmentedControl's
 * indicator, Modal, form inputs), we set resting-state inline styles instead — hover/focus for
 * those still get a real ring from the global `:focus-visible` rule in globals.css.
 */

const isCompactSize = (size: unknown) => typeof size === 'string' && size.startsWith('compact');

const formInputBase = {
  height: '42px',
  minHeight: '42px',
  borderRadius: 'var(--radius-sm)',
  backgroundColor: 'var(--color-surface-input)',
  border: '1px solid var(--color-border)',
  color: 'var(--color-text)',
};

const formLabelBase = {
  fontSize: '12px',
  fontWeight: 600,
  color: 'var(--color-text-muted)',
  marginBottom: 'var(--space-1)',
};

const formErrorBase = {
  color: 'var(--color-danger)',
  fontSize: '12px',
  fontWeight: 500,
};

export const themeComponents: MantineThemeComponents = {
  Button: Button.extend({
    defaultProps: { radius: 'md' },
    vars: (_theme, props) => {
      const isPrimary = (props.variant ?? 'filled') === 'filled';
      const compact = isCompactSize(props.size);
      return {
        root: {
          '--button-height': compact ? '38px' : '44px',
          '--button-padding-x': '19px',
          '--button-radius': '12px',
          '--button-fz': '13px',
          '--button-bg': isPrimary ? 'var(--color-accent)' : 'var(--color-surface-2)',
          '--button-hover': isPrimary ? 'var(--color-accent-hover)' : 'var(--color-surface-3)',
          '--button-color': isPrimary ? 'var(--color-bg)' : 'var(--color-text)',
          '--button-hover-color': isPrimary ? 'var(--color-bg)' : 'var(--color-accent)',
          '--button-bd': isPrimary ? '1px solid transparent' : '1px solid var(--color-border)',
        },
      };
    },
    styles: { root: { fontWeight: 850, transition: 'transform var(--transition-fast), background-color var(--transition-fast), color var(--transition-fast)' } },
  }),

  ActionIcon: ActionIcon.extend({
    defaultProps: { radius: 'sm' },
    vars: (_theme, props) => {
      const isSubtle = (props.variant ?? 'subtle') === 'subtle' || props.variant === 'default';
      return {
        root: {
          '--ai-size': '38px',
          '--ai-radius': '10px',
          '--ai-bg': isSubtle ? 'var(--color-surface-2)' : 'var(--color-accent)',
          '--ai-hover': isSubtle ? 'var(--color-surface-3)' : 'var(--color-accent-hover)',
          '--ai-color': isSubtle ? 'var(--color-text-muted)' : 'var(--color-bg)',
          '--ai-hover-color': isSubtle ? 'var(--color-accent)' : 'var(--color-bg)',
          '--ai-bd': isSubtle ? '1px solid var(--color-border)' : '1px solid transparent',
        },
      };
    },
  }),

  Badge: Badge.extend({
    vars: () => ({
      root: {
        '--badge-height': '22px',
        '--badge-padding-x': '10px',
        '--badge-fz': '11px',
        '--badge-radius': '999px',
      },
    }),
    styles: { root: { fontWeight: 700, textTransform: 'none' } },
  }),

  SegmentedControl: SegmentedControl.extend({
    vars: () => ({
      root: {
        '--sc-radius': '12px',
        '--sc-padding': '4px',
        '--sc-font-size': '12px',
      },
    }),
    styles: {
      root: { backgroundColor: 'var(--color-surface-inset)', border: '1px solid var(--color-border)' },
      indicator: { backgroundColor: '#274039', boxShadow: '0 4px 12px rgba(0,0,0,0.3)' },
      label: { fontWeight: 700, color: 'var(--color-text-muted)' },
      innerLabel: { color: 'var(--color-text)' },
    },
  }),

  Modal: Modal.extend({
    defaultProps: {
      radius: 'var(--radius-modal)',
      overlayProps: { backgroundOpacity: 0.78, blur: 9, color: '#020806' },
      shadow: 'var(--shadow-modal)',
    },
    styles: {
      content: {
        backgroundColor: 'var(--color-surface-inset)',
        border: '1px solid var(--color-border-strong)',
        maxWidth: '680px',
      },
      header: { backgroundColor: 'transparent' },
      title: { fontWeight: 700, fontSize: '19px', color: 'var(--color-text)' },
    },
  }),

  Skeleton: Skeleton.extend({
    styles: { root: { '--skeleton-color': 'var(--color-surface-2)', '--skeleton-highlight-color': 'var(--color-surface-3)' } as React.CSSProperties },
  }),

  TextInput: TextInput.extend({ styles: { input: formInputBase, label: formLabelBase, error: formErrorBase } }),
  NumberInput: NumberInput.extend({ styles: { input: formInputBase, label: formLabelBase, error: formErrorBase } }),
  PasswordInput: PasswordInput.extend({ styles: { input: formInputBase, label: formLabelBase, error: formErrorBase } }),
  Textarea: Textarea.extend({ styles: { input: { ...formInputBase, height: 'auto', minHeight: '84px' }, label: formLabelBase, error: formErrorBase } }),
  Select: Select.extend({ styles: { input: formInputBase, label: formLabelBase, error: formErrorBase } }),
  DateInput: DateInput.extend({ styles: { input: formInputBase, label: formLabelBase, error: formErrorBase } }),
  Checkbox: Checkbox.extend({
    styles: {
      input: { backgroundColor: 'var(--color-surface-input)', borderColor: 'var(--color-border)' },
      label: { fontSize: '13px', color: 'var(--color-text)' },
    },
  }),
};
