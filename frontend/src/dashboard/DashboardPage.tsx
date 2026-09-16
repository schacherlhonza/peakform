import { AppRole } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { AthleteDashboardPage } from '../features/athlete-dashboard/AthleteDashboardPage';
import { CoachDashboardPage } from '../features/coach-dashboard/CoachDashboardPage';

export function DashboardPage() {
  const { user } = useAuth();
  return user?.role === AppRole.Coach ? <CoachDashboardPage /> : <AthleteDashboardPage />;
}
