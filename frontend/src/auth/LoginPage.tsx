import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { Anchor, Center, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { getPostApiAuthLoginMutationOptions } from '../api/generated/auth/auth';
import { Panel, Button, showToast } from '../design-system/components';
import { useAuth } from './AuthContext';

const schema = z.object({
  email: z.string().min(1).email(),
  password: z.string().min(1),
});
type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { login } = useAuth();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const mutation = useMutation(getPostApiAuthLoginMutationOptions());

  const onSubmit = handleSubmit(async (values) => {
    try {
      const result = await mutation.mutateAsync({ data: values });
      if (result.accessToken && result.refreshToken) {
        login(result.accessToken, result.refreshToken);
        navigate('/dashboard');
      }
    } catch {
      showToast({ tone: 'danger', title: t('common.error'), message: t('auth.invalidCredentials') });
    }
  });

  return (
    <Center mih="100vh">
      <Panel w={380}>
        <Stack gap="md">
          <div>
            <Title className="ds-card-headline" order={2}>
              {t('app.name')}
            </Title>
            <Text className="ds-body">{t('auth.login')}</Text>
          </div>
          <form onSubmit={onSubmit}>
            <Stack gap="sm">
              <TextInput
                label={t('auth.email')}
                autoComplete="email"
                error={errors.email?.message}
                {...register('email')}
              />
              <PasswordInput
                label={t('auth.password')}
                autoComplete="current-password"
                error={errors.password?.message}
                {...register('password')}
              />
              <Button type="submit" loading={isSubmitting} fullWidth mt="sm">
                {t('auth.loginButton')}
              </Button>
            </Stack>
          </form>
          <Text size="sm" ta="center">
            {t('auth.noAccount')}{' '}
            <Anchor component={Link} to="/register">
              {t('auth.registerLink')}
            </Anchor>
          </Text>
        </Stack>
      </Panel>
    </Center>
  );
}
