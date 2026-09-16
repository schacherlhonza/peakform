import { Navigate, Route, Routes } from 'react-router-dom';
import { LoginPage } from './auth/LoginPage';
import { RegisterPage } from './auth/RegisterPage';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { AppShell } from './app/AppShell';
import { DashboardPage } from './dashboard/DashboardPage';
import { CalendarPage } from './calendar/CalendarPage';
import { WorkoutDetailPage } from './workouts/WorkoutDetailPage';
import { MorningCheckInPage } from './checkins/MorningCheckInPage';
import { EveningCheckInPage } from './checkins/EveningCheckInPage';
import { ReportsPage } from './reports/ReportsPage';
import { AthleteListPage } from './athletes/AthleteListPage';
import { AthleteDetailPage } from './athletes/AthleteDetailPage';
import RacesPage from './races/RacesPage';
import NutritionPage from './nutrition/NutritionPage';
import WellnessTrendsPage from './wellness/WellnessTrendsPage';
import SettingsPage from './settings/SettingsPage';
import HeartRateZonesPage from './settings/HeartRateZonesPage';
import AbbreviationsPage from './settings/AbbreviationsPage';
import PermissionsPage from './settings/PermissionsPage';
import IntegrationsPage from './integrations/IntegrationsPage';
import ImportPage from './import/ImportPage';
import TemplatesPage from './features/templates/TemplatesPage';
import NotificationsPage from './features/notifications/NotificationsPage';
import { AppRole } from './api/generated/models';

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route
        element={
          <ProtectedRoute>
            <AppShell />
          </ProtectedRoute>
        }
      >
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route
          path="/calendar"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <CalendarPage />
            </ProtectedRoute>
          }
        />
        <Route path="/workouts/:workoutId" element={<WorkoutDetailPage />} />
        <Route path="/checkins/morning" element={<MorningCheckInPage />} />
        <Route path="/checkins/evening" element={<EveningCheckInPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/notifications" element={<NotificationsPage />} />
        <Route
          path="/templates"
          element={
            <ProtectedRoute roles={[AppRole.Coach]}>
              <TemplatesPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/races"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <RacesPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/nutrition"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <NutritionPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/wellness"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <WellnessTrendsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/import"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <ImportPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings/heart-rate-zones"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <HeartRateZonesPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings/integrations"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <IntegrationsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings/permissions"
          element={
            <ProtectedRoute roles={[AppRole.Athlete]}>
              <PermissionsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings/abbreviations"
          element={
            <ProtectedRoute roles={[AppRole.Coach]}>
              <AbbreviationsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/athletes"
          element={
            <ProtectedRoute roles={[AppRole.Coach]}>
              <AthleteListPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/athletes/:athleteId"
          element={
            <ProtectedRoute roles={[AppRole.Coach]}>
              <AthleteDetailPage />
            </ProtectedRoute>
          }
        />
      </Route>

      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
