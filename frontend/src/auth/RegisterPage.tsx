import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import {
  Anchor,
  Button,
  Center,
  Paper,
  PasswordInput,
  SegmentedControl,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { getPostApiAuthRegisterMutationOptions } from '../api/generated/auth/auth';
import { AppRole } from '../api/generated/models';
import { useAuth } from './AuthContext';

const schema = z.object({
  role: z.enum([AppRole.Athlete, AppRole.Coach]),
  firstName: z.string().min(1),
  lastName: z.string().min(1),
  email: z.string().min(1).email(),
  password: z
    .string()
    .min(8)
    .regex(/[A-Z]/, 'auth.passwordHint')
    .regex(/[a-z]/, 'auth.passwordHint')
    .regex(/[0-9]/, 'auth.passwordHint'),
});
type FormValues = z.infer<typeof schema>;

export function RegisterPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { login } = useAuth();

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { role: AppRole.Athlete } });

  const mutation = useMutation(getPostApiAuthRegisterMutationOptions());

  const onSubmit = handleSubmit(async (values) => {
    try {
      const result = await mutation.mutateAsync({
        data: {
          ...values,
          timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
          locale: 'cs-CZ',
        },
      });
      if (result.accessToken && result.refreshToken) {
        login(result.accessToken, result.refreshToken);
        navigate('/dashboard');
      }
    } catch {
      notifications.show({ color: 'red', title: t('common.error'), message: t('common.unknownError') });
    }
  });

  return (
    <Center mih="100vh" bg="gray.0" py="xl">
      <Paper withBorder shadow="sm" p="xl" radius="md" w={420}>
        <Stack gap="md">
          <div>
            <Title order={2}>{t('app.name')}</Title>
            <Text c="dimmed" size="sm">
              {t('auth.register')}
            </Text>
          </div>
          <form onSubmit={onSubmit}>
            <Stack gap="sm">
              <Controller
                name="role"
                control={control}
                render={({ field }) => (
                  <SegmentedControl
                    {...field}
                    fullWidth
                    data={[
                      { label: t('auth.roleAthlete'), value: AppRole.Athlete },
                      { label: t('auth.roleCoach'), value: AppRole.Coach },
                    ]}
                  />
                )}
              />
              <Stack gap="sm" style={{ flexDirection: 'row' }} display="flex">
                <TextInput
                  label={t('auth.firstName')}
                  style={{ flex: 1 }}
                  error={errors.firstName?.message}
                  {...register('firstName')}
                />
                <TextInput
                  label={t('auth.lastName')}
                  style={{ flex: 1 }}
                  error={errors.lastName?.message}
                  {...register('lastName')}
                />
              </Stack>
              <TextInput
                label={t('auth.email')}
                autoComplete="email"
                error={errors.email?.message}
                {...register('email')}
              />
              <PasswordInput
                label={t('auth.password')}
                description={t('auth.passwordHint')}
                autoComplete="new-password"
                error={errors.password?.message && t('auth.passwordHint')}
                {...register('password')}
              />
              <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
                {t('auth.registerButton')}
              </Button>
            </Stack>
          </form>
          <Text size="sm" ta="center">
            {t('auth.haveAccount')}{' '}
            <Anchor component={Link} to="/login">
              {t('auth.loginLink')}
            </Anchor>
          </Text>
        </Stack>
      </Paper>
    </Center>
  );
}
