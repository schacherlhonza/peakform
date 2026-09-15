import { SegmentedControl, Stack, Text } from '@mantine/core';
import { WellnessScale } from '../api/generated/models';

const OPTIONS = [
  { value: WellnessScale.VeryPoor, label: '1' },
  { value: WellnessScale.Poor, label: '2' },
  { value: WellnessScale.Moderate, label: '3' },
  { value: WellnessScale.Good, label: '4' },
  { value: WellnessScale.VeryGood, label: '5' },
];

export function WellnessScaleControl({
  label,
  value,
  onChange,
}: {
  label: string;
  value: WellnessScale | null | undefined;
  onChange: (value: WellnessScale) => void;
}) {
  return (
    <Stack gap={4}>
      <Text size="sm" fw={500}>
        {label}
      </Text>
      <SegmentedControl fullWidth data={OPTIONS} value={value ?? undefined} onChange={(v) => onChange(v as WellnessScale)} />
    </Stack>
  );
}
