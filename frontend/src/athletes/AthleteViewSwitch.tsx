import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { SegmentedControl } from '../design-system/components';

type AthleteView = 'calendar' | 'activities' | 'records';

/** The coach's sections of one athlete — calendar (plan), activity history, records. */
export function AthleteViewSwitch({ athleteId }: { athleteId: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [params] = useSearchParams();

  const current: AthleteView = !pathname.endsWith('/activities')
    ? 'calendar'
    : params.get('view') === 'records'
      ? 'records'
      : 'activities';

  const go = (view: AthleteView) => {
    if (view === 'calendar') navigate(`/athletes/${athleteId}`);
    else navigate(`/athletes/${athleteId}/activities${view === 'records' ? '?view=records' : ''}`);
  };

  return (
    <SegmentedControl
      value={current}
      onChange={(v) => go(v as AthleteView)}
      data={[
        { value: 'calendar', label: t('athletes.views.calendar') },
        { value: 'activities', label: t('athletes.views.activities') },
        { value: 'records', label: t('athletes.views.records') },
      ]}
    />
  );
}
