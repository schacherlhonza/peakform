import { Alert, Card, Stack, Text, Title } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { GeneratedReportDto } from '../api/generated/models';
import { InsightSeverity } from '../api/generated/models';

const severityColor: Record<string, string> = {
  [InsightSeverity.Info]: 'gray',
  [InsightSeverity.Notice]: 'yellow',
  [InsightSeverity.Attention]: 'red',
};

export function ReportCard({ title, report }: { title: string; report: GeneratedReportDto | undefined | null }) {
  const { t } = useTranslation();

  return (
    <Card withBorder radius="md" p="lg">
      <Title order={4} mb="sm">
        {title}
      </Title>
      {!report ? (
        <Text c="dimmed" size="sm">
          {t('report.notGenerated')}
        </Text>
      ) : (
        <Stack gap="sm">
          <Text size="sm">{report.narrativeText}</Text>
          {report.insights?.map((insight, i) => (
            <Alert key={i} color={severityColor[insight.severity ?? 'Info']} icon={<IconInfoCircle size={16} />} p="xs">
              {insight.message}
            </Alert>
          ))}
        </Stack>
      )}
    </Card>
  );
}
