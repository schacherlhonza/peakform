import { useMemo } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Badge, Button, Card, Checkbox, Group, Loader, NumberInput, Select, SimpleGrid, Stack, Text, Textarea, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconDroplet, IconSalad } from '@tabler/icons-react';
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

function FoodSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const foodQuery = useGetApiAthletesAthleteUserIdFood(athleteUserId);
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
      notifications.show({ color: 'green', message: t('nutrition.foodAdded') });
      reset({ mealType: values.mealType, description: '' });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdFoodQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Card withBorder radius="md" p="lg">
      <Group mb="sm" gap="xs">
        <IconSalad size={20} />
        <Title order={4}>{t('nutrition.foodTab')}</Title>
      </Group>

      <form onSubmit={onSubmit}>
        <Stack gap="sm">
          <Group grow align="flex-start" wrap="wrap">
            <Controller
              name="mealType"
              control={control}
              render={({ field }) => <Select label={t('nutrition.mealType')} data={mealTypeOptions} {...field} />}
            />
            <Controller
              name="estimatedCarbsGrams"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label={t('nutrition.carbs')}
                  value={field.value ?? undefined}
                  onChange={(v) => field.onChange(v === '' ? undefined : Number(v))}
                />
              )}
            />
            <Controller
              name="estimatedProteinGrams"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label={t('nutrition.protein')}
                  value={field.value ?? undefined}
                  onChange={(v) => field.onChange(v === '' ? undefined : Number(v))}
                />
              )}
            />
          </Group>
          <Textarea label={t('nutrition.description')} minRows={1} error={errors.description?.message} {...register('description')} />
          <Button type="submit" loading={isSubmitting}>
            {t('nutrition.addFood')}
          </Button>
        </Stack>
      </form>

      <Text fw={500} size="sm" mt="lg" mb="xs">
        {t('nutrition.todayFood')}
      </Text>
      {foodQuery.isLoading ? (
        <Loader size="sm" />
      ) : todayEntries.length === 0 ? (
        <Text c="dimmed" size="sm">
          {t('nutrition.noFoodToday')}
        </Text>
      ) : (
        <Stack gap="xs">
          {todayEntries.map((entry) => (
            <Card key={entry.id} withBorder radius="sm" p="xs">
              <Group justify="space-between" wrap="wrap">
                <Group gap="xs">
                  <Badge size="sm" variant="light">
                    {t(`mealType.${entry.mealType}`)}
                  </Badge>
                  <Text size="sm">{entry.description}</Text>
                </Group>
                <Text size="xs" c="dimmed">
                  {entry.consumedAtUtc && new Date(entry.consumedAtUtc).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' })}
                  {entry.estimatedCarbsGrams != null ? ` · ${entry.estimatedCarbsGrams} g S` : ''}
                  {entry.estimatedProteinGrams != null ? ` · ${entry.estimatedProteinGrams} g B` : ''}
                </Text>
              </Group>
            </Card>
          ))}
        </Stack>
      )}
    </Card>
  );
}

function HydrationSection({ athleteUserId }: { athleteUserId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const hydrationQuery = useGetApiAthletesAthleteUserIdHydration(athleteUserId);
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
      notifications.show({ color: 'green', message: t('nutrition.hydrationAdded') });
      reset({ drinkType: values.drinkType, volumeMilliliters: 250, containsElectrolytes: false });
      await queryClient.invalidateQueries({ queryKey: getGetApiAthletesAthleteUserIdHydrationQueryKey(athleteUserId) });
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Card withBorder radius="md" p="lg">
      <Group mb="sm" gap="xs">
        <IconDroplet size={20} />
        <Title order={4}>{t('nutrition.hydrationTab')}</Title>
      </Group>

      <form onSubmit={onSubmit}>
        <Stack gap="sm">
          <Group grow align="flex-start" wrap="wrap">
            <Controller
              name="drinkType"
              control={control}
              render={({ field }) => <Select label={t('nutrition.drinkType')} data={drinkTypeOptions} {...field} />}
            />
            <Controller
              name="volumeMilliliters"
              control={control}
              render={({ field }) => (
                <NumberInput label={t('nutrition.volume')} min={0} value={field.value} onChange={(v) => field.onChange(Number(v))} />
              )}
            />
            <Controller
              name="caffeineMilligrams"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label={t('nutrition.caffeine')}
                  value={field.value ?? undefined}
                  onChange={(v) => field.onChange(v === '' ? undefined : Number(v))}
                />
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

      <Text fw={500} size="sm" mt="lg" mb="xs">
        {t('nutrition.todayHydration')}
      </Text>
      {hydrationQuery.isLoading ? (
        <Loader size="sm" />
      ) : todayEntries.length === 0 ? (
        <Text c="dimmed" size="sm">
          {t('nutrition.noHydrationToday')}
        </Text>
      ) : (
        <Stack gap="xs">
          {todayEntries.map((entry) => (
            <Card key={entry.id} withBorder radius="sm" p="xs">
              <Group justify="space-between" wrap="wrap">
                <Group gap="xs">
                  <Badge size="sm" variant="light">
                    {t(`drinkType.${entry.drinkType}`)}
                  </Badge>
                  <Text size="sm">{entry.volumeMilliliters} ml</Text>
                  {entry.containsElectrolytes && (
                    <Badge size="xs" color="teal" variant="light">
                      {t('nutrition.electrolytes')}
                    </Badge>
                  )}
                </Group>
                <Text size="xs" c="dimmed">
                  {entry.consumedAtUtc && new Date(entry.consumedAtUtc).toLocaleTimeString('cs-CZ', { hour: '2-digit', minute: '2-digit' })}
                  {entry.caffeineMilligrams != null ? ` · ${entry.caffeineMilligrams} mg kofeinu` : ''}
                </Text>
              </Group>
            </Card>
          ))}
        </Stack>
      )}
    </Card>
  );
}

function NutritionPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const athleteUserId = user!.userId;

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nutrition.title')}</Title>
      <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
        <FoodSection athleteUserId={athleteUserId} />
        <HydrationSection athleteUserId={athleteUserId} />
      </SimpleGrid>
    </Stack>
  );
}

export default NutritionPage;
