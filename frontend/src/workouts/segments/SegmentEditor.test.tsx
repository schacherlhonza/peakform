import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { WorkoutSegmentDto } from '../../api/generated/models';
import { renderWithProviders } from '../../test/renderWithProviders';
import { SegmentEditor } from './SegmentEditor';
import { SegmentList } from './SegmentList';
import { normalizeSegments } from './segmentFormat';

function Harness() {
  const [segments, setSegments] = useState<WorkoutSegmentDto[]>([]);
  return (
    <>
      <SegmentEditor value={segments} onChange={setSegments} />
      <div data-testid="preview">
        <SegmentList segments={normalizeSegments(segments)} />
      </div>
    </>
  );
}

describe('SegmentEditor', () => {
  it('adds a repeat block with an interval and an active recovery step', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Blok opakování' }));

    expect(screen.getByText('Krok 1')).toBeInTheDocument();
    expect(screen.getByText('Krok 2')).toBeInTheDocument();
    const preview = screen.getByTestId('preview');
    expect(preview).toHaveTextContent('4× blok');
    expect(preview).toHaveTextContent('5:00 Interval');
    expect(preview).toHaveTextContent('2:00 Aktivní pauza');
    expect(preview).toHaveTextContent('Celkem: 28:00');
  });

  it('switches a step to end on lap press', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Rozklus' }));
    await user.click(screen.getByText('Tlačítko Lap'));

    expect(screen.getByTestId('preview')).toHaveTextContent('do stisku Lap Rozklus');
  });
});
