import { Fragment, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Group, Stack, Table, Text, TextInput, Tooltip } from '@mantine/core';
import { IconChevronDown, IconChevronRight, IconCopy, IconPencil, IconSearch, IconTrash } from '@tabler/icons-react';
import { Badge, Button, EmptyState, FilterMultiSelect, IconButton, SegmentedControl } from '../../design-system/components';
import { SegmentList } from '../../workouts/segments/SegmentList';
import { GARMIN_MAX_STEPS, formatSegmentDistance, garminStepCount, segmentTotals, summarizeStructure } from '../../workouts/segments/segmentFormat';
import { formatClock } from '../../activities/activityFormat';
import type { WorkoutTemplateDto } from '../../api/generated/models';
import classes from './TemplatesTable.module.css';

type StructureFilter = 'all' | 'structured' | 'unstructured';

const normalize = (text: string) => text.toLocaleLowerCase('cs-CZ').normalize('NFD').replace(/\p{Diacritic}/gu, '');

/**
 * Templates as a filterable table: everything a coach picks a template by — sport, the structure on one
 * line, total time/distance, Garmin step count — is visible without opening it; a row click expands the
 * full step list in place.
 */
export function TemplatesTable({
  templates,
  onEdit,
  onDuplicate,
  onDelete,
}: {
  templates: WorkoutTemplateDto[];
  onEdit: (tpl: WorkoutTemplateDto) => void;
  onDuplicate: (tpl: WorkoutTemplateDto) => void;
  onDelete: (tpl: WorkoutTemplateDto) => void;
}) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [sports, setSports] = useState<string[]>([]);
  const [structure, setStructure] = useState<StructureFilter>('all');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  // Only sports that actually have a template — an empty option is noise.
  const sportOptions = useMemo(
    () => [...new Set(templates.map((tpl) => tpl.sport).filter(Boolean))].map((s) => ({ value: s!, label: t(`sport.${s}`) })),
    [templates, t],
  );

  const filtered = useMemo(() => {
    const needle = normalize(search.trim());
    return templates.filter((tpl) => {
      const hasStructure = (tpl.segments?.length ?? 0) > 0;
      return (
        (!needle || normalize(`${tpl.name ?? ''} ${tpl.description ?? ''}`).includes(needle)) &&
        (sports.length === 0 || sports.includes(tpl.sport ?? '')) &&
        (structure === 'all' || (structure === 'structured') === hasStructure)
      );
    });
  }, [templates, search, sports, structure]);

  const hasFilters = search !== '' || sports.length > 0 || structure !== 'all';
  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <Stack gap="sm">
      {/* One row of equally tall (42 px, the input height) controls, centred on each other. */}
      <Group align="center" wrap="wrap" gap="sm">
        <TextInput
          placeholder={t('templates.searchPlaceholder')}
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(e) => setSearch(e.currentTarget.value)}
          w={{ base: '100%', sm: 260 }}
        />
        <FilterMultiSelect
          placeholder={t('activities.allSports')}
          manySelected={(count) => t('common.selectedCount', { count })}
          data={sportOptions}
          value={sports}
          onChange={setSports}
          w={{ base: '100%', sm: 220 }}
        />
        <SegmentedControl
          h={42}
          styles={{ root: { display: 'flex', alignItems: 'stretch' }, control: { display: 'flex', alignItems: 'center' } }}
          value={structure}
          onChange={(v) => setStructure(v as StructureFilter)}
          data={(['all', 'structured', 'unstructured'] as StructureFilter[]).map((v) => ({ value: v, label: t(`templates.filter.${v}`) }))}
        />
        {hasFilters && (
          <Button
            variant="subtle"
            h={42}
            onClick={() => {
              setSearch('');
              setSports([]);
              setStructure('all');
            }}
          >
            {t('activities.clearFilters')}
          </Button>
        )}
      </Group>

      {filtered.length === 0 ? (
        <EmptyState icon={<IconSearch size={28} stroke={1.6} />} title={t('templates.noMatch')} />
      ) : (
        <Table.ScrollContainer minWidth={900}>
          <Table highlightOnHover verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th w={28} />
                <Table.Th>{t('templates.name')}</Table.Th>
                <Table.Th>{t('templates.sport')}</Table.Th>
                <Table.Th>{t('workout.structure')}</Table.Th>
                <Table.Th ta="right">{t('workout.duration')}</Table.Th>
                <Table.Th ta="right">{t('workout.distance')}</Table.Th>
                <Table.Th ta="right">
                  <Tooltip label={t('templates.garminStepsHint', { max: GARMIN_MAX_STEPS })} multiline maw={260}>
                    <span>{t('templates.garminSteps')}</span>
                  </Tooltip>
                </Table.Th>
                <Table.Th w={110} />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filtered.map((tpl) => {
                const id = tpl.id ?? '';
                const segments = tpl.segments ?? [];
                const totals = segmentTotals(segments);
                const steps = garminStepCount(segments);
                const isOpen = expanded.has(id);
                return (
                  <Fragment key={id}>
                    <Table.Tr className={classes.row} onClick={() => toggle(id)} aria-expanded={isOpen}>
                      <Table.Td>{isOpen ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />}</Table.Td>
                      <Table.Td maw={320}>
                        <Text fz={14} fw={600} lineClamp={1}>
                          {tpl.name}
                        </Text>
                        {tpl.description && (
                          <Text className="ds-metadata" lineClamp={1}>
                            {tpl.description}
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td>{tpl.sport && <Badge tone="info">{t(`sport.${tpl.sport}`)}</Badge>}</Table.Td>
                      <Table.Td maw={520}>
                        {segments.length > 0 ? (
                          <Text fz={13} lineClamp={2}>
                            {summarizeStructure(segments, t)}
                          </Text>
                        ) : (
                          <Text className="ds-metadata">{t('templates.noStructure')}</Text>
                        )}
                      </Table.Td>
                      <Table.Td ta="right">{totals.durationSeconds ? formatClock(totals.durationSeconds) : '—'}</Table.Td>
                      <Table.Td ta="right">{totals.distanceMeters ? formatSegmentDistance(totals.distanceMeters) : '—'}</Table.Td>
                      <Table.Td ta="right">
                        {segments.length === 0 ? '—' : steps > GARMIN_MAX_STEPS ? <Badge tone="warning">{steps}</Badge> : steps}
                      </Table.Td>
                      <Table.Td onClick={(e) => e.stopPropagation()}>
                        <Group gap={2} wrap="nowrap" justify="flex-end">
                          <IconButton icon={<IconPencil size={16} />} label={t('common.edit')} onClick={() => onEdit(tpl)} />
                          <IconButton icon={<IconCopy size={16} />} label={t('templates.duplicate')} onClick={() => onDuplicate(tpl)} />
                          <IconButton icon={<IconTrash size={16} />} label={t('common.delete')} color="red" onClick={() => onDelete(tpl)} />
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                    {isOpen && (
                      <Table.Tr className={classes.detailRow}>
                        <Table.Td />
                        <Table.Td colSpan={7}>
                          <Stack gap="xs" py={4}>
                            {tpl.description && <Text className="ds-body">{tpl.description}</Text>}
                            {segments.length > 0 ? <SegmentList segments={segments} /> : <Text className="ds-metadata">{t('templates.noStructure')}</Text>}
                          </Stack>
                        </Table.Td>
                      </Table.Tr>
                    )}
                  </Fragment>
                );
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
