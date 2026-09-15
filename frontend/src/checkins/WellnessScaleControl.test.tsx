import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MantineProvider } from '@mantine/core';
import { render } from '@testing-library/react';
import { WellnessScaleControl } from './WellnessScaleControl';
import { WellnessScale } from '../api/generated/models';

function renderControl(value: WellnessScale | null = null) {
  const onChange = vi.fn();
  render(
    <MantineProvider>
      <WellnessScaleControl label="Energie" value={value} onChange={onChange} />
    </MantineProvider>,
  );
  return onChange;
}

describe('WellnessScaleControl', () => {
  it('renders the label and five options', () => {
    renderControl();

    expect(screen.getByText('Energie')).toBeInTheDocument();
    for (const label of ['1', '2', '3', '4', '5']) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }
  });

  it('calls onChange with the selected scale value', async () => {
    const user = userEvent.setup();
    const onChange = renderControl();

    await user.click(screen.getByText('4'));

    expect(onChange).toHaveBeenCalledWith(WellnessScale.Good);
  });
});
