import { useEffect, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Loader, Stack, Text } from '@mantine/core';
import { showToast } from '../design-system/components';
import {
  getPostApiIntegrationsProviderCallbackMutationOptions,
  getGetApiIntegrationsQueryKey,
} from '../api/generated/integration-connections/integration-connections';
import { IntegrationProviderType } from '../api/generated/models';

export default function StravaCallbackPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const callbackMutation = useMutation(getPostApiIntegrationsProviderCallbackMutationOptions());
  const handled = useRef(false);

  useEffect(() => {
    if (handled.current) return;
    handled.current = true;

    const code = searchParams.get('code');
    const state = searchParams.get('state');
    const oauthError = searchParams.get('error');

    if (oauthError || !code) {
      if (!oauthError) {
        showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
      }
      navigate('/settings/integrations', { replace: true });
      return;
    }

    callbackMutation
      .mutateAsync({ provider: IntegrationProviderType.Strava, data: { code, state } })
      .then(async () => {
        showToast({ tone: 'positive', message: t('integrations.connected') });
        await queryClient.invalidateQueries({ queryKey: getGetApiIntegrationsQueryKey() });
        navigate('/settings/integrations', { replace: true });
      })
      .catch(() => {
        showToast({ tone: 'danger', title: t('common.error'), message: t('common.unknownError') });
        navigate('/settings/integrations', { replace: true });
      });
    // Runs once on mount to exchange the one-time authorization code; searchParams/navigate/etc.
    // are stable enough here that re-running this on their identity churn would risk a double exchange.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Stack align="center" justify="center" gap="md" py={80}>
      <Loader />
      <Text className="ds-body">{t('integrations.connecting')}</Text>
    </Stack>
  );
}
