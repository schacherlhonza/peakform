import { AppRole } from '../api/generated/models';
import { useAuth } from '../auth/AuthContext';
import { AthleteDashboardPage } from './AthleteDashboardPage';
import { CoachDashboardPage } from './CoachDashboardPage';

export function DashboardPage() {
  const { user } = useAuth();
  return user?.role === AppRole.Coach ? <CoachDashboardPage /> : <AthleteDashboardPage />;
}
