import { Stack, Text } from '@mantine/core';
import { IconBulb } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { Panel, CardHeader, Badge, EmptyState } from '../design-system/components';
import { ReportDeliveryStatus, type GeneratedReportDto } from '../api/generated/models';
import { severityTone } from '../features/athlete-dashboard/RecoveryInsightsSection';

export function ReportCard({ title, report }: { title: string; report: GeneratedReportDto | undefined | null }) {
  const { t } = useTranslation();

  return (
    <Panel>
      <CardHeader kicker={title} />
      {!report ? (
        <EmptyState icon={<IconBulb size={28} stroke={1.6} />} title={t('report.notGenerated')} />
      ) : (
        <Stack gap="sm">
          <Text className="ds-body">{report.narrativeText}</Text>
          {report.insights?.map((insight, i) => (
            <div key={insight.ruleCode ?? i} className="ds-list-row">
              <Badge tone={severityTone[insight.severity ?? ''] ?? 'neutral'}>{insight.ruleCode ?? ''}</Badge>
              <Text className="ds-body" mt={4}>
                {insight.message}
              </Text>
            </div>
          ))}
          {report.deliveryStatus === ReportDeliveryStatus.Failed && (
            <Badge tone="warning">{t('report.deliveryFailed', { channel: t(`report.channel.${report.deliveryChannel}`) })}</Badge>
          )}
          {report.generatedAtUtc && (
            <Text className="ds-metadata">
              {t('report.generatedAt', { time: new Date(report.generatedAtUtc).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' }) })}
            </Text>
          )}
          <Text className="ds-metadata">{t('report.disclaimer')}</Text>
        </Stack>
      )}
    </Panel>
  );
}
