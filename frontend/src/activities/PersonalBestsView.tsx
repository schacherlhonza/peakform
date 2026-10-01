import { useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Anchor, Group, Stack, Table, Text, Tooltip } from '@mantine/core';
import { LineChart } from '@mantine/charts';
import { IconTrophy } from '@tabler/icons-react';
import { useGetApiAthletesAthleteUserIdPersonalBests } from '../api/generated/activities/activities';
import type { PersonalBestDto } from '../api/generated/models';
import { Panel, CardHeader, Badge, EmptyState, Skeleton } from '../design-system/components';
import { EFFORT_ORDER, formatEffortDetail, formatEffortValue, isDistanceEffort } from './bestEfforts';

const key = (b: PersonalBestDto) => `${b.sport}-${b.type}`;
const formatDate = (value: string | number) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString('cs-CZ', { day: 'numeric', month: 'numeric', year: 'numeric' });
};

/** A record's history as a step line: each step is an effort that beat every earlier one. */
function ProgressionChart({ best }: { best: PersonalBestDto }) {
  const { t } = useTranslation();
  const type = best.type!;
  const [now] = useState(() => Date.now());
  const data = useMemo(() => {
    const steps = (best.progression ?? []).map((s) => ({ t: new Date(s.achievedAtUtc!).getTime(), v: s.value ?? 0 }));
    // Extend the current best to today, so the last step reads as "still standing".
    if (steps.length > 0) steps.push({ t: now, v: steps[steps.length - 1].v });
    return steps;
  }, [best, now]);
  const fmt = (v: number) => formatEffortValue(type, v);
  const year = (ms: number) => new Date(ms).getFullYear().toString();

  return (
    <Stack gap={4}>
      <Text className="ds-eyebrow">
        {t('activities.records.progressionTitle', { name: t(`activities.records.type.${type}`) })}
      </Text>
      <LineChart
        h={220}
        data={data}
        dataKey="t"
        series={[{ name: 'v', color: 'var(--color-accent)', label: t(`activities.records.type.${type}`) }]}
        curveType="stepAfter"
        withLegend={false}
        withDots
        strokeWidth={2}
        gridColor="rgba(255,255,255,.07)"
        textColor="var(--color-text-muted)"
        xAxisProps={{ type: 'number', scale: 'time', domain: ['dataMin', 'dataMax'], tickFormatter: year }}
        // Faster times plot higher, like watts — "up" is always "better".
        yAxisProps={{ reversed: isDistanceEffort(type), tickFormatter: fmt, width: 70, domain: ['auto', 'auto'] }}
        // The chart also calls this with an empty label (e.g. before the pointer is over a point).
        tooltipProps={{ labelFormatter: (label: ReactNode) => formatDate(Number(label)) }}
        valueFormatter={fmt}
      />
      <Text className="ds-metadata">{t('activities.records.progressionHint', { count: (best.progression?.length ?? 0) })}</Text>
    </Stack>
  );
}

/** Personal bests derived from every activity's best efforts (backend PersonalBestService). */
export default function PersonalBestsView({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const query = useGetApiAthletesAthleteUserIdPersonalBests(athleteUserId);
  const [selected, setSelected] = useState<string | null>(null);

  const bests = useMemo(
    () =>
      [...(query.data ?? [])].sort(
        (a, b) => (a.sport ?? '').localeCompare(b.sport ?? '') || EFFORT_ORDER.indexOf(a.type!) - EFFORT_ORDER.indexOf(b.type!),
      ),
    [query.data],
  );
  const current = bests.find((b) => key(b) === selected) ?? bests[0];

  if (query.isLoading) return <Skeleton height={320} radius="var(--radius-panel)" />;
  if (bests.length === 0) {
    return (
      <Panel>
        <EmptyState icon={<IconTrophy size={28} stroke={1.6} />} title={t('activities.records.emptyTitle')} description={t('activities.records.emptyDescription')} />
      </Panel>
    );
  }

  return (
    <Stack gap="lg">
      <Panel>
        <CardHeader kicker={t('activities.records.kicker')} title={t('activities.records.title')} />
        <Table.ScrollContainer minWidth={640}>
          <Table highlightOnHover verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t('activities.records.columns.record')}</Table.Th>
                <Table.Th ta="right">{t('activities.records.columns.value')}</Table.Th>
                <Table.Th>{t('activities.records.columns.date')}</Table.Th>
                <Table.Th>{t('activities.records.columns.activity')}</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {bests.map((b) => {
                const detail = formatEffortDetail(b.type!, b.value ?? 0);
                const isSelected = key(b) === key(current);
                return (
                  <Table.Tr
                    key={key(b)}
                    onClick={() => setSelected(key(b))}
                    style={{ cursor: 'pointer', background: isSelected ? 'rgba(199, 243, 77, 0.06)' : undefined }}
                  >
                    <Table.Td>
                      <Group gap="xs" wrap="nowrap">
                        <Badge tone="neutral">{t(`sport.${b.sport}`)}</Badge>
                        <Text size="sm">{t(`activities.records.type.${b.type}`)}</Text>
                      </Group>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Text size="sm" fw={600} style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {!b.isPrecise && (
                          <Tooltip label={t('activities.records.estimate')}>
                            <span>≈ </span>
                          </Tooltip>
                        )}
                        {formatEffortValue(b.type!, b.value ?? 0)}
                      </Text>
                      {detail && <Text className="ds-metadata">{detail}</Text>}
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{formatDate(b.achievedAtUtc!)}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Anchor component={Link} to={`/activities/${b.activityId}`} size="sm" onClick={(e) => e.stopPropagation()}>
                        {b.activityTitle || t('activities.untitled')}
                      </Anchor>
                    </Table.Td>
                  </Table.Tr>
                );
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
        <Text className="ds-metadata" mt="xs">
          {t('activities.records.footnote')}
        </Text>
      </Panel>

      {current && (
        <Panel>
          <ProgressionChart best={current} />
        </Panel>
      )}
    </Stack>
  );
}
