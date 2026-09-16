import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { SimpleGrid, Text } from '@mantine/core';
import { IconDroplet, IconSalad } from '@tabler/icons-react';
import { Panel, CardHeader, EmptyState, Skeleton, Button } from '../../design-system/components';

export interface FuelHydrationCardData {
  carbsGrams: number;
  hydrationMilliliters: number;
  foodEntryCount: number;
  hydrationEntryCount: number;
  isLoading: boolean;
}

/**
 * Today's nutrition/hydration totals — docs/DESIGN_SYSTEM.md §7. The spec describes a
 * current/daily-target progress pair, but the domain has no real per-athlete nutrition target
 * (only logged entries); showing a fabricated target would violate the "no fake data" rule, so
 * this renders real totals-so-far with a clear next action instead. See MIGRATION.md.
 */
export function FuelHydrationCard({ data }: { data: FuelHydrationCardData }) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  if (data.isLoading) {
    return (
      <Panel>
        <Skeleton height={16} width={140} mb="md" />
        <Skeleton height={64} />
      </Panel>
    );
  }

  const hasAnyEntry = data.foodEntryCount > 0 || data.hydrationEntryCount > 0;

  return (
    <Panel>
      <CardHeader kicker={t('dashboard.fuelHydration')} />
      {!hasAnyEntry ? (
        <EmptyState
          icon={<IconSalad size={28} stroke={1.6} />}
          title={t('dashboard.fuelHydrationEmpty')}
          action={
            <Button variant="default" onClick={() => navigate('/nutrition')}>
              {t('dashboard.logNutrition')}
            </Button>
          }
        />
      ) : (
        <>
          <SimpleGrid cols={{ base: 1, xs: 2 }} spacing="sm" mt="sm">
            <div>
              <IconSalad size={20} stroke={1.8} color="var(--color-accent)" />
              <Text className="ds-eyebrow" mt={6}>
                {t('dashboard.carbsToday')}
              </Text>
              <Text className="ds-key-metric" fz={28}>
                {Math.round(data.carbsGrams)} g
              </Text>
              <Text className="ds-metadata">{t('dashboard.fuelHydrationEntries', { count: data.foodEntryCount })}</Text>
            </div>
            <div>
              <IconDroplet size={20} stroke={1.8} color="var(--color-info)" />
              <Text className="ds-eyebrow" mt={6}>
                {t('dashboard.hydrationToday')}
              </Text>
              <Text className="ds-key-metric" fz={28}>
                {(data.hydrationMilliliters / 1000).toFixed(1)} l
              </Text>
              <Text className="ds-metadata">{t('dashboard.fuelHydrationEntries', { count: data.hydrationEntryCount })}</Text>
            </div>
          </SimpleGrid>
          <Button variant="default" mt="md" onClick={() => navigate('/nutrition')}>
            {t('dashboard.logNutrition')}
          </Button>
        </>
      )}
    </Panel>
  );
}
