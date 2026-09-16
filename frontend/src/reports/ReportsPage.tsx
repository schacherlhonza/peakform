import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select, SimpleGrid, Stack, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconReportAnalytics } from '@tabler/icons-react';
import { Panel, FormField, Skeleton, EmptyState } from '../design-system/components';
import { useGetApiRelationships } from '../api/generated/relationships/relationships';
import { useGetApiAthletesAthleteUserIdReportsDateType } from '../api/generated/reports/reports';
import { AppRole, ReportType, RelationshipStatus } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { ReportCard } from './ReportCard';

function toIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function ReportCardSkeleton() {
  return (
    <Panel>
      <Skeleton height={12} width={100} mb="md" />
      <Skeleton height={16} mb="sm" />
      <Skeleton height={16} width="80%" mb="sm" />
      <Skeleton height={44} />
    </Panel>
  );
}

export function ReportsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [date, setDate] = useState<Date>(new Date());
  const [selectedAthleteId, setSelectedAthleteId] = useState<string | null>(null);

  const isCoach = user?.role === AppRole.Coach;
  const relationshipsQuery = useGetApiRelationships({ query: { enabled: isCoach } });
  const activeAthletes = (relationshipsQuery.data ?? []).filter((r) => r.status === RelationshipStatus.Active);

  const athleteUserId = isCoach ? selectedAthleteId : user?.userId;
  const dateIso = toIsoDate(date);

  const morningQuery = useGetApiAthletesAthleteUserIdReportsDateType(athleteUserId ?? '', dateIso, ReportType.Morning, {
    query: { enabled: !!athleteUserId },
  });
  const eveningQuery = useGetApiAthletesAthleteUserIdReportsDateType(athleteUserId ?? '', dateIso, ReportType.Evening, {
    query: { enabled: !!athleteUserId },
  });

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('report.title')}
      </Title>

      <Panel>
        <Stack gap="sm" maw={360}>
          {isCoach && (
            <FormField label={t('report.selectAthlete')}>
              <Select
                data={activeAthletes.map((r) => ({ value: r.athleteUserId!, label: r.athleteName ?? '' }))}
                value={selectedAthleteId}
                onChange={setSelectedAthleteId}
              />
            </FormField>
          )}
          <FormField label={t('report.selectDate')}>
            <DateInput value={date} onChange={(v) => setDate(v ? new Date(v) : new Date())} />
          </FormField>
        </Stack>
      </Panel>

      {!athleteUserId ? (
        <EmptyState icon={<IconReportAnalytics size={28} stroke={1.6} />} title={t('report.selectAthlete')} />
      ) : (
        <SimpleGrid cols={{ base: 1, md: 2 }}>
          {morningQuery.isLoading ? <ReportCardSkeleton /> : <ReportCard title={t('report.morning')} report={morningQuery.data} />}
          {eveningQuery.isLoading ? <ReportCardSkeleton /> : <ReportCard title={t('report.evening')} report={eveningQuery.data} />}
        </SimpleGrid>
      )}
    </Stack>
  );
}
