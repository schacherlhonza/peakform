import { IconTrophy } from '@tabler/icons-react';
import type { GoalPriority } from '../api/generated/models';
import { priorityColor } from './raceUtils';

/**
 * The race's motivational mark: a trophy in a glowing disc tinted with the race priority colour.
 * Used on the calendar card (small) and the race detail hero (large).
 */
export function RaceBadgeIcon({ priority, size = 34 }: { priority: GoalPriority | undefined; size?: number }) {
  const color = priorityColor(priority);
  return (
    <span
      aria-hidden
      style={{
        width: size,
        height: size,
        flexShrink: 0,
        borderRadius: '50%',
        display: 'grid',
        placeItems: 'center',
        color,
        background: `radial-gradient(circle at 30% 30%, color-mix(in srgb, ${color} 38%, transparent), color-mix(in srgb, ${color} 12%, transparent))`,
        border: `1px solid color-mix(in srgb, ${color} 55%, transparent)`,
        boxShadow: `0 0 ${Math.round(size / 2)}px color-mix(in srgb, ${color} 30%, transparent)`,
      }}
    >
      <IconTrophy size={Math.round(size * 0.55)} stroke={1.8} />
    </span>
  );
}
