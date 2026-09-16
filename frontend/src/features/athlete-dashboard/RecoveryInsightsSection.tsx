import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Stack, Text } from '@mantine/core';
import { IconBulb } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, EmptyState, Skeleton, Button } from '../../design-system/components';
import type { GeneratedReportDto } from '../../api/generated/models';
import { InsightSeverity } from '../../api/generated/models';

export interface InsightData {
  report: GeneratedReportDto | null;
  isLoading: boolean;
}

export const severityTone: Record<string, 'warning' | 'danger' | 'info'> = {
  [InsightSeverity.Attention]: 'danger',
  [InsightSeverity.Notice]: 'warning',
  [InsightSeverity.Info]: 'info',
};

/**
 * Insight rollup for the dashboard — docs/DESIGN_SYSTEM.md §7 describes claim/effect-size/
 * period/confidence fields, but the backend's GeneratedReportInsightDto only carries
 * ruleCode/severity/message (verified against the generated model). Rendering fabricated
 * statistics would violate the "never phrase correlation as causation, never fake data" rule,
 * so this shows the real rule code + message only. See MIGRATION.md.
 */
export function RecoveryInsightsSection({ data }: { data: InsightData }) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  if (data.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={140} mb="md" />
        <Skeleton height={60} />
      </Panel>
    );
  }

  const insights = data.report?.insights ?? [];

  return (
    <Panel>
      <CardHeader
        kicker={t('dashboard.insights')}
        right={
          <Button variant="default" onClick={() => navigate('/reports')}>
            {t('dashboard.viewAllInsights')}
          </Button>
        }
      />
      {insights.length === 0 ? (
        <EmptyState icon={<IconBulb size={28} stroke={1.6} />} title={t('dashboard.insightsEmptyTitle')} description={t('dashboard.insightsEmptyDescription')} />
      ) : (
        <Stack gap="sm">
          {insights.map((insight, i) => (
            <div key={insight.ruleCode ?? i} className="ds-list-row">
              <Badge tone={severityTone[insight.severity ?? ''] ?? 'neutral'}>{insight.ruleCode ?? ''}</Badge>
              <Text className="ds-body" mt={4}>
                {insight.message}
              </Text>
            </div>
          ))}
        </Stack>
      )}
      {data.report?.narrativeText && (
        <Text className="ds-metadata" mt="sm">
          {t('report.disclaimer')}
        </Text>
      )}
    </Panel>
  );
}
