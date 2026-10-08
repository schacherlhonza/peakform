import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntensityTargetType, SportType, WorkoutSegmentType, type WorkoutTemplateDto } from '../../api/generated/models';
import { renderWithProviders } from '../../test/renderWithProviders';
import { TemplatesTable } from './TemplatesTable';

const templates: WorkoutTemplateDto[] = [
  {
    id: 'a',
    name: 'Intervaly 3×3',
    sport: SportType.Running,
    description: 'Prahové intervaly',
    segments: [{ order: 1, type: WorkoutSegmentType.Interval, durationSeconds: 180, repeatCount: 3, intensityTargetType: IntensityTargetType.HeartRateZone, targetHeartRateZoneNumber: 4, notes: 'Držet rovnoměrně' }],
  },
  { id: 'b', name: 'Regenerační klus', sport: SportType.Running, description: 'Jen na pocit', segments: [] },
  { id: 'c', name: 'Kolo vytrvalost', sport: SportType.Cycling, description: '', segments: [] },
];

const renderTable = () =>
  renderWithProviders(<TemplatesTable templates={templates} onEdit={vi.fn()} onDuplicate={vi.fn()} onDelete={vi.fn()} />);

describe('TemplatesTable', () => {
  it('shows the structure and totals in the row', () => {
    renderTable();

    expect(screen.getByText('3× Interval 3:00 Z4')).toBeInTheDocument();
    expect(screen.getByText('9:00')).toBeInTheDocument();
  });

  it('filters by text (ignoring diacritics) and by structure', async () => {
    const user = userEvent.setup();
    renderTable();

    await user.type(screen.getByPlaceholderText('Hledat v názvu a popisu…'), 'regeneracni');
    expect(screen.getByText('Regenerační klus')).toBeInTheDocument();
    expect(screen.queryByText('Intervaly 3×3')).not.toBeInTheDocument();

    await user.clear(screen.getByPlaceholderText('Hledat v názvu a popisu…'));
    await user.click(screen.getByText('Se strukturou'));
    expect(screen.getByText('Intervaly 3×3')).toBeInTheDocument();
    expect(screen.queryByText('Kolo vytrvalost')).not.toBeInTheDocument();
  });

  it('expands a row into the full step list with notes', async () => {
    const user = userEvent.setup();
    renderTable();

    expect(screen.queryByText('Držet rovnoměrně')).not.toBeInTheDocument();
    await user.click(screen.getByText('Intervaly 3×3'));
    expect(screen.getByText('Držet rovnoměrně')).toBeInTheDocument();
  });
});
