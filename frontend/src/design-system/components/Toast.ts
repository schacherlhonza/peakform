import { notifications } from '@mantine/notifications';

export type ToastTone = 'positive' | 'info' | 'warning' | 'danger';

const toneColor: Record<ToastTone, string> = {
  positive: 'var(--color-accent)',
  info: 'var(--color-info)',
  warning: 'var(--color-warning)',
  danger: 'var(--color-danger)',
};

export interface ShowToastOptions {
  tone: ToastTone;
  title?: string;
  message: string;
}

/**
 * Thin wrapper over Mantine's notifications system with the design system's tone mapping,
 * position, and auto-dismiss timing (docs/DESIGN_SYSTEM.md §6). Prefer this over calling
 * `notifications.show` directly so every toast in the app stays visually consistent.
 */
export function showToast({ tone, title, message }: ShowToastOptions) {
  notifications.show({
    title,
    message,
    autoClose: 3200,
    color: toneColor[tone],
    styles: {
      root: {
        backgroundColor: 'var(--color-surface-inset)',
        borderColor: `rgba(199, 243, 77, ${tone === 'positive' ? 0.35 : 0.15})`,
        maxWidth: 330,
      },
      title: { color: 'var(--color-text)' },
      description: { color: 'var(--color-text-muted)' },
    },
  });
}
