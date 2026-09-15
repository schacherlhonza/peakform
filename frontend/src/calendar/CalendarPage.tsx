import { Stack, Title } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AuthContext';
import { WeekCalendar } from './WeekCalendar';

export function CalendarPage() {
  const { t } = useTranslation();
  const { user } = useAuth();

  return (
    <Stack gap="lg">
      <Title order={2}>{t('nav.calendar')}</Title>
      {user && <WeekCalendar athleteUserId={user.userId} canEdit={false} />}
    </Stack>
  );
}
