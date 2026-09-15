import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Loader, Select, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
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
      <Title order={2}>{t('report.title')}</Title>

      <Stack gap="sm" maw={360}>
        {isCoach && (
          <Select
            label={t('report.selectAthlete')}
            data={activeAthletes.map((r) => ({ value: r.athleteUserId!, label: r.athleteName ?? '' }))}
            value={selectedAthleteId}
            onChange={setSelectedAthleteId}
          />
        )}
        <DateInput
          label={t('report.selectDate')}
          value={date}
          onChange={(v) => setDate(v ? new Date(v) : new Date())}
        />
      </Stack>

      {!athleteUserId ? (
        <Text c="dimmed">{t('report.selectAthlete')}</Text>
      ) : (
        <SimpleGrid cols={{ base: 1, md: 2 }}>
          {morningQuery.isLoading ? <Loader /> : <ReportCard title={t('report.morning')} report={morningQuery.data} />}
          {eveningQuery.isLoading ? <Loader /> : <ReportCard title={t('report.evening')} report={eveningQuery.data} />}
        </SimpleGrid>
      )}

      <Text size="xs" c="dimmed">
        {t('report.disclaimer')}
      </Text>
    </Stack>
  );
}
