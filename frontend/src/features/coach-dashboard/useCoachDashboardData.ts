import { useGetApiRelationships } from '../../api/generated/relationships/relationships';
import { useGetApiNotifications } from '../../api/generated/notifications/notifications';
import { RelationshipStatus } from '../../api/generated/models';

/** Composes roster + notification data for the coach dashboard (docs/DESIGN_SYSTEM.md §11). */
export function useCoachDashboardData() {
  const relationshipsQuery = useGetApiRelationships();
  const notificationsQuery = useGetApiNotifications();

  const athletes = (relationshipsQuery.data ?? []).filter((r) => r.status === RelationshipStatus.Active);

  return {
    athletes,
    athletesLoading: relationshipsQuery.isLoading,
    athletesError: relationshipsQuery.isError,
    refetchAthletes: () => relationshipsQuery.refetch(),
    attentionQueue: {
      notifications: notificationsQuery.data ?? [],
      isLoading: notificationsQuery.isLoading,
    },
  };
}
