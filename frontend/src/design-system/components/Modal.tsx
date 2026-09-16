import { Modal as MantineModal, type ModalProps } from '@mantine/core';

export type { ModalProps };

/**
 * Re-export of Mantine's Modal — focus trap, Escape-to-close, focus return and body scroll
 * lock all come from Mantine's own implementation (do not reimplement). Visual theming
 * (backdrop blur, radius, border, shadow) is applied globally via theme-extend.ts.
 */
export const Modal = MantineModal;
