import { useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Checkbox, Group, NumberInput, Select, SimpleGrid, Stack, Text, Textarea, Title } from '@mantine/core';
import { IconDroplet, IconSalad } from '@tabler/icons-react';
import { Panel, CardHeader, Badge, FormField, Button, Skeleton, EmptyState, showToast } from '../design-system/components';
import {
  useGetApiAthletesAthleteUserIdFood,
  getGetApiAthletesAthleteUserIdFoodQueryKey,
  getPostApiAthletesAthleteUserIdFoodMutationOptions,
} from '../api/generated/food-entries/food-entries';
import {
  useGetApiAthletesAthleteUserIdHydration,
  getGetApiAthletesAthleteUserIdHydrationQueryKey,
  getPostApiAthletesAthleteUserIdHydrationMutationOptions,
} from '../api/generated/hydration-entries/hydration-entries';
import { HydrationDrinkType, MealType } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { toIsoDate } from '../calendar/dateUtils';

const mealTypeOptions = Object.values(MealType).map((value) => ({ value, label: value }));
const drinkTypeOptions = Object.values(HydrationDrinkType).map((value) => ({ value, label: value }));

const foodSchema = z.object({
  mealType: z.nativeEnum(MealType),
  description: z.string().min(1),
  estimatedCarbsGrams: z.number().optional(),
  estimatedProteinGrams: z.number().optional(),
});
type FoodFormValues = z.infer<typeof foodSchema>;

const hydrationSchema = z.object({
  drinkType: z.nativeEnum(HydrationDrinkType),
  volumeMilliliters: z.number().min(1),
  containsElectrolytes: z.boolean(),
  caffeineMilligrams: z.number().optional(),
});
type HydrationFormValues = z.infer<typeof hydrationSchema>;

function isToday(isoDateTime: string | undefined): boolean {
  if (!isoDateTime) return false;
  return toIsoDate(new Date(isoDateTime)) === toIsoDate(new Date());
}

/** The food/hydration list endpoints require from/to — without them the backend binds both
 * to DateTime.MinValue, which filters out every entry. */
function todayRange(): { from: string; to: string } {
  const start = new Date();
  start.setHours(0, 0, 0, 0);
  const end = new Date();
  end.setHours(23, 59, 59, 999);
  return { from: start.toISOString(), to: end.toISOString() };
}

function formatTime(isoDateTime: string | undefined): string {
  return isoDateTime ? new Date(isoDateTime).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' }) : '';
}

function EntriesSkeleton() {
  return (
    <Stack gap="sm" mt="lg">
      {[0, 1].map((i) => (
        <Skeleton key={i} height={40} radius="var(--radius-md)" />
      ))}
    </Stack>
  );
}

function FoodSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const foodQuery = useGetApiAthletesAthleteUserIdFood(athleteUserId, todayRange());
  const todayEntries = useMemo(
    () =>
      [...(foodQuery.data ?? [])]
        .filter((e) => isToday(e.consumedAtUtc))
        .sort((a, b) => (b.consumedAtUtc ?? '').localeCompare(a.consumedAtUtc ?? '')),
    [foodQuery.data],
  );

  const createMutation = useMutation(getPostApiAthletesAthleteUserIdFoodMutationOptions());

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FoodFormValues>({
    resolver: zodResolver(foodSchema),
    defaultValues: { mealType: MealType.Snack, description: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({
        athleteUserId,
        data: {
          athleteUserId,
          consumedAtUtc: new Date().toISOString(),
          mealType: values.mealType,
          description: values.description,
          estimatedCarbsGrams: values.estimatedCarbsGrams,
          estimatedProteinGrams: values.estimatedProteinGrams,
        },
      });
      showToast({ tone: 'positive', message: t('nutrition.foodAdded') });
      reset({ mealType: values.mealType, description: '' });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdFoodQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Panel>
      <CardHeader kicker={t('nutrition.foodTab')} right={<IconSalad size={20} stroke={1.8} color="var(--color-accent)" />} />

      <form onSubmit={onSubmit}>
        <Stack gap="sm">
          <Group grow align="flex-start" wrap="wrap">
            <Controller
              name="mealType"
              control={control}
              render={({ field }) => (
                <FormField label={t('nutrition.mealType')}>
                  <Select data={mealTypeOptions} {...field} />
                </FormField>
              )}
            />
            <Controller
              name="estimatedCarbsGrams"
              control={control}
              render={({ field }) => (
                <FormField label={t('nutrition.carbs')} unit="g">
                  <NumberInput value={field.value ?? undefined} onChange={(v) => field.onChange(v === '' ? undefined : Number(v))} />
                </FormField>
              )}
            />
            <Controller
              name="estimatedProteinGrams"
              control={control}
              render={({ field }) => (
                <FormField label={t('nutrition.protein')} unit="g">
                  <NumberInput value={field.value ?? undefined} onChange={(v) => field.onChange(v === '' ? undefined : Number(v))} />
                </FormField>
              )}
            />
          </Group>
          <FormField label={t('nutrition.description')} error={errors.description?.message}>
            <Textarea minRows={1} {...register('description')} />
          </FormField>
          <Button type="submit" loading={isSubmitting}>
            {t('nutrition.addFood')}
          </Button>
        </Stack>
      </form>

      <Text className="ds-eyebrow" mt="lg" mb="xs">
        {t('nutrition.todayFood')}
      </Text>
      {foodQuery.isLoading ? (
        <EntriesSkeleton />
      ) : todayEntries.length === 0 ? (
        <EmptyState icon={<IconSalad size={28} stroke={1.6} />} title={t('nutrition.noFoodToday')} />
      ) : (
        <Stack gap={0}>
          {todayEntries.map((entry) => (
            <div key={entry.id} className="ds-list-row">
              <Group justify="space-between" align="flex-start" wrap="nowrap">
                <div style={{ minWidth: 0 }}>
                  <Group gap={6} mb={2}>
                    <Badge tone="neutral">{t(`mealType.${entry.mealType}`)}</Badge>
                    <Text className="ds-metadata">{formatTime(entry.consumedAtUtc)}</Text>
                  </Group>
                  <Text fz={14}>{entry.description}</Text>
                </div>
                <Group gap="md" wrap="nowrap">
                  {entry.estimatedCarbsGrams != null && (
                    <div style={{ textAlign: 'right' }}>
                      <Text className="ds-key-metric" fz={16}>
                        {entry.estimatedCarbsGrams}g
                      </Text>
                      <Text className="ds-metadata">S</Text>
                    </div>
                  )}
                  {entry.estimatedProteinGrams != null && (
                    <div style={{ textAlign: 'right' }}>
                      <Text className="ds-key-metric" fz={16}>
                        {entry.estimatedProteinGrams}g
                      </Text>
                      <Text className="ds-metadata">B</Text>
                    </div>
                  )}
                </Group>
              </Group>
            </div>
          ))}
        </Stack>
      )}
    </Panel>
  );
}

function HydrationSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const hydrationQuery = useGetApiAthletesAthleteUserIdHydration(athleteUserId, todayRange());
  const todayEntries = useMemo(
    () =>
      [...(hydrationQuery.data ?? [])]
        .filter((e) => isToday(e.consumedAtUtc))
        .sort((a, b) => (b.consumedAtUtc ?? '').localeCompare(a.consumedAtUtc ?? '')),
    [hydrationQuery.data],
  );

  const createMutation = useMutation(getPostApiAthletesAthleteUserIdHydrationMutationOptions());

  const {
    handleSubmit,
    control,
    reset,
    formState: { isSubmitting },
  } = useForm<HydrationFormValues>({
    resolver: zodResolver(hydrationSchema),
    defaultValues: { drinkType: HydrationDrinkType.Water, volumeMilliliters: 250, containsElectrolytes: false },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createMutation.mutateAsync({
        athleteUserId,
        data: {
          athleteUserId,
          consumedAtUtc: new Date().toISOString(),
          drinkType: values.drinkType,
          volumeMilliliters: values.volumeMilliliters,
          containsElectrolytes: values.containsElectrolytes,
          caffeineMilligrams: values.caffeineMilligrams,
        },
      });
      showToast({ tone: 'positive', message: t('nutrition.hydrationAdded') });
      reset({ drinkType: values.drinkType, volumeMilliliters: 250, containsElectrolytes: false });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdHydrationQueryKey(athleteUserId) });
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Panel>
      <CardHeader kicker={t('nutrition.hydrationTab')} right={<IconDroplet size={20} stroke={1.8} color="var(--color-info)" />} />

      <form onSubmit={onSubmit}>
        <Stack gap="sm">
          <Group grow align="flex-start" wrap="wrap">
            <Controller
              name="drinkType"
              control={control}
              render={({ field }) => (
                <FormField label={t('nutrition.drinkType')}>
                  <Select data={drinkTypeOptions} {...field} />
                </FormField>
              )}
            />
            <Controller
              name="volumeMilliliters"
              control={control}
              render={({ field }) => (
                <FormField label={t('nutrition.volume')} unit="ml">
                  <NumberInput min={0} value={field.value} onChange={(v) => field.onChange(Number(v))} />
                </FormField>
              )}
            />
            <Controller
              name="caffeineMilligrams"
              control={control}
              render={({ field }) => (
                <FormField label={t('nutrition.caffeine')} unit="mg">
                  <NumberInput value={field.value ?? undefined} onChange={(v) => field.onChange(v === '' ? undefined : Number(v))} />
                </FormField>
              )}
            />
          </Group>
          <Controller
            name="containsElectrolytes"
            control={control}
            render={({ field }) => (
              <Checkbox
                label={t('nutrition.electrolytes')}
                checked={field.value}
                onChange={(e) => field.onChange(e.currentTarget.checked)}
              />
            )}
          />
          <Button type="submit" loading={isSubmitting}>
            {t('nutrition.addHydration')}
          </Button>
        </Stack>
      </form>

      <Text className="ds-eyebrow" mt="lg" mb="xs">
        {t('nutrition.todayHydration')}
      </Text>
      {hydrationQuery.isLoading ? (
        <EntriesSkeleton />
      ) : todayEntries.length === 0 ? (
        <EmptyState icon={<IconDroplet size={28} stroke={1.6} />} title={t('nutrition.noHydrationToday')} />
      ) : (
        <Stack gap={0}>
          {todayEntries.map((entry) => (
            <div key={entry.id} className="ds-list-row">
              <Group justify="space-between" align="flex-start" wrap="nowrap">
                <div style={{ minWidth: 0 }}>
                  <Group gap={6} mb={2}>
                    <Badge tone="neutral">{t(`drinkType.${entry.drinkType}`)}</Badge>
                    {entry.containsElectrolytes && <Badge tone="info">{t('nutrition.electrolytes')}</Badge>}
                  </Group>
                  <Text className="ds-metadata">
                    {formatTime(entry.consumedAtUtc)}
                    {entry.caffeineMilligrams != null ? ` · ${entry.caffeineMilligrams} mg kofeinu` : ''}
                  </Text>
                </div>
                <Text className="ds-key-metric" fz={16}>
                  {entry.volumeMilliliters} ml
                </Text>
              </Group>
            </div>
          ))}
        </Stack>
      )}
    </Panel>
  );
}

function NutritionPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  return (
    <Stack gap="lg">
      <Title className="ds-page-title" order={2}>
        {t('nutrition.title')}
      </Title>
      <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
        <FoodSection athleteUserId={athleteUserId} />
        <HydrationSection athleteUserId={athleteUserId} />
      </SimpleGrid>
    </Stack>
  );
}

export default NutritionPage;
