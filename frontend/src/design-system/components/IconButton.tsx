import { ActionIcon, Tooltip, type ActionIconProps } from '@mantine/core';
import type { MouseEventHandler, ReactNode } from 'react';

export interface IconButtonProps extends Omit<ActionIconProps, 'children'> {
  icon: ReactNode;
  /** Required — used as both the tooltip text and the accessible name (docs/DESIGN_SYSTEM.md §10). */
  label: string;
  onClick?: MouseEventHandler<HTMLButtonElement>;
  disabled?: boolean;
  type?: 'button' | 'submit';
}

/** Icon-only button. Always carries an accessible label and a matching tooltip. */
export function IconButton({ icon, label, onClick, disabled, type = 'button', variant = 'subtle', ...rest }: IconButtonProps) {
  return (
    <Tooltip label={label} withArrow openDelay={300}>
      <ActionIcon
        aria-label={label}
        onClick={onClick}
        disabled={disabled}
        type={type}
        variant={variant}
        {...rest}
      >
        {icon}
      </ActionIcon>
    </Tooltip>
  );
}
