import { Skeleton as MantineSkeleton, type SkeletonProps } from '@mantine/core';

export type { SkeletonProps };

/**
 * Re-export of Mantine's Skeleton — shimmer colors themed globally via theme-extend.ts.
 * Compose feature-local skeletons (e.g. a ReadinessCardSkeleton) from this primitive so the
 * loading state matches the final layout, per docs/DESIGN_SYSTEM.md §9.
 */
export const Skeleton = MantineSkeleton;
