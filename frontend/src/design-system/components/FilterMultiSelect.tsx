import { Checkbox, CloseButton, Combobox, Group, Input, InputBase, Text, useCombobox } from '@mantine/core';

export interface FilterMultiSelectProps {
  data: { value: string; label: string }[];
  value: string[];
  onChange: (value: string[]) => void;
  /** Shown when nothing is picked, e.g. "Všechny sporty". */
  placeholder: string;
  /** Summary once more than two are picked, e.g. (n) => `${n} sporty`. */
  manySelected: (count: number) => string;
  label?: string;
  w?: number | string | Record<string, number | string>;
  clearLabel?: string;
}

/**
 * Multi-choice filter that always stays one input tall: the picked values are summarised as text
 * ("Běh, Posilovna" / "3 sporty") instead of pills, which would wrap and grow the filter row.
 * Options are checkboxes in the dropdown; picking keeps it open. Built on Mantine's Combobox.
 */
export function FilterMultiSelect({ data, value, onChange, placeholder, manySelected, label, w, clearLabel = 'Zrušit výběr' }: FilterMultiSelectProps) {
  const combobox = useCombobox({ onDropdownClose: () => combobox.resetSelectedOption() });
  const picked = data.filter((d) => value.includes(d.value));
  const summary = picked.length === 0 ? null : picked.length <= 2 ? picked.map((d) => d.label).join(', ') : manySelected(picked.length);

  const toggle = (v: string) => onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);

  return (
    <Combobox store={combobox} onOptionSubmit={toggle} withinPortal>
      <Combobox.Target>
        <InputBase
          component="button"
          type="button"
          pointer
          label={label}
          w={w}
          onClick={() => combobox.toggleDropdown()}
          rightSection={
            value.length > 0 ? (
              <CloseButton
                size="sm"
                aria-label={clearLabel}
                onMouseDown={(e) => e.preventDefault()}
                onClick={(e) => {
                  e.stopPropagation();
                  onChange([]);
                }}
              />
            ) : (
              <Combobox.Chevron />
            )
          }
          rightSectionPointerEvents={value.length > 0 ? 'all' : 'none'}
        >
          {summary ? (
            <Text component="span" fz="sm" truncate="end" display="block">
              {summary}
            </Text>
          ) : (
            <Input.Placeholder>{placeholder}</Input.Placeholder>
          )}
        </InputBase>
      </Combobox.Target>

      <Combobox.Dropdown>
        <Combobox.Options>
          {data.map((d) => (
            <Combobox.Option value={d.value} key={d.value} active={value.includes(d.value)}>
              <Group gap="sm" wrap="nowrap">
                <Checkbox checked={value.includes(d.value)} onChange={() => {}} aria-hidden tabIndex={-1} style={{ pointerEvents: 'none' }} />
                <span>{d.label}</span>
              </Group>
            </Combobox.Option>
          ))}
        </Combobox.Options>
      </Combobox.Dropdown>
    </Combobox>
  );
}
