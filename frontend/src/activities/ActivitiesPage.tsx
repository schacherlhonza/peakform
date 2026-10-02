import { useState, type ReactNode } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { keepPreviousData } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Anchor, Group, MultiSelect, Pagination, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useDebouncedCallback } from '@mantine/hooks';
import { IconRun, IconSearch } from '@tabler/icons-react';
import { useAuth } from '../auth/AuthContext';
import { useGetApiAthletesAthleteUserIdActivitiesSearch } from '../api/generated/activities/activities';
import { SportType, type CompletedActivityDto } from '../api/generated/models';
import { Panel, Badge, Button, EmptyState, MetricStrip, SegmentedControl, Skeleton } from '../design-system/components';
import { formatClock, formatDistanceKm, formatPace } from './activityFormat';
import PersonalBestsView from './PersonalBestsView';

const PAGE_SIZE = 25;
const SPORTS: SportType[] = [SportType.Running, SportType.Cycling, SportType.Swimming, SportType.Strength, SportType.CrossTraining, SportType.Other];
type Preset = 'all' | 'month' | 'quarter' | 'year';

function isoDaysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

const PRESET_DAYS: Record<Exclude<Preset, 'all'>, number> = { month: 30, quarter: 91, year: 365 };

function formatTotalDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h.toLocaleString('cs-CZ')} h ${m} min` : `${m} min`;
}

function formatTotalDistance(meters: number): string {
  const km = meters / 1000;
  return `${km.toLocaleString('cs-CZ', { maximumFractionDigits: km >= 100 ? 0 : 1 })} km`;
}

function formatStart(iso: string): { date: string; time: string } {
  const d = new Date(iso);
  return {
    date: d.toLocaleDateString('cs-CZ', { weekday: 'short', day: 'numeric', month: 'numeric', year: 'numeric' }),
    time: d.toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' }),
  };
}

function TableSkeleton() {
  return (
    <Stack gap={8}>
      {Array.from({ length: 8 }, (_, i) => (
        <Skeleton key={i} height={34} />
      ))}
    </Stack>
  );
}

function ActivityRow({ activity, listPath, onOpen }: { activity: CompletedActivityDto; listPath: string; onOpen: () => void }) {
  const { t } = useTranslation();
  const start = formatStart(activity.startedAtUtc!);
  return (
    <Table.Tr onClick={onOpen} style={{ cursor: 'pointer' }}>
      <Table.Td>
        <Text size="sm">{start.date}</Text>
        <Text className="ds-metadata">{start.time}</Text>
      </Table.Td>
      <Table.Td>
        <Badge tone="neutral">{t(`sport.${activity.sport}`)}</Badge>
      </Table.Td>
      <Table.Td>
        <Anchor component={Link} to={`/activities/${activity.id}`} state={{ listPath }} onClick={(e) => e.stopPropagation()} size="sm">
          {activity.title || t('activities.untitled')}
        </Anchor>
      </Table.Td>
      <Table.Td ta="right">{activity.distanceMeters ? formatDistanceKm(activity.distanceMeters) : '—'}</Table.Td>
      <Table.Td ta="right">{formatClock(activity.durationSeconds ?? 0)}</Table.Td>
      <Table.Td ta="right">
        {activity.sport === SportType.Running && activity.averagePaceSecondsPerKm ? formatPace(activity.averagePaceSecondsPerKm) : '—'}
      </Table.Td>
      <Table.Td ta="right">{activity.averageHeartRateBpm ? `${activity.averageHeartRateBpm} bpm` : '—'}</Table.Td>
      <Table.Td>
        <Text className="ds-metadata">{t(`activity.source.${activity.source}`, { defaultValue: activity.source })}</Text>
      </Table.Td>
    </Table.Tr>
  );
}

/** The athlete's own activities page. */
export default function ActivitiesPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  return <ActivitiesBrowser athleteUserId={user!.userId} title={t('nav.activities')} />;
}

/**
 * Full activity history of one athlete (the dashboard only shows the current week) — the athlete's
 * own page and the coach's view of an athlete share it. Filters live in the URL, and the list's
 * path rides along to the activity detail so "back" restores the same list and page.
 */
export function ActivitiesBrowser({
  athleteUserId,
  title,
  subtitle,
  viewSwitch: customViewSwitch,
}: {
  athleteUserId: string;
  title: string;
  subtitle?: ReactNode;
  /** Replaces the list/records control (the coach's athlete pages use their own section switch). */
  viewSwitch?: ReactNode;
}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [params, setParams] = useSearchParams();
  const listPath = params.toString() ? `${pathname}?${params.toString()}` : pathname;

  const search = params.get('q') ?? '';
  const from = params.get('from');
  const to = params.get('to');
  const sports = params.getAll('sport') as SportType[];
  const page = Math.max(1, Number(params.get('page')) || 1);
  const [searchInput, setSearchInput] = useState(search);

  /** Any filter change goes back to page 1; `replace` keeps typing out of the history stack. */
  const update = (changes: Record<string, string | string[] | null>) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(changes)) {
      next.delete(key);
      if (Array.isArray(value)) value.forEach((v) => next.append(key, v));
      else if (value) next.set(key, value);
    }
    if (!('page' in changes)) next.delete('page');
    setParams(next, { replace: true });
  };
  const updateSearch = useDebouncedCallback((value: string) => update({ q: value.trim() || null }), 300);

  const query = useGetApiAthletesAthleteUserIdActivitiesSearch(
    athleteUserId,
    { search: search || undefined, from: from ?? undefined, to: to ?? undefined, sports: sports.length ? sports : undefined, page, pageSize: PAGE_SIZE },
    { query: { placeholderData: keepPreviousData } },
  );

  const data = query.data;
  const total = data?.totalCount ?? 0;
  const hasFilters = !!(search || from || to || sports.length);
  const activePreset: Preset | null = !from && !to ? 'all' : !to ? (Object.entries(PRESET_DAYS).find(([, days]) => from === isoDaysAgo(days))?.[0] as Preset | undefined) ?? null : null;

  const applyPreset = (preset: Preset) =>
    update({ from: preset === 'all' ? null : isoDaysAgo(PRESET_DAYS[preset]), to: null });

  const clearFilters = () => {
    setSearchInput('');
    setParams(new URLSearchParams(), { replace: true });
  };

  const view = params.get('view') === 'records' ? 'records' : 'list';
  const viewSwitch = (
    <Group justify="space-between" align="center" wrap="wrap">
      <div>
        <Title className="ds-page-title" order={2}>
          {title}
        </Title>
        {subtitle}
      </div>
      {customViewSwitch ?? (
        <SegmentedControl
          value={view}
          onChange={(v) => setParams(v === 'records' ? new URLSearchParams({ view: 'records' }) : new URLSearchParams(), { replace: true })}
          data={[
            { value: 'list', label: t('activities.views.list') },
            { value: 'records', label: t('activities.views.records') },
          ]}
        />
      )}
    </Group>
  );

  if (view === 'records') {
    return (
      <Stack gap="lg">
        {viewSwitch}
        <PersonalBestsView athleteUserId={athleteUserId} />
      </Stack>
    );
  }

  return (
    <Stack gap="lg">
      {viewSwitch}

      <Panel>
        <Stack gap="sm">
          <Group align="end" wrap="wrap" gap="sm">
            <TextInput
              placeholder={t('activities.searchPlaceholder')}
              leftSection={<IconSearch size={16} />}
              value={searchInput}
              onChange={(e) => {
                setSearchInput(e.currentTarget.value);
                updateSearch(e.currentTarget.value);
              }}
              w={{ base: '100%', sm: 240 }}
            />
            <DateInput
              label={t('activities.fromDate')}
              value={from}
              onChange={(v) => update({ from: v })}
              clearable
              maxDate={to ?? undefined}
              w={{ base: '47%', sm: 150 }}
            />
            <DateInput
              label={t('activities.toDate')}
              value={to}
              onChange={(v) => update({ to: v })}
              clearable
              minDate={from ?? undefined}
              w={{ base: '47%', sm: 150 }}
            />
            <MultiSelect
              label={t('activities.sports')}
              placeholder={sports.length ? undefined : t('activities.allSports')}
              data={SPORTS.map((s) => ({ value: s, label: t(`sport.${s}`) }))}
              value={sports}
              onChange={(v) => update({ sport: v })}
              clearable
              w={{ base: '100%', sm: 260 }}
            />
          </Group>
          <Group justify="space-between" wrap="wrap" gap="sm">
            <SegmentedControl
              size="xs"
              value={activePreset ?? ''}
              onChange={(v) => applyPreset(v as Preset)}
              data={(['all', 'month', 'quarter', 'year'] as Preset[]).map((p) => ({ value: p, label: t(`activities.presets.${p}`) }))}
            />
            {hasFilters && (
              <Button variant="subtle" size="xs" onClick={clearFilters}>
                {t('activities.clearFilters')}
              </Button>
            )}
          </Group>
        </Stack>
      </Panel>

      {data && total > 0 && (
        <MetricStrip
          metrics={[
            { label: t('activities.summary.count'), value: data.summary!.count!.toLocaleString('cs-CZ') },
            { label: t('activities.summary.distance'), value: formatTotalDistance(data.summary!.totalDistanceMeters ?? 0) },
            { label: t('activities.summary.duration'), value: formatTotalDuration(data.summary!.totalDurationSeconds ?? 0) },
            { label: t('activities.summary.elevation'), value: `${Math.round(data.summary!.totalElevationGainMeters ?? 0).toLocaleString('cs-CZ')} m` },
          ]}
        />
      )}

      <Panel>
        {query.isLoading ? (
          <TableSkeleton />
        ) : query.isError ? (
          <EmptyState icon={<IconRun size={28} stroke={1.6} />} title={t('common.error')} description={t('common.unknownError')} />
        ) : total === 0 ? (
          hasFilters ? (
            <EmptyState icon={<IconSearch size={28} stroke={1.6} />} title={t('activities.noMatchTitle')} description={t('activities.noMatchDescription')} />
          ) : (
            <EmptyState
              icon={<IconRun size={28} stroke={1.6} />}
              title={t('activities.emptyTitle')}
              description={t('activities.emptyDescription')}
              action={
                <Button component={Link} to="/settings/integrations" variant="light">
                  {t('activities.emptyAction')}
                </Button>
              }
            />
          )
        ) : (
          <Stack gap="md" style={{ opacity: query.isPlaceholderData ? 0.6 : 1, transition: 'opacity 120ms' }}>
            <Table.ScrollContainer minWidth={820}>
              <Table highlightOnHover verticalSpacing="xs">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>{t('activities.columns.date')}</Table.Th>
                    <Table.Th>{t('activities.columns.sport')}</Table.Th>
                    <Table.Th>{t('activities.columns.title')}</Table.Th>
                    <Table.Th ta="right">{t('activities.columns.distance')}</Table.Th>
                    <Table.Th ta="right">{t('activities.columns.duration')}</Table.Th>
                    <Table.Th ta="right">{t('activities.columns.pace')}</Table.Th>
                    <Table.Th ta="right">{t('activities.columns.heartRate')}</Table.Th>
                    <Table.Th>{t('activities.columns.source')}</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {data!.items!.map((activity) => (
                    <ActivityRow
                      key={activity.id}
                      activity={activity}
                      listPath={listPath}
                      onOpen={() => navigate(`/activities/${activity.id}`, { state: { listPath } })}
                    />
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
            <Group justify="space-between" wrap="wrap">
              <Text className="ds-metadata">
                {t('activities.pageInfo', {
                  from: (page - 1) * PAGE_SIZE + 1,
                  to: Math.min(page * PAGE_SIZE, total),
                  total: total.toLocaleString('cs-CZ'),
                })}
              </Text>
              <Pagination
                total={Math.ceil(total / PAGE_SIZE)}
                value={page}
                onChange={(p) => {
                  update({ page: p > 1 ? String(p) : null });
                  window.scrollTo({ top: 0 });
                }}
                size="sm"
              />
            </Group>
          </Stack>
        )}
      </Panel>
    </Stack>
  );
}
